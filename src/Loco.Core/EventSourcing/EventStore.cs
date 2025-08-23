using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EventSourcing
{
    public interface IEventStore
    {
        Task<Guid> SaveEventAsync<T>(T @event, string aggregateId, string aggregateType, int? expectedVersion = null) where T : IEvent;
        Task<List<T>> SaveEventsAsync<T>(List<T> events, string aggregateId, string aggregateType, int? expectedVersion = null) where T : IEvent;
        Task<List<IEvent>> GetEventsAsync(string aggregateId, int fromVersion = 0);
        Task<List<IEvent>> GetEventsAsync(string aggregateId, DateTime from, DateTime to);
        Task<T> GetAggregateAsync<T>(string aggregateId) where T : AggregateRoot, new();
        Task<EventStream> GetEventStreamAsync(string aggregateId);
        Task<List<IEvent>> GetAllEventsAsync(string aggregateType = null, int skip = 0, int take = 100);
        Task<Snapshot> GetLatestSnapshotAsync(string aggregateId);
        Task SaveSnapshotAsync(Snapshot snapshot);
        Task<EventStoreStatistics> GetStatisticsAsync();
    }

    public class EventStore : IEventStore
    {
        private readonly DbContext _dbContext;
        private readonly ILogger<EventStore> _logger;
        private readonly IEventSerializer _serializer;
        private readonly List<IEventHandler> _eventHandlers;
        private readonly ISnapshotStrategy _snapshotStrategy;

        public EventStore(
            DbContext dbContext,
            ILogger<EventStore> logger,
            IEventSerializer serializer,
            ISnapshotStrategy snapshotStrategy = null)
        {
            _dbContext = dbContext;
            _logger = logger;
            _serializer = serializer;
            _eventHandlers = new List<IEventHandler>();
            _snapshotStrategy = snapshotStrategy ?? new DefaultSnapshotStrategy();
        }

        public async Task<Guid> SaveEventAsync<T>(T @event, string aggregateId, string aggregateType, int? expectedVersion = null) where T : IEvent
        {
            return (await SaveEventsAsync(new List<T> { @event }, aggregateId, aggregateType, expectedVersion)).First();
        }

        public async Task<List<Guid>> SaveEventsAsync<T>(List<T> events, string aggregateId, string aggregateType, int? expectedVersion = null) where T : IEvent
        {
            if (!events.Any())
                return new List<Guid>();

            var eventIds = new List<Guid>();
            
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            
            try
            {
                var currentVersion = await GetCurrentVersionAsync(aggregateId);
                
                if (expectedVersion.HasValue && currentVersion != expectedVersion.Value)
                {
                    throw new EventStoreConcurrencyException(
                        $"Expected version {expectedVersion.Value} but current version is {currentVersion}");
                }

                foreach (var @event in events)
                {
                    currentVersion++;
                    
                    var eventEntity = new EventEntity
                    {
                        Id = Guid.NewGuid(),
                        AggregateId = aggregateId,
                        AggregateType = aggregateType,
                        EventType = @event.GetType().Name,
                        EventData = _serializer.Serialize(@event),
                        Metadata = _serializer.Serialize(new EventMetadata
                        {
                            UserId = @event.UserId,
                            CorrelationId = @event.CorrelationId,
                            CausationId = @event.CausationId,
                            Timestamp = @event.Timestamp
                        }),
                        Version = currentVersion,
                        Timestamp = @event.Timestamp,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.Set<EventEntity>().Add(eventEntity);
                    eventIds.Add(eventEntity.Id);
                    
                    _logger.LogDebug("Saved event {EventType} for aggregate {AggregateId} version {Version}", 
                        @event.GetType().Name, aggregateId, currentVersion);
                }

                await _dbContext.SaveChangesAsync();
                
                if (_snapshotStrategy.ShouldTakeSnapshot(aggregateId, currentVersion))
                {
                    await CreateSnapshotAsync(aggregateId, aggregateType, currentVersion);
                }

                await transaction.CommitAsync();
                
                await PublishEventsAsync(events);
                
                return eventIds;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving events for aggregate {AggregateId}", aggregateId);
                throw;
            }
        }

        public async Task<List<IEvent>> GetEventsAsync(string aggregateId, int fromVersion = 0)
        {
            var eventEntities = await _dbContext.Set<EventEntity>()
                .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
                .OrderBy(e => e.Version)
                .ToListAsync();

            return eventEntities.Select(e => _serializer.Deserialize(e.EventData, e.EventType)).ToList();
        }

        public async Task<List<IEvent>> GetEventsAsync(string aggregateId, DateTime from, DateTime to)
        {
            var eventEntities = await _dbContext.Set<EventEntity>()
                .Where(e => e.AggregateId == aggregateId && e.Timestamp >= from && e.Timestamp <= to)
                .OrderBy(e => e.Version)
                .ToListAsync();

            return eventEntities.Select(e => _serializer.Deserialize(e.EventData, e.EventType)).ToList();
        }

        public async Task<T> GetAggregateAsync<T>(string aggregateId) where T : AggregateRoot, new()
        {
            var aggregate = new T();
            
            var snapshot = await GetLatestSnapshotAsync(aggregateId);
            if (snapshot != null)
            {
                aggregate.LoadFromSnapshot(snapshot);
                var events = await GetEventsAsync(aggregateId, snapshot.Version);
                aggregate.LoadFromHistory(events);
            }
            else
            {
                var events = await GetEventsAsync(aggregateId);
                aggregate.LoadFromHistory(events);
            }
            
            return aggregate;
        }

        public async Task<EventStream> GetEventStreamAsync(string aggregateId)
        {
            var events = await GetEventsAsync(aggregateId);
            var currentVersion = await GetCurrentVersionAsync(aggregateId);
            
            return new EventStream
            {
                AggregateId = aggregateId,
                Events = events,
                Version = currentVersion
            };
        }

        public async Task<List<IEvent>> GetAllEventsAsync(string aggregateType = null, int skip = 0, int take = 100)
        {
            var query = _dbContext.Set<EventEntity>().AsQueryable();
            
            if (!string.IsNullOrEmpty(aggregateType))
            {
                query = query.Where(e => e.AggregateType == aggregateType);
            }
            
            var eventEntities = await query
                .OrderBy(e => e.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return eventEntities.Select(e => _serializer.Deserialize(e.EventData, e.EventType)).ToList();
        }

        public async Task<Snapshot> GetLatestSnapshotAsync(string aggregateId)
        {
            var snapshotEntity = await _dbContext.Set<SnapshotEntity>()
                .Where(s => s.AggregateId == aggregateId)
                .OrderByDescending(s => s.Version)
                .FirstOrDefaultAsync();

            if (snapshotEntity == null)
                return null;

            return new Snapshot
            {
                AggregateId = snapshotEntity.AggregateId,
                AggregateType = snapshotEntity.AggregateType,
                Data = snapshotEntity.Data,
                Version = snapshotEntity.Version,
                Timestamp = snapshotEntity.Timestamp
            };
        }

        public async Task SaveSnapshotAsync(Snapshot snapshot)
        {
            var snapshotEntity = new SnapshotEntity
            {
                Id = Guid.NewGuid(),
                AggregateId = snapshot.AggregateId,
                AggregateType = snapshot.AggregateType,
                Data = snapshot.Data,
                Version = snapshot.Version,
                Timestamp = snapshot.Timestamp,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Set<SnapshotEntity>().Add(snapshotEntity);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Saved snapshot for aggregate {AggregateId} at version {Version}", 
                snapshot.AggregateId, snapshot.Version);
        }

        public async Task<EventStoreStatistics> GetStatisticsAsync()
        {
            var totalEvents = await _dbContext.Set<EventEntity>().CountAsync();
            var totalSnapshots = await _dbContext.Set<SnapshotEntity>().CountAsync();
            var aggregateTypes = await _dbContext.Set<EventEntity>()
                .Select(e => e.AggregateType)
                .Distinct()
                .CountAsync();
            var eventTypes = await _dbContext.Set<EventEntity>()
                .Select(e => e.EventType)
                .Distinct()
                .ToListAsync();

            return new EventStoreStatistics
            {
                TotalEvents = totalEvents,
                TotalSnapshots = totalSnapshots,
                AggregateTypes = aggregateTypes,
                EventTypes = eventTypes,
                LastEventTime = await _dbContext.Set<EventEntity>()
                    .OrderByDescending(e => e.Timestamp)
                    .Select(e => e.Timestamp)
                    .FirstOrDefaultAsync()
            };
        }

        private async Task<int> GetCurrentVersionAsync(string aggregateId)
        {
            var lastEvent = await _dbContext.Set<EventEntity>()
                .Where(e => e.AggregateId == aggregateId)
                .OrderByDescending(e => e.Version)
                .FirstOrDefaultAsync();

            return lastEvent?.Version ?? 0;
        }

        private async Task CreateSnapshotAsync(string aggregateId, string aggregateType, int version)
        {
            try
            {
                var events = await GetEventsAsync(aggregateId);
                var aggregateRoot = AggregateFactory.CreateAggregate(aggregateType);
                aggregateRoot.LoadFromHistory(events);
                
                var snapshot = new Snapshot
                {
                    AggregateId = aggregateId,
                    AggregateType = aggregateType,
                    Data = JsonSerializer.Serialize(aggregateRoot),
                    Version = version,
                    Timestamp = DateTime.UtcNow
                };

                await SaveSnapshotAsync(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating snapshot for aggregate {AggregateId}", aggregateId);
            }
        }

        private async Task PublishEventsAsync<T>(List<T> events) where T : IEvent
        {
            foreach (var handler in _eventHandlers)
            {
                foreach (var @event in events)
                {
                    try
                    {
                        await handler.HandleAsync(@event);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing event {EventType}", @event.GetType().Name);
                    }
                }
            }
        }

        public void RegisterEventHandler(IEventHandler handler)
        {
            _eventHandlers.Add(handler);
        }
    }

    public interface IEvent
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
        string UserId { get; }
        string CorrelationId { get; }
        string CausationId { get; }
    }

    public abstract class EventBase : IEvent
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();
        public DateTime Timestamp { get; protected set; } = DateTime.UtcNow;
        public string UserId { get; set; }
        public string CorrelationId { get; set; }
        public string CausationId { get; set; }
    }

    public abstract class AggregateRoot
    {
        private readonly List<IEvent> _changes = new List<IEvent>();
        
        public string Id { get; protected set; }
        public int Version { get; protected set; }
        
        public IEnumerable<IEvent> GetUncommittedChanges()
        {
            return _changes;
        }

        public void MarkChangesAsCommitted()
        {
            _changes.Clear();
        }

        public void LoadFromHistory(IEnumerable<IEvent> history)
        {
            foreach (var @event in history)
            {
                ApplyChange(@event, false);
            }
        }

        protected void ApplyChange(IEvent @event)
        {
            ApplyChange(@event, true);
        }

        private void ApplyChange(IEvent @event, bool isNew)
        {
            var method = GetType().GetMethod("Apply", new[] { @event.GetType() });
            if (method != null)
            {
                method.Invoke(this, new[] { @event });
            }
            
            if (isNew)
            {
                _changes.Add(@event);
            }
            
            Version++;
        }

        public virtual void LoadFromSnapshot(Snapshot snapshot)
        {
            var data = JsonSerializer.Deserialize(snapshot.Data, GetType());
            foreach (var prop in GetType().GetProperties())
            {
                prop.SetValue(this, prop.GetValue(data));
            }
            Version = snapshot.Version;
        }

        public virtual Snapshot CreateSnapshot()
        {
            return new Snapshot
            {
                AggregateId = Id,
                AggregateType = GetType().Name,
                Data = JsonSerializer.Serialize(this),
                Version = Version,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public class EventEntity
    {
        public Guid Id { get; set; }
        public string AggregateId { get; set; }
        public string AggregateType { get; set; }
        public string EventType { get; set; }
        public string EventData { get; set; }
        public string Metadata { get; set; }
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SnapshotEntity
    {
        public Guid Id { get; set; }
        public string AggregateId { get; set; }
        public string AggregateType { get; set; }
        public string Data { get; set; }
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EventStream
    {
        public string AggregateId { get; set; }
        public List<IEvent> Events { get; set; }
        public int Version { get; set; }
    }

    public class Snapshot
    {
        public string AggregateId { get; set; }
        public string AggregateType { get; set; }
        public string Data { get; set; }
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class EventMetadata
    {
        public string UserId { get; set; }
        public string CorrelationId { get; set; }
        public string CausationId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class EventStoreStatistics
    {
        public int TotalEvents { get; set; }
        public int TotalSnapshots { get; set; }
        public int AggregateTypes { get; set; }
        public List<string> EventTypes { get; set; }
        public DateTime? LastEventTime { get; set; }
    }

    public interface IEventSerializer
    {
        string Serialize<T>(T @event);
        IEvent Deserialize(string data, string eventType);
    }

    public class JsonEventSerializer : IEventSerializer
    {
        public string Serialize<T>(T @event)
        {
            return JsonSerializer.Serialize(@event);
        }

        public IEvent Deserialize(string data, string eventType)
        {
            var type = Type.GetType(eventType) ?? typeof(EventBase);
            return (IEvent)JsonSerializer.Deserialize(data, type);
        }
    }

    public interface IEventHandler
    {
        Task HandleAsync(IEvent @event);
    }

    public interface ISnapshotStrategy
    {
        bool ShouldTakeSnapshot(string aggregateId, int version);
    }

    public class DefaultSnapshotStrategy : ISnapshotStrategy
    {
        private readonly int _snapshotFrequency;

        public DefaultSnapshotStrategy(int snapshotFrequency = 10)
        {
            _snapshotFrequency = snapshotFrequency;
        }

        public bool ShouldTakeSnapshot(string aggregateId, int version)
        {
            return version % _snapshotFrequency == 0;
        }
    }

    public static class AggregateFactory
    {
        public static AggregateRoot CreateAggregate(string aggregateType)
        {
            var type = Type.GetType(aggregateType);
            if (type == null)
                throw new InvalidOperationException($"Aggregate type {aggregateType} not found");
                
            return (AggregateRoot)Activator.CreateInstance(type);
        }
    }

    public class EventStoreConcurrencyException : Exception
    {
        public EventStoreConcurrencyException(string message) : base(message) { }
    }
}