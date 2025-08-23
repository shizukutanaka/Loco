using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Database
{
    /// <summary>
    /// Simple and efficient database migration system
    /// Following Robert C. Martin's clean architecture principles
    /// </summary>
    public interface IMigrationService
    {
        Task<bool> InitializeAsync(string connectionString);
        Task<MigrationResult> MigrateAsync(string targetVersion = null);
        Task<MigrationResult> RollbackAsync(string targetVersion);
        Task<List<MigrationInfo>> GetAppliedMigrationsAsync();
        Task<List<MigrationInfo>> GetPendingMigrationsAsync();
        Task<DatabaseInfo> GetDatabaseInfoAsync();
    }

    public class MigrationService : IMigrationService
    {
        private readonly ILogger<MigrationService> _logger;
        private readonly List<IMigration> _migrations;
        private string _connectionString;
        private readonly object _lock = new object();

        public MigrationService(ILogger<MigrationService> logger)
        {
            _logger = logger;
            _migrations = new List<IMigration>();
            LoadMigrations();
        }

        public async Task<bool> InitializeAsync(string connectionString)
        {
            _connectionString = connectionString;
            
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Create migrations table if it doesn't exist
                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS __migrations (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        version TEXT NOT NULL UNIQUE,
                        name TEXT NOT NULL,
                        applied_at DATETIME NOT NULL,
                        checksum TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS idx_migrations_version ON __migrations(version);";
                
                using var command = new SqliteCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Migration system initialized");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize migration system");
                return false;
            }
        }

        public async Task<MigrationResult> MigrateAsync(string targetVersion = null)
        {
            var result = new MigrationResult { Success = true };
            
            try
            {
                var pendingMigrations = await GetPendingMigrationsAsync();
                
                if (targetVersion != null)
                {
                    pendingMigrations = pendingMigrations
                        .Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) <= 0)
                        .ToList();
                }
                
                if (!pendingMigrations.Any())
                {
                    result.Message = "No pending migrations";
                    return result;
                }
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    foreach (var migrationInfo in pendingMigrations)
                    {
                        var migration = _migrations.FirstOrDefault(m => m.Version == migrationInfo.Version);
                        if (migration == null)
                        {
                            throw new InvalidOperationException($"Migration {migrationInfo.Version} not found");
                        }
                        
                        _logger.LogInformation("Applying migration {Version}: {Name}", 
                            migration.Version, migration.Name);
                        
                        // Execute migration
                        await migration.UpAsync(connection, transaction);
                        
                        // Record migration
                        var insertSql = @"
                            INSERT INTO __migrations (version, name, applied_at, checksum)
                            VALUES (@version, @name, @appliedAt, @checksum)";
                        
                        using var insertCommand = new SqliteCommand(insertSql, connection, transaction);
                        insertCommand.Parameters.AddWithValue("@version", migration.Version);
                        insertCommand.Parameters.AddWithValue("@name", migration.Name);
                        insertCommand.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow);
                        insertCommand.Parameters.AddWithValue("@checksum", migration.GetChecksum());
                        await insertCommand.ExecuteNonQueryAsync();
                        
                        result.AppliedMigrations.Add(migrationInfo);
                    }
                    
                    await transaction.CommitAsync();
                    result.Message = $"Successfully applied {result.AppliedMigrations.Count} migration(s)";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new MigrationException($"Migration failed: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed");
                result.Success = false;
                result.Message = ex.Message;
                result.Error = ex;
            }
            
            return result;
        }

        public async Task<MigrationResult> RollbackAsync(string targetVersion)
        {
            var result = new MigrationResult { Success = true };
            
            try
            {
                var appliedMigrations = await GetAppliedMigrationsAsync();
                var migrationsToRollback = appliedMigrations
                    .Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) > 0)
                    .OrderByDescending(m => m.Version)
                    .ToList();
                
                if (!migrationsToRollback.Any())
                {
                    result.Message = "No migrations to rollback";
                    return result;
                }
                
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    foreach (var migrationInfo in migrationsToRollback)
                    {
                        var migration = _migrations.FirstOrDefault(m => m.Version == migrationInfo.Version);
                        if (migration == null)
                        {
                            throw new InvalidOperationException($"Migration {migrationInfo.Version} not found");
                        }
                        
                        _logger.LogInformation("Rolling back migration {Version}: {Name}", 
                            migration.Version, migration.Name);
                        
                        // Execute rollback
                        await migration.DownAsync(connection, transaction);
                        
                        // Remove migration record
                        var deleteSql = "DELETE FROM __migrations WHERE version = @version";
                        using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@version", migration.Version);
                        await deleteCommand.ExecuteNonQueryAsync();
                        
                        result.RolledBackMigrations.Add(migrationInfo);
                    }
                    
                    await transaction.CommitAsync();
                    result.Message = $"Successfully rolled back {result.RolledBackMigrations.Count} migration(s)";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new MigrationException($"Rollback failed: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed");
                result.Success = false;
                result.Message = ex.Message;
                result.Error = ex;
            }
            
            return result;
        }

        public async Task<List<MigrationInfo>> GetAppliedMigrationsAsync()
        {
            var migrations = new List<MigrationInfo>();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var sql = "SELECT version, name, applied_at, checksum FROM __migrations ORDER BY version";
            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                migrations.Add(new MigrationInfo
                {
                    Version = reader.GetString(0),
                    Name = reader.GetString(1),
                    AppliedAt = reader.GetDateTime(2),
                    Checksum = reader.GetString(3),
                    Status = MigrationStatus.Applied
                });
            }
            
            return migrations;
        }

        public async Task<List<MigrationInfo>> GetPendingMigrationsAsync()
        {
            var appliedMigrations = await GetAppliedMigrationsAsync();
            var appliedVersions = new HashSet<string>(appliedMigrations.Select(m => m.Version));
            
            return _migrations
                .Where(m => !appliedVersions.Contains(m.Version))
                .OrderBy(m => m.Version)
                .Select(m => new MigrationInfo
                {
                    Version = m.Version,
                    Name = m.Name,
                    Status = MigrationStatus.Pending,
                    Checksum = m.GetChecksum()
                })
                .ToList();
        }

        public async Task<DatabaseInfo> GetDatabaseInfoAsync()
        {
            var info = new DatabaseInfo();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            // Get current version
            var versionSql = "SELECT MAX(version) FROM __migrations";
            using var versionCommand = new SqliteCommand(versionSql, connection);
            var currentVersion = await versionCommand.ExecuteScalarAsync() as string;
            info.CurrentVersion = currentVersion ?? "0.0.0";
            
            // Get table count
            var tableCountSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            using var tableCountCommand = new SqliteCommand(tableCountSql, connection);
            info.TableCount = Convert.ToInt32(await tableCountCommand.ExecuteScalarAsync());
            
            // Get database size
            var pragmaSql = "PRAGMA page_count; PRAGMA page_size;";
            using var pragmaCommand = new SqliteCommand(pragmaSql, connection);
            using var reader = await pragmaCommand.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var pageCount = reader.GetInt64(0);
                await reader.NextResultAsync();
                if (await reader.ReadAsync())
                {
                    var pageSize = reader.GetInt64(0);
                    info.SizeInBytes = pageCount * pageSize;
                }
            }
            
            info.AppliedMigrations = await GetAppliedMigrationsAsync();
            info.PendingMigrations = await GetPendingMigrationsAsync();
            
            return info;
        }

        private void LoadMigrations()
        {
            // Load built-in migrations
            _migrations.Add(new InitialMigration());
            _migrations.Add(new AddIndicesMigration());
            _migrations.Add(new AddMetadataTableMigration());
            
            // Load migrations from assembly
            var migrationTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
            
            foreach (var type in migrationTypes)
            {
                if (Activator.CreateInstance(type) is IMigration migration)
                {
                    if (!_migrations.Any(m => m.Version == migration.Version))
                    {
                        _migrations.Add(migration);
                    }
                }
            }
            
            // Sort by version
            _migrations.Sort((a, b) => string.Compare(a.Version, b.Version, StringComparison.Ordinal));
            
            _logger.LogInformation("Loaded {Count} migrations", _migrations.Count);
        }
    }

    /// <summary>
    /// Base interface for database migrations
    /// </summary>
    public interface IMigration
    {
        string Version { get; }
        string Name { get; }
        Task UpAsync(IDbConnection connection, IDbTransaction transaction);
        Task DownAsync(IDbConnection connection, IDbTransaction transaction);
        string GetChecksum();
    }

    /// <summary>
    /// Base class for migrations
    /// </summary>
    public abstract class Migration : IMigration
    {
        public abstract string Version { get; }
        public abstract string Name { get; }
        
        public abstract Task UpAsync(IDbConnection connection, IDbTransaction transaction);
        public abstract Task DownAsync(IDbConnection connection, IDbTransaction transaction);
        
        public virtual string GetChecksum()
        {
            var content = $"{Version}:{Name}:{GetType().FullName}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
        
        protected async Task ExecuteSqlAsync(IDbConnection connection, IDbTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            await ((DbCommand)command).ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Initial database schema migration
    /// </summary>
    public class InitialMigration : Migration
    {
        public override string Version => "1.0.0";
        public override string Name => "Initial Schema";
        
        public override async Task UpAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS rules (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    priority INTEGER NOT NULL DEFAULT 0,
                    trigger_type TEXT NOT NULL,
                    trigger_config TEXT,
                    conditions TEXT,
                    actions TEXT NOT NULL,
                    metadata TEXT,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL
                );
                
                CREATE TABLE IF NOT EXISTS flows (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT,
                    definition TEXT NOT NULL,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL
                );
                
                CREATE TABLE IF NOT EXISTS execution_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    entity_type TEXT NOT NULL,
                    entity_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    started_at DATETIME NOT NULL,
                    completed_at DATETIME,
                    duration_ms INTEGER,
                    error_message TEXT,
                    context TEXT
                );";
            
            await ExecuteSqlAsync(connection, transaction, sql);
        }
        
        public override async Task DownAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = @"
                DROP TABLE IF EXISTS execution_logs;
                DROP TABLE IF EXISTS flows;
                DROP TABLE IF EXISTS rules;";
            
            await ExecuteSqlAsync(connection, transaction, sql);
        }
    }

    /// <summary>
    /// Add performance indices
    /// </summary>
    public class AddIndicesMigration : Migration
    {
        public override string Version => "1.1.0";
        public override string Name => "Add Performance Indices";
        
        public override async Task UpAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = @"
                CREATE INDEX IF NOT EXISTS idx_rules_enabled ON rules(enabled);
                CREATE INDEX IF NOT EXISTS idx_rules_priority ON rules(priority);
                CREATE INDEX IF NOT EXISTS idx_flows_enabled ON flows(enabled);
                CREATE INDEX IF NOT EXISTS idx_logs_entity ON execution_logs(entity_type, entity_id);
                CREATE INDEX IF NOT EXISTS idx_logs_started ON execution_logs(started_at);";
            
            await ExecuteSqlAsync(connection, transaction, sql);
        }
        
        public override async Task DownAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = @"
                DROP INDEX IF EXISTS idx_rules_enabled;
                DROP INDEX IF EXISTS idx_rules_priority;
                DROP INDEX IF EXISTS idx_flows_enabled;
                DROP INDEX IF EXISTS idx_logs_entity;
                DROP INDEX IF EXISTS idx_logs_started;";
            
            await ExecuteSqlAsync(connection, transaction, sql);
        }
    }

    /// <summary>
    /// Add metadata table for system configuration
    /// </summary>
    public class AddMetadataTableMigration : Migration
    {
        public override string Version => "1.2.0";
        public override string Name => "Add Metadata Table";
        
        public override async Task UpAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS system_metadata (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at DATETIME NOT NULL
                );
                
                INSERT OR IGNORE INTO system_metadata (key, value, updated_at)
                VALUES ('schema_version', '1.2.0', datetime('now'));";
            
            await ExecuteSqlAsync(connection, transaction, sql);
        }
        
        public override async Task DownAsync(IDbConnection connection, IDbTransaction transaction)
        {
            var sql = "DROP TABLE IF EXISTS system_metadata;";
            await ExecuteSqlAsync(connection, transaction, sql);
        }
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<MigrationInfo> AppliedMigrations { get; set; } = new();
        public List<MigrationInfo> RolledBackMigrations { get; set; } = new();
        public Exception Error { get; set; }
    }

    public class MigrationInfo
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public DateTime? AppliedAt { get; set; }
        public string Checksum { get; set; }
        public MigrationStatus Status { get; set; }
    }

    public enum MigrationStatus
    {
        Pending,
        Applied,
        Failed
    }

    public class DatabaseInfo
    {
        public string CurrentVersion { get; set; }
        public int TableCount { get; set; }
        public long SizeInBytes { get; set; }
        public List<MigrationInfo> AppliedMigrations { get; set; } = new();
        public List<MigrationInfo> PendingMigrations { get; set; } = new();
    }

    public class MigrationException : Exception
    {
        public MigrationException(string message) : base(message) { }
        public MigrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
