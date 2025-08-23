using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Data
{
    public interface IDataSeeder
    {
        Task SeedAsync(bool force = false);
        Task SeedUsersAsync(int count = 10);
        Task SeedFlowsAsync(int count = 20);
        Task SeedRulesAsync(int count = 30);
        Task SeedTestDataAsync();
        Task ClearAllDataAsync();
        Task<SeedingReport> GetSeedingReportAsync();
    }

    public class DataSeeder : IDataSeeder
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataSeeder> _logger;
        private readonly DbContext _dbContext;
        private readonly Faker _faker;
        private readonly List<SeedingOperation> _operations;

        public DataSeeder(
            IConfiguration configuration,
            ILogger<DataSeeder> logger,
            DbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
            _faker = new Faker();
            _operations = new List<SeedingOperation>();
        }

        public async Task SeedAsync(bool force = false)
        {
            try
            {
                if (!force && await DataExistsAsync())
                {
                    _logger.LogInformation("Data already exists, skipping seeding");
                    return;
                }

                _logger.LogInformation("Starting data seeding...");
                
                await SeedRolesAsync();
                await SeedPermissionsAsync();
                await SeedUsersAsync(50);
                await SeedFlowsAsync(100);
                await SeedRulesAsync(150);
                await SeedFeatureFlagsAsync();
                await SeedApiKeysAsync();
                await SeedConfigurationAsync();
                
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data seeding");
                throw;
            }
        }

        private async Task<bool> DataExistsAsync()
        {
            return await _dbContext.Set<User>().AnyAsync();
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[]
            {
                new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "Full system access" },
                new Role { Id = Guid.NewGuid(), Name = "Moderator", Description = "Moderate content and users" },
                new Role { Id = Guid.NewGuid(), Name = "User", Description = "Standard user access" },
                new Role { Id = Guid.NewGuid(), Name = "Premium", Description = "Premium features access" },
                new Role { Id = Guid.NewGuid(), Name = "Developer", Description = "API and development access" }
            };

            await _dbContext.Set<Role>().AddRangeAsync(roles);
            RecordOperation("Roles", roles.Length);
        }

        private async Task SeedPermissionsAsync()
        {
            var permissions = new[]
            {
                "users:read", "users:write", "users:delete", "users:manage",
                "flows:read", "flows:write", "flows:delete", "flows:execute",
                "rules:read", "rules:write", "rules:delete", "rules:activate",
                "reports:view", "reports:export", "reports:schedule",
                "settings:read", "settings:write",
                "api:access", "api:unlimited"
            };

            var permissionEntities = permissions.Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                Name = p,
                Description = $"Permission to {p.Replace(':', ' ')}"
            });

            await _dbContext.Set<Permission>().AddRangeAsync(permissionEntities);
            RecordOperation("Permissions", permissions.Length);
        }

        public async Task SeedUsersAsync(int count = 10)
        {
            var userFaker = new Faker<User>()
                .RuleFor(u => u.Id, f => Guid.NewGuid())
                .RuleFor(u => u.Username, f => f.Internet.UserName())
                .RuleFor(u => u.Email, f => f.Internet.Email())
                .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                .RuleFor(u => u.LastName, f => f.Name.LastName())
                .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber())
                .RuleFor(u => u.DateOfBirth, f => f.Date.Between(DateTime.Now.AddYears(-60), DateTime.Now.AddYears(-18)))
                .RuleFor(u => u.IsActive, f => f.Random.Bool(0.9f))
                .RuleFor(u => u.IsEmailVerified, f => f.Random.Bool(0.8f))
                .RuleFor(u => u.CreatedAt, f => f.Date.Between(DateTime.Now.AddYears(-2), DateTime.Now))
                .RuleFor(u => u.LastLoginAt, f => f.Date.Recent(30))
                .RuleFor(u => u.ProfilePicture, f => f.Internet.Avatar())
                .RuleFor(u => u.Bio, f => f.Lorem.Paragraph())
                .RuleFor(u => u.Location, f => $"{f.Address.City()}, {f.Address.Country()}")
                .RuleFor(u => u.Website, f => f.Internet.Url())
                .RuleFor(u => u.Company, f => f.Company.CompanyName())
                .RuleFor(u => u.JobTitle, f => f.Name.JobTitle());

            var users = userFaker.Generate(count);
            
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@loco.app",
                FirstName = "Admin",
                LastName = "User",
                IsActive = true,
                IsEmailVerified = true,
                CreatedAt = DateTime.Now.AddYears(-1),
                LastLoginAt = DateTime.Now
            };
            users.Add(adminUser);

            await _dbContext.Set<User>().AddRangeAsync(users);
            RecordOperation("Users", users.Count);
        }

        public async Task SeedFlowsAsync(int count = 20)
        {
            var flowFaker = new Faker<Flow>()
                .RuleFor(f => f.Id, f => Guid.NewGuid())
                .RuleFor(f => f.Name, f => f.Commerce.ProductName())
                .RuleFor(f => f.Description, f => f.Lorem.Sentence())
                .RuleFor(f => f.Version, f => f.System.Version().ToString())
                .RuleFor(f => f.IsEnabled, f => f.Random.Bool(0.7f))
                .RuleFor(f => f.Category, f => f.PickRandom("Automation", "Integration", "Notification", "Data Processing", "Reporting"))
                .RuleFor(f => f.Priority, f => f.PickRandom("Low", "Medium", "High", "Critical"))
                .RuleFor(f => f.Schedule, f => f.PickRandom(null, "0 */6 * * *", "0 0 * * *", "*/15 * * * *"))
                .RuleFor(f => f.MaxRetries, f => f.Random.Int(1, 5))
                .RuleFor(f => f.TimeoutSeconds, f => f.Random.Int(30, 300))
                .RuleFor(f => f.CreatedAt, f => f.Date.Between(DateTime.Now.AddMonths(-6), DateTime.Now))
                .RuleFor(f => f.LastExecutedAt, f => f.Date.Recent(7))
                .RuleFor(f => f.ExecutionCount, f => f.Random.Int(0, 1000))
                .RuleFor(f => f.SuccessCount, (f, flow) => f.Random.Int(0, flow.ExecutionCount))
                .RuleFor(f => f.FailureCount, (f, flow) => flow.ExecutionCount - flow.SuccessCount)
                .RuleFor(f => f.AverageExecutionTime, f => f.Random.Double(100, 5000))
                .RuleFor(f => f.Tags, f => f.Make(3, () => f.Lorem.Word()));

            var flows = flowFaker.Generate(count);
            
            var sampleFlows = new[]
            {
                new Flow
                {
                    Id = Guid.NewGuid(),
                    Name = "Daily Backup",
                    Description = "Automated daily backup of critical data",
                    IsEnabled = true,
                    Category = "Automation",
                    Priority = "High",
                    Schedule = "0 2 * * *",
                    CreatedAt = DateTime.Now.AddMonths(-3)
                },
                new Flow
                {
                    Id = Guid.NewGuid(),
                    Name = "Email Notification",
                    Description = "Send email notifications on events",
                    IsEnabled = true,
                    Category = "Notification",
                    Priority = "Medium",
                    CreatedAt = DateTime.Now.AddMonths(-2)
                },
                new Flow
                {
                    Id = Guid.NewGuid(),
                    Name = "Data Sync",
                    Description = "Synchronize data between systems",
                    IsEnabled = true,
                    Category = "Integration",
                    Priority = "Critical",
                    Schedule = "*/30 * * * *",
                    CreatedAt = DateTime.Now.AddMonths(-1)
                }
            };
            
            flows.AddRange(sampleFlows);
            await _dbContext.Set<Flow>().AddRangeAsync(flows);
            RecordOperation("Flows", flows.Count);
        }

        public async Task SeedRulesAsync(int count = 30)
        {
            var ruleFaker = new Faker<Rule>()
                .RuleFor(r => r.Id, f => Guid.NewGuid())
                .RuleFor(r => r.Name, f => f.Hacker.Phrase())
                .RuleFor(r => r.Description, f => f.Lorem.Sentence())
                .RuleFor(r => r.Condition, f => GenerateCondition(f))
                .RuleFor(r => r.Action, f => GenerateAction(f))
                .RuleFor(r => r.IsActive, f => f.Random.Bool(0.8f))
                .RuleFor(r => r.Priority, f => f.Random.Int(1, 100))
                .RuleFor(r => r.Category, f => f.PickRandom("Security", "Performance", "Business Logic", "Data Validation", "Workflow"))
                .RuleFor(r => r.CreatedAt, f => f.Date.Between(DateTime.Now.AddMonths(-6), DateTime.Now))
                .RuleFor(r => r.LastModifiedAt, f => f.Date.Recent(14))
                .RuleFor(r => r.LastEvaluatedAt, f => f.Date.Recent(1))
                .RuleFor(r => r.EvaluationCount, f => f.Random.Int(0, 10000))
                .RuleFor(r => r.MatchCount, (f, rule) => f.Random.Int(0, rule.EvaluationCount))
                .RuleFor(r => r.Tags, f => f.Make(2, () => f.Lorem.Word()));

            var rules = ruleFaker.Generate(count);
            
            var predefinedRules = new[]
            {
                new Rule
                {
                    Id = Guid.NewGuid(),
                    Name = "Rate Limit Check",
                    Description = "Enforce rate limiting on API calls",
                    Condition = "request.rate > 100",
                    Action = "block",
                    IsActive = true,
                    Priority = 1,
                    Category = "Security",
                    CreatedAt = DateTime.Now.AddMonths(-2)
                },
                new Rule
                {
                    Id = Guid.NewGuid(),
                    Name = "Data Validation",
                    Description = "Validate input data format",
                    Condition = "data.format == 'invalid'",
                    Action = "reject",
                    IsActive = true,
                    Priority = 2,
                    Category = "Data Validation",
                    CreatedAt = DateTime.Now.AddMonths(-1)
                }
            };
            
            rules.AddRange(predefinedRules);
            await _dbContext.Set<Rule>().AddRangeAsync(rules);
            RecordOperation("Rules", rules.Count);
        }

        private async Task SeedFeatureFlagsAsync()
        {
            var flags = new[]
            {
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    Name = "NewDashboard",
                    Description = "Enable new dashboard UI",
                    IsEnabled = true,
                    RolloutPercentage = 100,
                    CreatedAt = DateTime.Now.AddDays(-30)
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    Name = "AIAssistant",
                    Description = "Enable AI assistant features",
                    IsEnabled = true,
                    RolloutPercentage = 50,
                    CreatedAt = DateTime.Now.AddDays(-15)
                },
                new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    Name = "BetaFeatures",
                    Description = "Enable beta features for testing",
                    IsEnabled = false,
                    RolloutPercentage = 0,
                    CreatedAt = DateTime.Now.AddDays(-7)
                }
            };

            await _dbContext.Set<FeatureFlag>().AddRangeAsync(flags);
            RecordOperation("FeatureFlags", flags.Length);
        }

        private async Task SeedApiKeysAsync()
        {
            var apiKeys = new[]
            {
                new ApiKey
                {
                    Id = Guid.NewGuid(),
                    Key = GenerateApiKey(),
                    Name = "Production API Key",
                    Description = "Main production API key",
                    IsActive = true,
                    ExpiresAt = DateTime.Now.AddYears(1),
                    Permissions = new[] { "api:access", "flows:execute" },
                    RateLimit = 1000,
                    CreatedAt = DateTime.Now.AddMonths(-6)
                },
                new ApiKey
                {
                    Id = Guid.NewGuid(),
                    Key = GenerateApiKey(),
                    Name = "Development API Key",
                    Description = "Development and testing",
                    IsActive = true,
                    ExpiresAt = DateTime.Now.AddMonths(6),
                    Permissions = new[] { "api:access" },
                    RateLimit = 100,
                    CreatedAt = DateTime.Now.AddMonths(-3)
                }
            };

            await _dbContext.Set<ApiKey>().AddRangeAsync(apiKeys);
            RecordOperation("ApiKeys", apiKeys.Length);
        }

        private async Task SeedConfigurationAsync()
        {
            var configs = new[]
            {
                new ConfigurationSetting { Key = "System.MaintenanceMode", Value = "false", Category = "System" },
                new ConfigurationSetting { Key = "System.MaxConcurrentFlows", Value = "10", Category = "System" },
                new ConfigurationSetting { Key = "Email.SmtpHost", Value = "smtp.example.com", Category = "Email" },
                new ConfigurationSetting { Key = "Email.SmtpPort", Value = "587", Category = "Email" },
                new ConfigurationSetting { Key = "Security.PasswordMinLength", Value = "8", Category = "Security" },
                new ConfigurationSetting { Key = "Security.RequireTwoFactor", Value = "false", Category = "Security" },
                new ConfigurationSetting { Key = "Logging.Level", Value = "Information", Category = "Logging" },
                new ConfigurationSetting { Key = "Monitoring.Enabled", Value = "true", Category = "Monitoring" }
            };

            await _dbContext.Set<ConfigurationSetting>().AddRangeAsync(configs);
            RecordOperation("ConfigurationSettings", configs.Length);
        }

        public async Task SeedTestDataAsync()
        {
            _logger.LogInformation("Seeding test data...");
            
            var testUsers = new Faker<User>()
                .RuleFor(u => u.Id, f => Guid.NewGuid())
                .RuleFor(u => u.Username, (f, u) => $"test_{f.IndexFaker}")
                .RuleFor(u => u.Email, (f, u) => $"test{f.IndexFaker}@test.com")
                .RuleFor(u => u.FirstName, f => "Test")
                .RuleFor(u => u.LastName, (f, u) => $"User{f.IndexFaker}")
                .RuleFor(u => u.IsActive, f => true)
                .RuleFor(u => u.IsEmailVerified, f => true)
                .RuleFor(u => u.CreatedAt, f => DateTime.Now)
                .Generate(5);

            await _dbContext.Set<User>().AddRangeAsync(testUsers);
            await _dbContext.SaveChangesAsync();
            
            RecordOperation("TestUsers", testUsers.Count);
            _logger.LogInformation("Test data seeded successfully");
        }

        public async Task ClearAllDataAsync()
        {
            _logger.LogWarning("Clearing all data from database...");
            
            var entityTypes = _dbContext.Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    await _dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM [{tableName}]");
                }
            }
            
            _logger.LogInformation("All data cleared successfully");
        }

        public async Task<SeedingReport> GetSeedingReportAsync()
        {
            var report = new SeedingReport
            {
                GeneratedAt = DateTime.Now,
                Operations = _operations.ToList()
            };

            foreach (var entityType in _dbContext.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                var dbSet = _dbContext.GetType()
                    .GetMethod("Set", Type.EmptyTypes)
                    ?.MakeGenericMethod(clrType)
                    .Invoke(_dbContext, null);

                if (dbSet != null)
                {
                    var countMethod = typeof(EntityFrameworkQueryableExtensions)
                        .GetMethod("CountAsync", new[] { typeof(IQueryable<>).MakeGenericType(clrType), typeof(CancellationToken) })
                        ?.MakeGenericMethod(clrType);

                    if (countMethod != null)
                    {
                        var countTask = (Task<int>)countMethod.Invoke(null, new object[] { dbSet, default(CancellationToken) });
                        var count = await countTask;
                        report.EntityCounts[clrType.Name] = count;
                    }
                }
            }

            return report;
        }

        private string GenerateCondition(Faker faker)
        {
            var conditions = new[]
            {
                "value > 100",
                "status == 'active'",
                "count < 50",
                "type != 'invalid'",
                "timestamp > now() - 3600",
                "user.role == 'admin'",
                "data.size > 1024",
                "request.method == 'POST'"
            };
            return faker.PickRandom(conditions);
        }

        private string GenerateAction(Faker faker)
        {
            var actions = new[]
            {
                "allow",
                "block",
                "notify",
                "log",
                "redirect",
                "transform",
                "validate",
                "execute"
            };
            return faker.PickRandom(actions);
        }

        private string GenerateApiKey()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        private void RecordOperation(string entityType, int count)
        {
            _operations.Add(new SeedingOperation
            {
                EntityType = entityType,
                Count = count,
                Timestamp = DateTime.Now
            });
        }
    }

    public class SeedingReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<SeedingOperation> Operations { get; set; } = new List<SeedingOperation>();
        public Dictionary<string, int> EntityCounts { get; set; } = new Dictionary<string, int>();
    }

    public class SeedingOperation
    {
        public string EntityType { get; set; }
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string ProfilePicture { get; set; }
        public string Bio { get; set; }
        public string Location { get; set; }
        public string Website { get; set; }
        public string Company { get; set; }
        public string JobTitle { get; set; }
    }

    public class Flow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public bool IsEnabled { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }
        public string Schedule { get; set; }
        public int MaxRetries { get; set; }
        public int TimeoutSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public int ExecutionCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AverageExecutionTime { get; set; }
        public List<string> Tags { get; set; }
    }

    public class Rule
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Condition { get; set; }
        public string Action { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public string Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? LastEvaluatedAt { get; set; }
        public int EvaluationCount { get; set; }
        public int MatchCount { get; set; }
        public List<string> Tags { get; set; }
    }

    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class Permission
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class FeatureFlag
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public int RolloutPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ApiKey
    {
        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string[] Permissions { get; set; }
        public int RateLimit { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ConfigurationSetting
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Category { get; set; }
    }
}