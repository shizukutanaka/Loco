using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Loco.Core.RateLimiting
{
    public interface IDistributedRateLimiter
    {
        Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitRule rule);
        Task<RateLimitResult> CheckRateLimitAsync(string key, int limit, TimeSpan period);
        Task<bool> IsAllowedAsync(string key, int limit = 100, TimeSpan? period = null);
        Task<RateLimitStatus> GetStatusAsync(string key);
        Task ResetAsync(string key);
        Task<Dictionary<string, RateLimitStatus>> GetAllStatusesAsync(string pattern = "*");
        void ConfigureRule(string ruleName, RateLimitRule rule);
        Task<long> GetRemainingTokensAsync(string key, string ruleName);
    }

    public class DistributedRateLimiter : IDistributedRateLimiter
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DistributedRateLimiter> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly Dictionary<string, RateLimitRule> _rules;
        private readonly string _keyPrefix;
        private readonly bool _enableDistributed;

        public DistributedRateLimiter(
            IConfiguration configuration,
            ILogger<DistributedRateLimiter> logger,
            IConnectionMultiplexer redis = null)
        {
            _configuration = configuration;
            _logger = logger;
            _redis = redis;
            _database = redis?.GetDatabase();
            _rules = new Dictionary<string, RateLimitRule>();
            _keyPrefix = _configuration["RateLimiting:KeyPrefix"] ?? "ratelimit:";
            _enableDistributed = _configuration.GetValue<bool>("RateLimiting:EnableDistributed", true) && redis != null;
            
            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            ConfigureRule("default", new RateLimitRule
            {
                Limit = 100,
                Period = TimeSpan.FromMinutes(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow
            });
            
            ConfigureRule("api", new RateLimitRule
            {
                Limit = 1000,
                Period = TimeSpan.FromHours(1),
                Algorithm = RateLimitAlgorithm.TokenBucket,
                BurstSize = 50
            });
            
            ConfigureRule("auth", new RateLimitRule
            {
                Limit = 5,
                Period = TimeSpan.FromMinutes(15),
                Algorithm = RateLimitAlgorithm.FixedWindow,
                BlockDuration = TimeSpan.FromMinutes(30)
            });
            
            ConfigureRule("heavy", new RateLimitRule
            {
                Limit = 10,
                Period = TimeSpan.FromMinutes(5),
                Algorithm = RateLimitAlgorithm.LeakyBucket,
                LeakRate = 2
            });
        }

        public void ConfigureRule(string ruleName, RateLimitRule rule)
        {
            _rules[ruleName] = rule;
            _logger.LogInformation("Configured rate limit rule '{RuleName}': {Limit} per {Period}", 
                ruleName, rule.Limit, rule.Period);
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitRule rule)
        {
            if (!_enableDistributed)
            {
                return CheckInMemory(key, rule);
            }

            try
            {
                return rule.Algorithm switch
                {
                    RateLimitAlgorithm.FixedWindow => await CheckFixedWindowAsync(key, rule),
                    RateLimitAlgorithm.SlidingWindow => await CheckSlidingWindowAsync(key, rule),
                    RateLimitAlgorithm.TokenBucket => await CheckTokenBucketAsync(key, rule),
                    RateLimitAlgorithm.LeakyBucket => await CheckLeakyBucketAsync(key, rule),
                    _ => await CheckSlidingWindowAsync(key, rule)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for key {Key}", key);
                return new RateLimitResult { IsAllowed = true };
            }
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, int limit, TimeSpan period)
        {
            var rule = new RateLimitRule
            {
                Limit = limit,
                Period = period,
                Algorithm = RateLimitAlgorithm.SlidingWindow
            };
            
            return await CheckRateLimitAsync(key, rule);
        }

        public async Task<bool> IsAllowedAsync(string key, int limit = 100, TimeSpan? period = null)
        {
            var result = await CheckRateLimitAsync(key, limit, period ?? TimeSpan.FromMinutes(1));
            return result.IsAllowed;
        }

        private async Task<RateLimitResult> CheckFixedWindowAsync(string key, RateLimitRule rule)
        {
            var redisKey = $"{_keyPrefix}fixed:{key}";
            var windowKey = $"{redisKey}:{GetCurrentWindow(rule.Period)}";
            
            var script = @"
                local key = KEYS[1]
                local limit = tonumber(ARGV[1])
                local window = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])
                
                local current = redis.call('GET', key)
                if current == false then
                    redis.call('SET', key, 1, 'EX', window)
                    return {1, limit - 1, window}
                end
                
                current = tonumber(current)
                if current < limit then
                    redis.call('INCR', key)
                    return {1, limit - current - 1, redis.call('TTL', key)}
                else
                    return {0, 0, redis.call('TTL', key)}
                end
            ";
            
            var result = await _database.ScriptEvaluateAsync(script,
                new RedisKey[] { windowKey },
                new RedisValue[] { rule.Limit, (int)rule.Period.TotalSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            
            var array = (RedisValue[])result;
            return new RateLimitResult
            {
                IsAllowed = (int)array[0] == 1,
                RemainingTokens = (int)array[1],
                ResetAfter = TimeSpan.FromSeconds((int)array[2]),
                Limit = rule.Limit,
                Period = rule.Period
            };
        }

        private async Task<RateLimitResult> CheckSlidingWindowAsync(string key, RateLimitRule rule)
        {
            var redisKey = $"{_keyPrefix}sliding:{key}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStart = now - (long)rule.Period.TotalMilliseconds;
            
            var script = @"
                local key = KEYS[1]
                local limit = tonumber(ARGV[1])
                local window = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])
                local window_start = now - window
                
                redis.call('ZREMRANGEBYSCORE', key, '-inf', window_start)
                local current = redis.call('ZCARD', key)
                
                if current < limit then
                    redis.call('ZADD', key, now, now)
                    redis.call('EXPIRE', key, window / 1000)
                    return {1, limit - current - 1}
                else
                    return {0, 0}
                end
            ";
            
            var result = await _database.ScriptEvaluateAsync(script,
                new RedisKey[] { redisKey },
                new RedisValue[] { rule.Limit, (long)rule.Period.TotalMilliseconds, now });
            
            var array = (RedisValue[])result;
            return new RateLimitResult
            {
                IsAllowed = (int)array[0] == 1,
                RemainingTokens = (int)array[1],
                ResetAfter = rule.Period,
                Limit = rule.Limit,
                Period = rule.Period
            };
        }

        private async Task<RateLimitResult> CheckTokenBucketAsync(string key, RateLimitRule rule)
        {
            var redisKey = $"{_keyPrefix}token:{key}";
            var refillRate = (double)rule.Limit / rule.Period.TotalSeconds;
            var capacity = rule.BurstSize > 0 ? rule.BurstSize : rule.Limit;
            
            var script = @"
                local key = KEYS[1]
                local capacity = tonumber(ARGV[1])
                local refill_rate = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])
                local requested = tonumber(ARGV[4])
                
                local bucket = redis.call('HMGET', key, 'tokens', 'last_refill')
                local tokens = tonumber(bucket[1]) or capacity
                local last_refill = tonumber(bucket[2]) or now
                
                local time_passed = now - last_refill
                local new_tokens = math.min(capacity, tokens + (time_passed * refill_rate))
                
                if new_tokens >= requested then
                    new_tokens = new_tokens - requested
                    redis.call('HMSET', key, 'tokens', new_tokens, 'last_refill', now)
                    redis.call('EXPIRE', key, 3600)
                    return {1, math.floor(new_tokens)}
                else
                    redis.call('HMSET', key, 'tokens', new_tokens, 'last_refill', now)
                    redis.call('EXPIRE', key, 3600)
                    return {0, math.floor(new_tokens)}
                end
            ";
            
            var result = await _database.ScriptEvaluateAsync(script,
                new RedisKey[] { redisKey },
                new RedisValue[] { capacity, refillRate, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 1 });
            
            var array = (RedisValue[])result;
            return new RateLimitResult
            {
                IsAllowed = (int)array[0] == 1,
                RemainingTokens = (int)array[1],
                ResetAfter = TimeSpan.FromSeconds((capacity - (int)array[1]) / refillRate),
                Limit = rule.Limit,
                Period = rule.Period
            };
        }

        private async Task<RateLimitResult> CheckLeakyBucketAsync(string key, RateLimitRule rule)
        {
            var redisKey = $"{_keyPrefix}leaky:{key}";
            var leakRate = rule.LeakRate > 0 ? rule.LeakRate : 1;
            
            var script = @"
                local key = KEYS[1]
                local capacity = tonumber(ARGV[1])
                local leak_rate = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])
                
                local bucket = redis.call('HMGET', key, 'volume', 'last_leak')
                local volume = tonumber(bucket[1]) or 0
                local last_leak = tonumber(bucket[2]) or now
                
                local time_passed = now - last_leak
                local leaked = time_passed * leak_rate
                volume = math.max(0, volume - leaked)
                
                if volume < capacity then
                    volume = volume + 1
                    redis.call('HMSET', key, 'volume', volume, 'last_leak', now)
                    redis.call('EXPIRE', key, 3600)
                    return {1, capacity - math.floor(volume)}
                else
                    redis.call('HMSET', key, 'volume', volume, 'last_leak', now)
                    redis.call('EXPIRE', key, 3600)
                    return {0, 0}
                end
            ";
            
            var result = await _database.ScriptEvaluateAsync(script,
                new RedisKey[] { redisKey },
                new RedisValue[] { rule.Limit, leakRate, DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            
            var array = (RedisValue[])result;
            return new RateLimitResult
            {
                IsAllowed = (int)array[0] == 1,
                RemainingTokens = (int)array[1],
                ResetAfter = TimeSpan.FromSeconds((rule.Limit - (int)array[1]) / leakRate),
                Limit = rule.Limit,
                Period = rule.Period
            };
        }

        public async Task<RateLimitStatus> GetStatusAsync(string key)
        {
            var status = new RateLimitStatus { Key = key };
            
            if (!_enableDistributed)
            {
                return status;
            }
            
            try
            {
                var tasks = new List<Task>();
                
                foreach (var rule in _rules)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var result = await CheckRateLimitAsync(key, rule.Value);
                        status.Rules[rule.Key] = new RateLimitRuleStatus
                        {
                            RuleName = rule.Key,
                            Limit = result.Limit,
                            RemainingTokens = result.RemainingTokens,
                            ResetAfter = result.ResetAfter,
                            IsBlocked = !result.IsAllowed
                        };
                    }));
                }
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit status for key {Key}", key);
            }
            
            return status;
        }

        public async Task ResetAsync(string key)
        {
            if (!_enableDistributed)
            {
                _logger.LogInformation("Reset rate limit for key {Key} (in-memory)", key);
                return;
            }
            
            try
            {
                var pattern = $"{_keyPrefix}*:{key}";
                var server = _redis.GetServer(_redis.GetEndPoints()[0]);
                var keys = server.Keys(pattern: pattern);
                
                foreach (var redisKey in keys)
                {
                    await _database.KeyDeleteAsync(redisKey);
                }
                
                _logger.LogInformation("Reset rate limit for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting rate limit for key {Key}", key);
            }
        }

        public async Task<Dictionary<string, RateLimitStatus>> GetAllStatusesAsync(string pattern = "*")
        {
            var statuses = new Dictionary<string, RateLimitStatus>();
            
            if (!_enableDistributed)
            {
                return statuses;
            }
            
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints()[0]);
                var keys = server.Keys(pattern: $"{_keyPrefix}*:{pattern}");
                var uniqueKeys = new HashSet<string>();
                
                foreach (var redisKey in keys)
                {
                    var parts = redisKey.ToString().Split(':');
                    if (parts.Length >= 3)
                    {
                        uniqueKeys.Add(parts[2]);
                    }
                }
                
                foreach (var key in uniqueKeys)
                {
                    statuses[key] = await GetStatusAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all rate limit statuses");
            }
            
            return statuses;
        }

        public async Task<long> GetRemainingTokensAsync(string key, string ruleName)
        {
            if (!_rules.TryGetValue(ruleName, out var rule))
            {
                return -1;
            }
            
            var result = await CheckRateLimitAsync(key, rule);
            return result.RemainingTokens;
        }

        private long GetCurrentWindow(TimeSpan period)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var window = (long)period.TotalSeconds;
            return now / window * window;
        }

        private RateLimitResult CheckInMemory(string key, RateLimitRule rule)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingTokens = rule.Limit,
                ResetAfter = rule.Period,
                Limit = rule.Limit,
                Period = rule.Period
            };
        }
    }

    public class RateLimitRule
    {
        public int Limit { get; set; }
        public TimeSpan Period { get; set; }
        public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;
        public int BurstSize { get; set; }
        public double LeakRate { get; set; }
        public TimeSpan? BlockDuration { get; set; }
    }

    public enum RateLimitAlgorithm
    {
        FixedWindow,
        SlidingWindow,
        TokenBucket,
        LeakyBucket
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingTokens { get; set; }
        public TimeSpan ResetAfter { get; set; }
        public int Limit { get; set; }
        public TimeSpan Period { get; set; }
        public string RetryAfterHeader => ResetAfter.TotalSeconds.ToString("0");
        public string RateLimitHeader => Limit.ToString();
        public string RemainingHeader => RemainingTokens.ToString();
        public string ResetHeader => DateTimeOffset.UtcNow.Add(ResetAfter).ToUnixTimeSeconds().ToString();
    }

    public class RateLimitStatus
    {
        public string Key { get; set; }
        public Dictionary<string, RateLimitRuleStatus> Rules { get; set; } = new Dictionary<string, RateLimitRuleStatus>();
    }

    public class RateLimitRuleStatus
    {
        public string RuleName { get; set; }
        public int Limit { get; set; }
        public int RemainingTokens { get; set; }
        public TimeSpan ResetAfter { get; set; }
        public bool IsBlocked { get; set; }
    }
}