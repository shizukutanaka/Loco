using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Database
{
    public interface IMigrationSystem
    {
        Task<MigrationResult> MigrateAsync(string connectionString, DatabaseProvider provider);
        Task<MigrationResult> RollbackAsync(string connectionString, DatabaseProvider provider, int steps = 1);
        Task<List<AppliedMigration>> GetAppliedMigrationsAsync(string connectionString, DatabaseProvider provider);
        Task<List<PendingMigration>> GetPendingMigrationsAsync(string connectionString, DatabaseProvider provider);
        void RegisterMigration(IMigration migration);
    }

    public class MigrationSystem : IMigrationSystem
    {
        private readonly ILogger<MigrationSystem> _logger;
        private readonly List<IMigration> _migrations = new();
        private readonly object _lock = new();

        public MigrationSystem(ILogger<MigrationSystem> logger)
        {
            _logger = logger;
            LoadMigrationsFromAssembly();
        }

        public void RegisterMigration(IMigration migration)
        {
            lock (_lock)
            {
                _migrations.Add(migration);
                _migrations.Sort((a, b) => a.Version.CompareTo(b.Version));
            }
        }

        public async Task<MigrationResult> MigrateAsync(string connectionString, DatabaseProvider provider)
        {
            var result = new MigrationResult { StartTime = DateTime.UtcNow };

            try
            {
                using var connection = CreateConnection(connectionString, provider);
                await connection.OpenAsync();

                // Ensure migration table exists
                await EnsureMigrationTableAsync(connection, provider);

                // Get applied migrations
                var applied = await GetAppliedMigrationsInternalAsync(connection, provider);
                var appliedVersions = new HashSet<long>(applied.Select(m => m.Version));

                // Get pending migrations
                var pending = _migrations
                    .Where(m => !appliedVersions.Contains(m.Version))
                    .OrderBy(m => m.Version)
                    .ToList();

                if (!pending.Any())
                {
                    result.Message = "No pending migrations";
                    result.Success = true;
                    return result;
                }

                // Apply migrations
                foreach (var migration in pending)
                {
                    var migrationResult = await ApplyMigrationAsync(connection, provider, migration);
                    result.AppliedMigrations.Add(migrationResult);

                    if (!migrationResult.Success)
                    {
                        result.Success = false;
                        result.Message = $"Migration {migration.Version} failed: {migrationResult.Error}";
                        break;
                    }
                }

                if (result.Success)
                {
                    result.Message = $"Successfully applied {result.AppliedMigrations.Count} migrations";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed");
                result.Success = false;
                result.Message = ex.Message;
                result.Error = ex.ToString();
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        public async Task<MigrationResult> RollbackAsync(string connectionString, DatabaseProvider provider, int steps = 1)
        {
            var result = new MigrationResult { StartTime = DateTime.UtcNow };

            try
            {
                using var connection = CreateConnection(connectionString, provider);
                await connection.OpenAsync();

                // Get applied migrations
                var applied = await GetAppliedMigrationsInternalAsync(connection, provider);
                var toRollback = applied.OrderByDescending(m => m.Version).Take(steps).ToList();

                if (!toRollback.Any())
                {
                    result.Message = "No migrations to rollback";
                    result.Success = true;
                    return result;
                }

                // Rollback migrations
                foreach (var appliedMigration in toRollback)
                {
                    var migration = _migrations.FirstOrDefault(m => m.Version == appliedMigration.Version);
                    if (migration == null)
                    {
                        result.Success = false;
                        result.Message = $"Migration {appliedMigration.Version} not found in registered migrations";
                        break;
                    }

                    var rollbackResult = await RollbackMigrationAsync(connection, provider, migration);
                    result.RolledBackMigrations.Add(rollbackResult);

                    if (!rollbackResult.Success)
                    {
                        result.Success = false;
                        result.Message = $"Rollback of migration {migration.Version} failed: {rollbackResult.Error}";
                        break;
                    }
                }

                if (result.Success)
                {
                    result.Message = $"Successfully rolled back {result.RolledBackMigrations.Count} migrations";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rollback failed");
                result.Success = false;
                result.Message = ex.Message;
                result.Error = ex.ToString();
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        public async Task<List<AppliedMigration>> GetAppliedMigrationsAsync(string connectionString, DatabaseProvider provider)
        {
            using var connection = CreateConnection(connectionString, provider);
            await connection.OpenAsync();
            await EnsureMigrationTableAsync(connection, provider);
            return await GetAppliedMigrationsInternalAsync(connection, provider);
        }

        public async Task<List<PendingMigration>> GetPendingMigrationsAsync(string connectionString, DatabaseProvider provider)
        {
            var applied = await GetAppliedMigrationsAsync(connectionString, provider);
            var appliedVersions = new HashSet<long>(applied.Select(m => m.Version));

            return _migrations
                .Where(m => !appliedVersions.Contains(m.Version))
                .OrderBy(m => m.Version)
                .Select(m => new PendingMigration
                {
                    Version = m.Version,
                    Name = m.Name,
                    Description = m.Description
                })
                .ToList();
        }

        private async Task<MigrationExecutionResult> ApplyMigrationAsync(DbConnection connection, DatabaseProvider provider, IMigration migration)
        {
            var result = new MigrationExecutionResult
            {
                Version = migration.Version,
                Name = migration.Name,
                StartTime = DateTime.UtcNow
            };

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Applying migration {Version}: {Name}", migration.Version, migration.Name);

                // Execute migration
                await migration.UpAsync(connection, transaction, provider);

                // Record migration
                await RecordMigrationAsync(connection, transaction, provider, migration);

                await transaction.CommitAsync();

                result.Success = true;
                _logger.LogInformation("Successfully applied migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to apply migration {Version}", migration.Version);
                result.Success = false;
                result.Error = ex.Message;
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        private async Task<MigrationExecutionResult> RollbackMigrationAsync(DbConnection connection, DatabaseProvider provider, IMigration migration)
        {
            var result = new MigrationExecutionResult
            {
                Version = migration.Version,
                Name = migration.Name,
                StartTime = DateTime.UtcNow
            };

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Rolling back migration {Version}: {Name}", migration.Version, migration.Name);

                // Execute rollback
                await migration.DownAsync(connection, transaction, provider);

                // Remove migration record
                await RemoveMigrationRecordAsync(connection, transaction, provider, migration.Version);

                await transaction.CommitAsync();

                result.Success = true;
                _logger.LogInformation("Successfully rolled back migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to rollback migration {Version}", migration.Version);
                result.Success = false;
                result.Error = ex.Message;
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            return result;
        }

        private async Task EnsureMigrationTableAsync(DbConnection connection, DatabaseProvider provider)
        {
            var sql = provider switch
            {
                DatabaseProvider.SqlServer => @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__MigrationHistory')
                    CREATE TABLE __MigrationHistory (
                        Version BIGINT PRIMARY KEY,
                        Name NVARCHAR(255) NOT NULL,
                        Description NVARCHAR(MAX),
                        Checksum NVARCHAR(64),
                        AppliedAt DATETIME2 NOT NULL,
                        ExecutionTime INT
                    )",

                DatabaseProvider.PostgreSQL => @"
                    CREATE TABLE IF NOT EXISTS __MigrationHistory (
                        Version BIGINT PRIMARY KEY,
                        Name VARCHAR(255) NOT NULL,
                        Description TEXT,
                        Checksum VARCHAR(64),
                        AppliedAt TIMESTAMP NOT NULL,
                        ExecutionTime INT
                    )",

                DatabaseProvider.SQLite => @"
                    CREATE TABLE IF NOT EXISTS __MigrationHistory (
                        Version INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        Checksum TEXT,
                        AppliedAt TEXT NOT NULL,
                        ExecutionTime INTEGER
                    )",

                DatabaseProvider.MySQL => @"
                    CREATE TABLE IF NOT EXISTS __MigrationHistory (
                        Version BIGINT PRIMARY KEY,
                        Name VARCHAR(255) NOT NULL,
                        Description TEXT,
                        Checksum VARCHAR(64),
                        AppliedAt DATETIME NOT NULL,
                        ExecutionTime INT
                    )",

                _ => throw new NotSupportedException($"Database provider {provider} is not supported")
            };

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        private async Task<List<AppliedMigration>> GetAppliedMigrationsInternalAsync(DbConnection connection, DatabaseProvider provider)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Version, Name, Description, Checksum, AppliedAt, ExecutionTime FROM __MigrationHistory ORDER BY Version";

            var result = new List<AppliedMigration>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AppliedMigration
                {
                    Version = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Checksum = reader.IsDBNull(3) ? null : reader.GetString(3),
                    AppliedAt = reader.GetDateTime(5),
                    ExecutionTime = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                });
            }

            return result;
        }

        private async Task RecordMigrationAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider, IMigration migration)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO __MigrationHistory (Version, Name, Description, Checksum, AppliedAt, ExecutionTime)
                VALUES (@Version, @Name, @Description, @Checksum, @AppliedAt, @ExecutionTime)";

            AddParameter(command, "@Version", migration.Version);
            AddParameter(command, "@Name", migration.Name);
            AddParameter(command, "@Description", migration.Description ?? (object)DBNull.Value);
            AddParameter(command, "@Checksum", ComputeChecksum(migration));
            AddParameter(command, "@AppliedAt", DateTime.UtcNow);
            AddParameter(command, "@ExecutionTime", DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        private async Task RemoveMigrationRecordAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider, long version)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM __MigrationHistory WHERE Version = @Version";
            AddParameter(command, "@Version", version);
            await command.ExecuteNonQueryAsync();
        }

        private DbConnection CreateConnection(string connectionString, DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
                DatabaseProvider.PostgreSQL => new Npgsql.NpgsqlConnection(connectionString),
                DatabaseProvider.SQLite => new Microsoft.Data.Sqlite.SqliteConnection(connectionString),
                DatabaseProvider.MySQL => new MySqlConnector.MySqlConnection(connectionString),
                _ => throw new NotSupportedException($"Database provider {provider} is not supported")
            };
        }

        private void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private string ComputeChecksum(IMigration migration)
        {
            var content = $"{migration.Version}:{migration.Name}:{migration.Description}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private void LoadMigrationsFromAssembly()
        {
            var migrationTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();

            foreach (var type in migrationTypes)
            {
                if (Activator.CreateInstance(type) is IMigration migration)
                {
                    RegisterMigration(migration);
                }
            }

            _logger.LogInformation("Loaded {Count} migrations from assembly", _migrations.Count);
        }
    }

    // Migration interfaces and models
    public interface IMigration
    {
        long Version { get; }
        string Name { get; }
        string Description { get; }
        Task UpAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider);
        Task DownAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider);
    }

    public abstract class Migration : IMigration
    {
        public abstract long Version { get; }
        public abstract string Name { get; }
        public virtual string Description => null;

        public abstract Task UpAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider);
        public abstract Task DownAsync(DbConnection connection, DbTransaction transaction, DatabaseProvider provider);

        protected async Task ExecuteSqlAsync(DbConnection connection, DbTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    public enum DatabaseProvider
    {
        SqlServer,
        PostgreSQL,
        MySQL,
        SQLite
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<MigrationExecutionResult> AppliedMigrations { get; set; } = new();
        public List<MigrationExecutionResult> RolledBackMigrations { get; set; } = new();
    }

    public class MigrationExecutionResult
    {
        public long Version { get; set; }
        public string Name { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }

    public class AppliedMigration
    {
        public long Version { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Checksum { get; set; }
        public DateTime AppliedAt { get; set; }
        public int? ExecutionTime { get; set; }
    }

    public class PendingMigration
    {
        public long Version { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}