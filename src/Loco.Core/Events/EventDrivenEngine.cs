using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Loco.Core.Events
{
    /// <summary>
    /// Event-driven architecture engine with message queue, pub/sub pattern, and event sourcing.
    /// Based on n8n, Zapier, and Make.com architecture patterns for scalable workflow automation.
    /// Implements: Event Bus, Message Queue, Event Sourcing, CQRS pattern.
    /// </summary>
    public class EventDrivenEngine
    {
        private readonly ConcurrentDictionary<string, List<EventSubscription>> _subscriptions = new();
        private readonly Channel<EventMessage> _eventQueue;
        private readonly ConcurrentQueue<EventMessage> _deadLetterQueue = new();
        private readonly List<StoredEvent> _eventStore = new();
        private readonly EventEngineConfiguration _config;
        private readonly CancellationTokenSource _processingCts = new();
        private Task? _processingTask;

        public EventDrivenEngine(EventEngineConfiguration? config = null)
        {
            _config = config ?? EventEngineConfiguration.Default();
            _eventQueue = Channel.CreateBounded<EventMessage>(new BoundedChannelOptions(_config.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        #region Event Publishing

        public async Task PublishAsync(
            string eventType, object payload, Dictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            var eventMessage = new EventMessage
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                Payload = payload,
                Metadata = metadata ?? new Dictionary<string, string>(),
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Store event for event sourcing
            await StoreEventAsync(eventMessage, cancellationToken);

            // Enqueue for processing
            await _eventQueue.Writer.WriteAsync(eventMessage, cancellationToken);
        }

        public async Task PublishBatchAsync(
            List<EventMessage> events, CancellationToken cancellationToken = default)
        {
            foreach (var evt in events)
            {
                evt.EventId = evt.EventId ?? Guid.NewGuid().ToString();
                evt.Timestamp = evt.Timestamp == default ? DateTime.UtcNow : evt.Timestamp;

                await StoreEventAsync(evt, cancellationToken);
                await _eventQueue.Writer.WriteAsync(evt, cancellationToken);
            }
        }

        #endregion

        #region Event Subscription

        public async Task<string> SubscribeAsync(
            string eventType, Func<EventMessage, CancellationToken, Task> handler,
            SubscriptionOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            var subscription = new EventSubscription
            {
                SubscriptionId = Guid.NewGuid().ToString(),
                EventType = eventType,
                Handler = handler,
                Options = options ?? SubscriptionOptions.Default(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (!_subscriptions.ContainsKey(eventType))
            {
                _subscriptions[eventType] = new List<EventSubscription>();
            }

            _subscriptions[eventType].Add(subscription);

            return subscription.SubscriptionId;
        }

        public async Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            foreach (var subscriptionList in _subscriptions.Values)
            {
                var subscription = subscriptionList.FirstOrDefault(s => s.SubscriptionId == subscriptionId);
                if (subscription != null)
                {
                    subscription.IsActive = false;
                    subscriptionList.Remove(subscription);
                    break;
                }
            }
        }

        #endregion

        #region Event Processing

        public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            if (_processingTask != null)
                throw new InvalidOperationException("Event processing already started");

            _processingTask = Task.Run(async () =>
            {
                await ProcessEventQueueAsync(_processingCts.Token);
            }, cancellationToken);

            await Task.Delay(10, cancellationToken); // Give processing time to start
        }

        public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            _processingCts.Cancel();
            _eventQueue.Writer.Complete();

            if (_processingTask != null)
            {
                await _processingTask;
            }
        }

        private async Task ProcessEventQueueAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            // Start multiple worker tasks for parallel processing
            for (int i = 0; i < _config.WorkerThreads; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var eventMessage in _eventQueue.Reader.ReadAllAsync(cancellationToken))
                    {
                        await ProcessEventAsync(eventMessage, cancellationToken);
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessEventAsync(EventMessage eventMessage, CancellationToken cancellationToken)
        {
            if (!_subscriptions.TryGetValue(eventMessage.EventType, out var subscriptions))
            {
                // No subscribers for this event type
                return;
            }

            var activeSubscriptions = subscriptions.Where(s => s.IsActive).ToList();
            if (!activeSubscriptions.Any())
                return;

            var handlerTasks = new List<Task>();

            foreach (var subscription in activeSubscriptions)
            {
                handlerTasks.Add(InvokeHandlerWithRetryAsync(eventMessage, subscription, cancellationToken));
            }

            // Wait for all handlers (parallel execution)
            await Task.WhenAll(handlerTasks);
        }

        private async Task InvokeHandlerWithRetryAsync(
            EventMessage eventMessage, EventSubscription subscription, CancellationToken cancellationToken)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (attempt <= subscription.Options.MaxRetries)
            {
                try
                {
                    eventMessage.DeliveryAttempts++;

                    // Apply filter if specified
                    if (subscription.Options.Filter != null && !subscription.Options.Filter(eventMessage))
                    {
                        return; // Skip this event
                    }

                    await subscription.Handler(eventMessage, cancellationToken);

                    subscription.ProcessedCount++;
                    subscription.LastProcessedAt = DateTime.UtcNow;
                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    subscription.ErrorCount++;
                    attempt++;

                    if (attempt <= subscription.Options.MaxRetries)
                    {
                        // Exponential backoff
                        var delay = TimeSpan.FromMilliseconds(
                            subscription.Options.RetryDelayMs * Math.Pow(2, attempt - 1));
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            // All retries failed - send to dead letter queue
            await SendToDeadLetterQueueAsync(eventMessage, subscription.SubscriptionId, lastException!, cancellationToken);
        }

        #endregion

        #region Dead Letter Queue

        private async Task SendToDeadLetterQueueAsync(
            EventMessage eventMessage, string subscriptionId, Exception exception,
            CancellationToken cancellationToken)
        {
            await Task.Delay(5, cancellationToken);

            eventMessage.Metadata["dlq_reason"] = exception.Message;
            eventMessage.Metadata["dlq_subscription"] = subscriptionId;
            eventMessage.Metadata["dlq_timestamp"] = DateTime.UtcNow.ToString("O");

            _deadLetterQueue.Enqueue(eventMessage);

            // Enforce DLQ size limit
            while (_deadLetterQueue.Count > _config.DeadLetterQueueMaxSize)
            {
                _deadLetterQueue.TryDequeue(out _);
            }
        }

        public async Task<List<EventMessage>> GetDeadLetterQueueAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);
            return _deadLetterQueue.ToList();
        }

        public async Task ReprocessDeadLetterAsync(string eventId, CancellationToken cancellationToken = default)
        {
            var deadLetters = _deadLetterQueue.ToList();
            var message = deadLetters.FirstOrDefault(m => m.EventId == eventId);

            if (message != null)
            {
                message.DeliveryAttempts = 0;
                message.Metadata.Remove("dlq_reason");
                message.Metadata.Remove("dlq_subscription");
                message.Metadata.Remove("dlq_timestamp");

                await _eventQueue.Writer.WriteAsync(message, cancellationToken);

                // Remove from DLQ
                var newQueue = new ConcurrentQueue<EventMessage>(deadLetters.Where(m => m.EventId != eventId));
                while (_deadLetterQueue.TryDequeue(out _)) { }
                foreach (var msg in newQueue)
                {
                    _deadLetterQueue.Enqueue(msg);
                }
            }
        }

        #endregion

        #region Event Sourcing

        private async Task StoreEventAsync(EventMessage eventMessage, CancellationToken cancellationToken)
        {
            await Task.Delay(5, cancellationToken);

            var storedEvent = new StoredEvent
            {
                EventId = eventMessage.EventId!,
                EventType = eventMessage.EventType,
                Payload = eventMessage.Payload,
                Metadata = eventMessage.Metadata,
                Timestamp = eventMessage.Timestamp,
                CorrelationId = eventMessage.CorrelationId ?? ""
            };

            _eventStore.Add(storedEvent);

            // Enforce retention policy
            if (_eventStore.Count > _config.EventStoreMaxEvents)
            {
                var cutoff = DateTime.UtcNow.AddHours(-_config.EventStoreRetentionHours);
                _eventStore.RemoveAll(e => e.Timestamp < cutoff);
            }
        }

        public async Task<List<StoredEvent>> GetEventStreamAsync(
            string? eventType = null, DateTime? startTime = null, DateTime? endTime = null,
            int limit = 1000, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            var query = _eventStore.AsEnumerable();

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);
            if (startTime.HasValue)
                query = query.Where(e => e.Timestamp >= startTime.Value);
            if (endTime.HasValue)
                query = query.Where(e => e.Timestamp <= endTime.Value);

            return query.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        }

        public async Task<EventProjection> ProjectEventsAsync(
            string aggregateId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            var events = _eventStore
                .Where(e => e.Metadata.TryGetValue("aggregateId", out var id) && id == aggregateId)
                .OrderBy(e => e.Timestamp)
                .ToList();

            var projection = new EventProjection
            {
                AggregateId = aggregateId,
                EventCount = events.Count,
                FirstEventTime = events.FirstOrDefault()?.Timestamp,
                LastEventTime = events.LastOrDefault()?.Timestamp,
                Events = events
            };

            return projection;
        }

        #endregion

        #region Saga Pattern Support

        public async Task<SagaExecution> ExecuteSagaAsync(
            string sagaId, List<SagaStep> steps, Dictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            var execution = new SagaExecution
            {
                SagaId = sagaId,
                Steps = steps,
                StartTime = DateTime.UtcNow,
                Status = SagaStatus.Running,
                Context = context
            };

            var executedSteps = new List<SagaStep>();

            try
            {
                foreach (var step in steps)
                {
                    step.StartTime = DateTime.UtcNow;
                    step.Status = StepStatus.Running;

                    await PublishAsync($"saga.{sagaId}.step.started", new
                    {
                        sagaId,
                        stepId = step.StepId,
                        stepName = step.Name
                    }, cancellationToken: cancellationToken);

                    try
                    {
                        await step.Action(context, cancellationToken);

                        step.Status = StepStatus.Completed;
                        step.EndTime = DateTime.UtcNow;
                        executedSteps.Add(step);

                        await PublishAsync($"saga.{sagaId}.step.completed", new
                        {
                            sagaId,
                            stepId = step.StepId
                        }, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        step.Status = StepStatus.Failed;
                        step.Error = ex.Message;
                        step.EndTime = DateTime.UtcNow;

                        await PublishAsync($"saga.{sagaId}.step.failed", new
                        {
                            sagaId,
                            stepId = step.StepId,
                            error = ex.Message
                        }, cancellationToken: cancellationToken);

                        // Rollback executed steps
                        await RollbackSagaAsync(sagaId, executedSteps, context, cancellationToken);

                        execution.Status = SagaStatus.Failed;
                        execution.Error = $"Step {step.Name} failed: {ex.Message}";
                        execution.EndTime = DateTime.UtcNow;
                        return execution;
                    }
                }

                execution.Status = SagaStatus.Completed;
                execution.EndTime = DateTime.UtcNow;

                await PublishAsync($"saga.{sagaId}.completed", new
                {
                    sagaId,
                    duration = (execution.EndTime.Value - execution.StartTime).TotalSeconds
                }, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                execution.Status = SagaStatus.Failed;
                execution.Error = ex.Message;
                execution.EndTime = DateTime.UtcNow;

                await PublishAsync($"saga.{sagaId}.failed", new
                {
                    sagaId,
                    error = ex.Message
                }, cancellationToken: cancellationToken);
            }

            return execution;
        }

        private async Task RollbackSagaAsync(
            string sagaId, List<SagaStep> executedSteps, Dictionary<string, object> context,
            CancellationToken cancellationToken)
        {
            await PublishAsync($"saga.{sagaId}.rollback.started", new { sagaId }, cancellationToken: cancellationToken);

            // Rollback in reverse order
            for (int i = executedSteps.Count - 1; i >= 0; i--)
            {
                var step = executedSteps[i];
                if (step.CompensationAction != null)
                {
                    try
                    {
                        await step.CompensationAction(context, cancellationToken);

                        await PublishAsync($"saga.{sagaId}.rollback.step", new
                        {
                            sagaId,
                            stepId = step.StepId,
                            stepName = step.Name
                        }, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await PublishAsync($"saga.{sagaId}.rollback.error", new
                        {
                            sagaId,
                            stepId = step.StepId,
                            error = ex.Message
                        }, cancellationToken: cancellationToken);
                    }
                }
            }

            await PublishAsync($"saga.{sagaId}.rollback.completed", new { sagaId }, cancellationToken: cancellationToken);
        }

        #endregion

        #region Statistics

        public async Task<EventEngineStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            var allSubscriptions = _subscriptions.Values.SelectMany(s => s).ToList();

            var stats = new EventEngineStatistics
            {
                TotalEventTypes = _subscriptions.Count,
                TotalSubscriptions = allSubscriptions.Count,
                ActiveSubscriptions = allSubscriptions.Count(s => s.IsActive),
                TotalEventsProcessed = allSubscriptions.Sum(s => s.ProcessedCount),
                TotalErrors = allSubscriptions.Sum(s => s.ErrorCount),
                DeadLetterQueueSize = _deadLetterQueue.Count,
                EventStoreSize = _eventStore.Count,
                QueuedEvents = _eventQueue.Reader.Count
            };

            return stats;
        }

        #endregion
    }

    #region Models

    public class EventMessage
    {
        public string? EventId { get; set; }
        public string EventType { get; set; } = "";
        public object Payload { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string? CorrelationId { get; set; }
        public int DeliveryAttempts { get; set; }
    }

    public class EventSubscription
    {
        public string SubscriptionId { get; set; } = "";
        public string EventType { get; set; } = "";
        public Func<EventMessage, CancellationToken, Task> Handler { get; set; } = null!;
        public SubscriptionOptions Options { get; set; } = SubscriptionOptions.Default();
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public long ProcessedCount { get; set; }
        public long ErrorCount { get; set; }
        public DateTime? LastProcessedAt { get; set; }
    }

    public class SubscriptionOptions
    {
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public Func<EventMessage, bool>? Filter { get; set; }

        public static SubscriptionOptions Default() => new();
    }

    public class StoredEvent
    {
        public string EventId { get; set; } = "";
        public string EventType { get; set; } = "";
        public object Payload { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; } = "";
    }

    public class EventProjection
    {
        public string AggregateId { get; set; } = "";
        public int EventCount { get; set; }
        public DateTime? FirstEventTime { get; set; }
        public DateTime? LastEventTime { get; set; }
        public List<StoredEvent> Events { get; set; } = new();
    }

    public class SagaExecution
    {
        public string SagaId { get; set; } = "";
        public List<SagaStep> Steps { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
        public SagaStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }
    }

    public class SagaStep
    {
        public string StepId { get; set; } = "";
        public string Name { get; set; } = "";
        public Func<Dictionary<string, object>, CancellationToken, Task> Action { get; set; } = null!;
        public Func<Dictionary<string, object>, CancellationToken, Task>? CompensationAction { get; set; }
        public StepStatus Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }
    }

    public class EventEngineStatistics
    {
        public int TotalEventTypes { get; set; }
        public int TotalSubscriptions { get; set; }
        public int ActiveSubscriptions { get; set; }
        public long TotalEventsProcessed { get; set; }
        public long TotalErrors { get; set; }
        public int DeadLetterQueueSize { get; set; }
        public int EventStoreSize { get; set; }
        public int QueuedEvents { get; set; }
    }

    public class EventEngineConfiguration
    {
        public int QueueCapacity { get; set; } = 10000;
        public int WorkerThreads { get; set; } = 4;
        public int DeadLetterQueueMaxSize { get; set; } = 1000;
        public int EventStoreMaxEvents { get; set; } = 100000;
        public int EventStoreRetentionHours { get; set; } = 168; // 1 week

        public static EventEngineConfiguration Default() => new();
    }

    #endregion

    #region Enums

    public enum SagaStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        RolledBack
    }

    public enum StepStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    #endregion
}
