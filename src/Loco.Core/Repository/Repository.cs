using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Repository
{
    /// <summary>
    /// Generic repository pattern implementation
    /// Following Robert C. Martin's clean architecture
    /// </summary>
    public interface IRepository<T> where T : class, IEntity
    {
        Task<T> GetByIdAsync(string id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T> AddAsync(T entity);
        Task<bool> UpdateAsync(T entity);
        Task<bool> DeleteAsync(string id);
        Task<int> CountAsync();
        Task<bool> ExistsAsync(string id);
        Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, string orderBy = null);
    }

    public interface IEntity
    {
        string Id { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
    }

    public abstract class BaseEntity : IEntity
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class SqliteRepository<T> : IRepository<T> where T : class, IEntity, new()
    {
        protected readonly string _connectionString;
        protected readonly ILogger<SqliteRepository<T>> _logger;
        protected readonly string _tableName;

        public SqliteRepository(string connectionString, ILogger<SqliteRepository<T>> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            _tableName = GetTableName();
            
            EnsureTableExists().GetAwaiter().GetResult();
        }

        public async Task<T> GetByIdAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $"SELECT data FROM {_tableName} WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            var result = await command.ExecuteScalarAsync();
            if (result != null)
            {
                return JsonSerializer.Deserialize<T>(result.ToString());
            }

            return null;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $"SELECT data FROM {_tableName} ORDER BY updated_at DESC";
            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            var entities = new List<T>();
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                entities.Add(JsonSerializer.Deserialize<T>(json));
            }

            return entities;
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            // For complex queries, fetch all and filter in memory
            // In production, consider using a query builder or ORM
            var allEntities = await GetAllAsync();
            var compiledPredicate = predicate.Compile();
            return allEntities.Where(compiledPredicate);
        }

        public async Task<T> AddAsync(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $@"
                INSERT INTO {_tableName} (id, data, created_at, updated_at)
                VALUES (@id, @data, @createdAt, @updatedAt)";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", entity.Id);
            command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(entity));
            command.Parameters.AddWithValue("@createdAt", entity.CreatedAt);
            command.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt);

            await command.ExecuteNonQueryAsync();
            
            _logger.LogDebug("Added entity {EntityId} to {TableName}", entity.Id, _tableName);
            
            return entity;
        }

        public async Task<bool> UpdateAsync(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            entity.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $@"
                UPDATE {_tableName}
                SET data = @data, updated_at = @updatedAt
                WHERE id = @id";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", entity.Id);
            command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(entity));
            command.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            _logger.LogDebug("Updated entity {EntityId} in {TableName}", entity.Id, _tableName);
            
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $"DELETE FROM {_tableName} WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            _logger.LogDebug("Deleted entity {EntityId} from {TableName}", id, _tableName);
            
            return rowsAffected > 0;
        }

        public async Task<int> CountAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $"SELECT COUNT(*) FROM {_tableName}";
            using var command = new SqliteCommand(sql, connection);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<bool> ExistsAsync(string id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $"SELECT COUNT(*) FROM {_tableName} WHERE id = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, string orderBy = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            
            var offset = (page - 1) * pageSize;
            orderBy = string.IsNullOrEmpty(orderBy) ? "updated_at DESC" : orderBy;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $@"
                SELECT data FROM {_tableName}
                ORDER BY {orderBy}
                LIMIT @limit OFFSET @offset";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@limit", pageSize);
            command.Parameters.AddWithValue("@offset", offset);

            using var reader = await command.ExecuteReaderAsync();
            var entities = new List<T>();
            
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                entities.Add(JsonSerializer.Deserialize<T>(json));
            }

            return entities;
        }

        protected virtual string GetTableName()
        {
            return typeof(T).Name.ToLower() + "s";
        }

        protected async Task EnsureTableExists()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = $@"
                CREATE TABLE IF NOT EXISTS {_tableName} (
                    id TEXT PRIMARY KEY,
                    data TEXT NOT NULL,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_{_tableName}_created ON {_tableName}(created_at);
                CREATE INDEX IF NOT EXISTS idx_{_tableName}_updated ON {_tableName}(updated_at);";

            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Settings repository specializing IRepository for key/value JSON settings.
    /// </summary>
    public interface ISettingsRepository : IRepository<Setting>
    {
        Task<Setting> GetOrCreateAsync(string key);
    }

    public class Setting : BaseEntity
    {
        public string Key { get; set; }
        public JsonElement Value { get; set; }
    }

    public class SqliteSettingsRepository : SqliteRepository<Setting>, ISettingsRepository
    {
        public SqliteSettingsRepository(string connectionString, ILogger<SqliteSettingsRepository> logger)
            : base(connectionString, logger)
        {
        }

        protected override string GetTableName()
        {
            return "settings";
        }

        public async Task<Setting> GetOrCreateAsync(string key)
        {
            var existing = (await FindAsync(s => s.Key == key)).FirstOrDefault();
            if (existing != null)
            {
                return existing;
            }

            var setting = new Setting
            {
                Key = key,
                Value = System.Text.Json.JsonDocument.Parse("{}").RootElement
            };
            return await AddAsync(setting);
        }
    }

    /// <summary>
    /// Unit of Work pattern for transaction management
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Rule> Rules { get; }
        IRepository<Flow> Flows { get; }
        IRepository<ExecutionLog> ExecutionLogs { get; }
        ISettingsRepository Settings { get; }
        Task<int> SaveChangesAsync();
        Task CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly string _connectionString;
        private readonly ILoggerFactory _loggerFactory;
        private SqliteConnection _connection;
        private SqliteTransaction _transaction;

        private IRepository<Rule> _rules;
        private IRepository<Flow> _flows;
        private IRepository<ExecutionLog> _executionLogs;
        private ISettingsRepository _settings;

        public UnitOfWork(string connectionString, ILoggerFactory loggerFactory)
        {
            _connectionString = connectionString;
            _loggerFactory = loggerFactory;
        }

        public IRepository<Rule> Rules =>
            _rules ??= new SqliteRepository<Rule>(_connectionString,
                _loggerFactory.CreateLogger<SqliteRepository<Rule>>());

        public IRepository<Flow> Flows =>
            _flows ??= new SqliteRepository<Flow>(_connectionString,
                _loggerFactory.CreateLogger<SqliteRepository<Flow>>());

        public IRepository<ExecutionLog> ExecutionLogs =>
            _executionLogs ??= new SqliteRepository<ExecutionLog>(_connectionString,
                _loggerFactory.CreateLogger<SqliteRepository<ExecutionLog>>());

        public ISettingsRepository Settings =>
            _settings ??= new SqliteSettingsRepository(_connectionString,
                _loggerFactory.CreateLogger<SqliteSettingsRepository>());

        public async Task BeginTransactionAsync()
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
            _transaction = _connection.BeginTransaction();
        }

        public async Task CommitAsync()
        {
            try
            {
                await _transaction?.CommitAsync();
            }
            finally
            {
                Cleanup();
            }
        }

        public async Task RollbackAsync()
        {
            try
            {
                await _transaction?.RollbackAsync();
            }
            finally
            {
                Cleanup();
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            // Implementation would track changes and persist them
            return await Task.FromResult(0);
        }

        public Task CompleteAsync()
        {
            return SaveChangesAsync();
        }

        private void Cleanup()
        {
            _transaction?.Dispose();
            _transaction = null;
            _connection?.Dispose();
            _connection = null;
        }

        public void Dispose()
        {
            Cleanup();
        }
    }

    // Entity classes
    public class Rule : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public int Priority { get; set; }
        public string TriggerType { get; set; }
        public string TriggerConfig { get; set; }
        public string Conditions { get; set; }
        public string Actions { get; set; }
        public string Metadata { get; set; }
    }

    public class Flow : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Definition { get; set; }
        public bool Enabled { get; set; }
    }

    public class ExecutionLog : BaseEntity
    {
        public string EntityType { get; set; }
        public string EntityId { get; set; }
        public string Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMs { get; set; }
        public string ErrorMessage { get; set; }
        public string Context { get; set; }
    }

    /// <summary>
    /// Specification pattern for complex queries
    /// </summary>
    public interface ISpecification<T>
    {
        Expression<Func<T, bool>> Criteria { get; }
        List<Expression<Func<T, object>>> Includes { get; }
        List<string> IncludeStrings { get; }
        Expression<Func<T, object>> OrderBy { get; }
        Expression<Func<T, object>> OrderByDescending { get; }
        int Take { get; }
        int Skip { get; }
        bool IsPagingEnabled { get; }
    }

    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        public Expression<Func<T, bool>> Criteria { get; private set; }
        public List<Expression<Func<T, object>>> Includes { get; } = new();
        public List<string> IncludeStrings { get; } = new();
        public Expression<Func<T, object>> OrderBy { get; private set; }
        public Expression<Func<T, object>> OrderByDescending { get; private set; }
        public int Take { get; private set; }
        public int Skip { get; private set; }
        public bool IsPagingEnabled { get; private set; }

        protected BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        protected BaseSpecification()
        {
        }

        protected void AddCriteria(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        protected void AddInclude(Expression<Func<T, object>> includeExpression)
        {
            Includes.Add(includeExpression);
        }

        protected void AddInclude(string includeString)
        {
            IncludeStrings.Add(includeString);
        }

        protected void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }

        protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
        {
            OrderBy = orderByExpression;
        }

        protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
        {
            OrderByDescending = orderByDescExpression;
        }
    }

    // Example specification
    public class EnabledRulesSpecification : BaseSpecification<Rule>
    {
        public EnabledRulesSpecification() : base(r => r.Enabled)
        {
            ApplyOrderBy(r => r.Priority);
        }
    }

    public class RecentExecutionLogsSpecification : BaseSpecification<ExecutionLog>
    {
        public RecentExecutionLogsSpecification(int days = 7) 
            : base(log => log.StartedAt > DateTime.UtcNow.AddDays(-days))
        {
            ApplyOrderByDescending(log => log.StartedAt);
            ApplyPaging(0, 100);
        }
    }
}
