using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Loco.Integration.Tests
{
    public class DatabaseIntegrationTests : IAsyncLifetime
    {
        private MsSqlContainer _mssqlContainer;
        private PostgreSqlContainer _postgresContainer;

        public async Task InitializeAsync()
        {
            // Start SQL Server container
            _mssqlContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            
            await _mssqlContainer.StartAsync();

            // Start PostgreSQL container
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15-alpine")
                .Build();
            
            await _postgresContainer.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _mssqlContainer.DisposeAsync();
            await _postgresContainer.DisposeAsync();
        }

        [Fact]
        public async Task SqlServer_Connection_ShouldWork()
        {
            // Arrange
            var connectionString = _mssqlContainer.GetConnectionString();

            // Act & Assert
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT @@VERSION";
            var version = await command.ExecuteScalarAsync();
            
            version.Should().NotBeNull();
            version.ToString().Should().Contain("Microsoft SQL Server");
        }

        [Fact]
        public async Task PostgreSQL_Connection_ShouldWork()
        {
            // Arrange
            var connectionString = _postgresContainer.GetConnectionString();

            // Act & Assert
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT version()";
            var version = await command.ExecuteScalarAsync();
            
            version.Should().NotBeNull();
            version.ToString().Should().Contain("PostgreSQL");
        }

        [Fact]
        public async Task SqlServer_CRUD_Operations()
        {
            // Arrange
            var connectionString = _mssqlContainer.GetConnectionString();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Create table
            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = @"
                    CREATE TABLE TestTable (
                        Id INT PRIMARY KEY IDENTITY,
                        Name NVARCHAR(100),
                        CreatedAt DATETIME2
                    )";
                await createCmd.ExecuteNonQueryAsync();
            }

            // Insert
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.CommandText = @"
                    INSERT INTO TestTable (Name, CreatedAt) 
                    VALUES (@name, @createdAt);
                    SELECT SCOPE_IDENTITY();";
                insertCmd.Parameters.AddWithValue("@name", "Test Item");
                insertCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                
                var id = await insertCmd.ExecuteScalarAsync();
                id.Should().NotBeNull();
            }

            // Select
            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT COUNT(*) FROM TestTable";
                var count = await selectCmd.ExecuteScalarAsync();
                count.Should().Be(1);
            }

            // Update
            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.CommandText = "UPDATE TestTable SET Name = @name WHERE Id = 1";
                updateCmd.Parameters.AddWithValue("@name", "Updated Item");
                var affected = await updateCmd.ExecuteNonQueryAsync();
                affected.Should().Be(1);
            }

            // Delete
            using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.CommandText = "DELETE FROM TestTable WHERE Id = 1";
                var affected = await deleteCmd.ExecuteNonQueryAsync();
                affected.Should().Be(1);
            }
        }

        [Fact]
        public async Task PostgreSQL_CRUD_Operations()
        {
            // Arrange
            var connectionString = _postgresContainer.GetConnectionString();
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Create table
            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = @"
                    CREATE TABLE test_table (
                        id SERIAL PRIMARY KEY,
                        name VARCHAR(100),
                        created_at TIMESTAMP
                    )";
                await createCmd.ExecuteNonQueryAsync();
            }

            // Insert
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.CommandText = @"
                    INSERT INTO test_table (name, created_at) 
                    VALUES (@name, @createdAt)
                    RETURNING id";
                insertCmd.Parameters.AddWithValue("@name", "Test Item");
                insertCmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                
                var id = await insertCmd.ExecuteScalarAsync();
                id.Should().NotBeNull();
            }

            // Select
            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT COUNT(*) FROM test_table";
                var count = await selectCmd.ExecuteScalarAsync();
                count.Should().Be(1L);
            }

            // Update
            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.CommandText = "UPDATE test_table SET name = @name WHERE id = 1";
                updateCmd.Parameters.AddWithValue("@name", "Updated Item");
                var affected = await updateCmd.ExecuteNonQueryAsync();
                affected.Should().Be(1);
            }

            // Delete
            using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.CommandText = "DELETE FROM test_table WHERE id = 1";
                var affected = await deleteCmd.ExecuteNonQueryAsync();
                affected.Should().Be(1);
            }
        }

        [Fact]
        public async Task Transaction_Rollback_ShouldWork()
        {
            // Arrange
            var connectionString = _mssqlContainer.GetConnectionString();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Create table
            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = @"
                    CREATE TABLE TransactionTest (
                        Id INT PRIMARY KEY IDENTITY,
                        Value INT
                    )";
                await createCmd.ExecuteNonQueryAsync();
            }

            // Start transaction
            using var transaction = connection.BeginTransaction();

            try
            {
                // Insert with transaction
                using (var insertCmd = connection.CreateCommand())
                {
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = "INSERT INTO TransactionTest (Value) VALUES (100)";
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Rollback
                await transaction.RollbackAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Verify rollback
            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT COUNT(*) FROM TransactionTest";
                var count = await selectCmd.ExecuteScalarAsync();
                count.Should().Be(0);
            }
        }
    }
}