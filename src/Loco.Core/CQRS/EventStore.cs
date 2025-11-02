using System;
using System.Collections.Generic;

namespace Loco.Core.CQRS
{
    /// <summary>
    /// イベントストア インターフェース
    /// Interface for event sourcing persistence
    ///
    /// The Event Store persists all domain events that represent
    /// state changes in the system, allowing for event replay
    /// and complete audit trails
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// イベントをストアに追加
        /// Append event to the event store
        /// </summary>
        System.Threading.Tasks.Task AppendEventAsync(DomainEvent @event);

        /// <summary>
        /// 複数のイベントをストアに追加
        /// Append multiple events atomically
        /// </summary>
        System.Threading.Tasks.Task AppendEventsAsync(System.Collections.Generic.IEnumerable<DomainEvent> events);

        /// <summary>
        /// アグリゲートのすべてのイベントを取得
        /// Get all events for an aggregate
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsAsync(string aggregateId);

        /// <summary>
        /// 指定したバージョン以降のイベントを取得
        /// Get events from specific version onwards
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsAsync(string aggregateId, int fromVersion);

        /// <summary>
        /// イベントストリームをサブスクライブ
        /// Subscribe to event stream
        /// </summary>
        System.Threading.Tasks.Task SubscribeAsync(
            string aggregateId,
            Func<DomainEvent, System.Threading.Tasks.Task> handler,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// 全イベントのスナップショットを取得
        /// Get snapshot of aggregate state
        /// </summary>
        System.Threading.Tasks.Task<AggregateSnapshot?> GetSnapshotAsync(string aggregateId);

        /// <summary>
        /// スナップショットを保存
        /// Save snapshot of aggregate state
        /// </summary>
        System.Threading.Tasks.Task SaveSnapshotAsync(AggregateSnapshot snapshot);

        /// <summary>
        /// イベントのページネーション取得
        /// Get events with pagination
        /// </summary>
        System.Threading.Tasks.Task<EventPage> GetEventsPagedAsync(
            string aggregateId,
            int skip = 0,
            int take = 100);

        /// <summary>
        /// 期間内のイベントを取得
        /// Get events within time range
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsByTimeRangeAsync(
            string aggregateId,
            DateTime fromTime,
            DateTime toTime);

        /// <summary>
        /// イベントタイプ別にイベントを取得
        /// Get events of specific type
        /// </summary>
        System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsByTypeAsync(
            string aggregateId,
            string eventType);
    }

    /// <summary>
    /// アグリゲート スナップショット
    /// Snapshot of aggregate state at a point in time
    /// Used to optimize event replay performance
    /// </summary>
    public class AggregateSnapshot
    {
        public string AggregateId { get; set; } = "";

        public int Version { get; set; }

        public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;

        public string AggregateType { get; set; } = "";

        public Dictionary<string, object> State { get; set; } = new();
    }

    /// <summary>
    /// イベント ページングレスポンス
    /// Paged response of events
    /// </summary>
    public class EventPage
    {
        public System.Collections.Generic.List<DomainEvent> Events { get; set; } = new();

        public int TotalCount { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }

        public bool HasMore => (Skip + Events.Count) < TotalCount;
    }

    /// <summary>
    /// イベント エンベロープ
    /// Metadata wrapper around domain events
    /// </summary>
    public class EventEnvelope
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        public string AggregateId { get; set; } = "";

        public int Version { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string EventType { get; set; } = "";

        public DomainEvent Event { get; set; } = new WorkflowStartedEvent();

        public Dictionary<string, string> Metadata { get; set; } = new();

        public string CorrelationId { get; set; } = "";

        public string CausationId { get; set; } = "";
    }

    /// <summary>
    /// イベント ストア統計情報
    /// Statistics about event store
    /// </summary>
    public class EventStoreStats
    {
        public int TotalEventCount { get; set; }

        public int AggregateCount { get; set; }

        public DateTime OldestEventTime { get; set; }

        public DateTime NewestEventTime { get; set; }

        public Dictionary<string, int> EventTypeDistribution { get; set; } = new();

        public long ApproximateSizeBytes { get; set; }

        public DateTime LastCompactionTime { get; set; }
    }

    /// <summary>
    /// イベント ストア実装
    /// In-memory event store implementation (for testing/development)
    /// </summary>
    public class InMemoryEventStore : IEventStore
    {
        private readonly Dictionary<string, System.Collections.Generic.List<DomainEvent>> _eventsByAggregate =
            new();

        private readonly Dictionary<string, AggregateSnapshot> _snapshots = new();

        private readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);

        public async System.Threading.Tasks.Task AppendEventAsync(DomainEvent @event)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!_eventsByAggregate.ContainsKey(@event.AggregateId))
                {
                    _eventsByAggregate[@event.AggregateId] = new();
                }

                _eventsByAggregate[@event.AggregateId].Add(@event);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async System.Threading.Tasks.Task AppendEventsAsync(
            System.Collections.Generic.IEnumerable<DomainEvent> events)
        {
            await _semaphore.WaitAsync();
            try
            {
                foreach (var @event in events)
                {
                    if (!_eventsByAggregate.ContainsKey(@event.AggregateId))
                    {
                        _eventsByAggregate[@event.AggregateId] = new();
                    }

                    _eventsByAggregate[@event.AggregateId].Add(@event);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsAsync(
            string aggregateId)
        {
            var events = _eventsByAggregate.ContainsKey(aggregateId)
                ? new System.Collections.Generic.List<DomainEvent>(_eventsByAggregate[aggregateId])
                : new System.Collections.Generic.List<DomainEvent>();

            return System.Threading.Tasks.Task.FromResult(events);
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsAsync(
            string aggregateId,
            int fromVersion)
        {
            var events = _eventsByAggregate.ContainsKey(aggregateId)
                ? _eventsByAggregate[aggregateId].Where(e => e.Version >= fromVersion).ToList()
                : new System.Collections.Generic.List<DomainEvent>();

            return System.Threading.Tasks.Task.FromResult(events);
        }

        public System.Threading.Tasks.Task SubscribeAsync(
            string aggregateId,
            Func<DomainEvent, System.Threading.Tasks.Task> handler,
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Simple synchronous notification - could be enhanced with reactive streams
            if (_eventsByAggregate.ContainsKey(aggregateId))
            {
                foreach (var @event in _eventsByAggregate[aggregateId])
                {
                    handler(@event);
                }
            }

            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<AggregateSnapshot?> GetSnapshotAsync(string aggregateId)
        {
            var snapshot = _snapshots.ContainsKey(aggregateId) ? _snapshots[aggregateId] : null;
            return System.Threading.Tasks.Task.FromResult(snapshot);
        }

        public async System.Threading.Tasks.Task SaveSnapshotAsync(AggregateSnapshot snapshot)
        {
            await _semaphore.WaitAsync();
            try
            {
                _snapshots[snapshot.AggregateId] = snapshot;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public System.Threading.Tasks.Task<EventPage> GetEventsPagedAsync(
            string aggregateId,
            int skip = 0,
            int take = 100)
        {
            var allEvents = _eventsByAggregate.ContainsKey(aggregateId)
                ? _eventsByAggregate[aggregateId]
                : new System.Collections.Generic.List<DomainEvent>();

            var page = new EventPage
            {
                Events = allEvents.Skip(skip).Take(take).ToList(),
                TotalCount = allEvents.Count,
                Skip = skip,
                Take = take
            };

            return System.Threading.Tasks.Task.FromResult(page);
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsByTimeRangeAsync(
            string aggregateId,
            DateTime fromTime,
            DateTime toTime)
        {
            var events = _eventsByAggregate.ContainsKey(aggregateId)
                ? _eventsByAggregate[aggregateId]
                    .Where(e => e.Timestamp >= fromTime && e.Timestamp <= toTime)
                    .ToList()
                : new System.Collections.Generic.List<DomainEvent>();

            return System.Threading.Tasks.Task.FromResult(events);
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<DomainEvent>> GetEventsByTypeAsync(
            string aggregateId,
            string eventType)
        {
            var events = _eventsByAggregate.ContainsKey(aggregateId)
                ? _eventsByAggregate[aggregateId]
                    .Where(e => e.EventType == eventType)
                    .ToList()
                : new System.Collections.Generic.List<DomainEvent>();

            return System.Threading.Tasks.Task.FromResult(events);
        }
    }
}
