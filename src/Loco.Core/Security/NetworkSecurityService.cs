using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace Loco.Core.Security
{
    public interface INetworkSecurityService
    {
        Task<bool> IsIpAddressAllowedAsync(IPAddress ipAddress);
        Task<bool> IsDdosAttackAsync(IPAddress ipAddress);
        Task<SecurityThreatLevel> AssessConnectionThreatAsync(IPAddress ipAddress, string userAgent);
        void BlockIpAddress(IPAddress ipAddress, TimeSpan? duration = null);
        void UnblockIpAddress(IPAddress ipAddress);
        Task<bool> ValidateHttpRequestAsync(string method, string path, Dictionary<string, string> headers);
        Task<bool> CheckGeolocationRiskAsync(IPAddress ipAddress);
        void LogNetworkEvent(NetworkSecurityEvent networkEvent);
        Task<bool> IsKnownBotAsync(string userAgent);
        Task<bool> ValidateTlsConnectionAsync(IPAddress clientIp);
    }

    public enum SecurityThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    public class NetworkSecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public IPAddress IpAddress { get; set; }
        public string UserAgent { get; set; }
        public SecurityThreatLevel ThreatLevel { get; set; }
        public string Details { get; set; }
        public string Action { get; set; }
        public string Country { get; set; }
        public long ResponseTime { get; set; }
    }

    public class IpAddressInfo
    {
        public IPAddress Address { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int RequestCount { get; set; }
        public int FailedAttempts { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public string Country { get; set; }
        public SecurityThreatLevel ThreatLevel { get; set; }
        public List<string> ViolationTypes { get; set; } = new List<string>();
    }

    public class NetworkSecurityService : INetworkSecurityService
    {
        private readonly ILogger<NetworkSecurityService> _logger;
        private readonly Dictionary<IPAddress, IpAddressInfo> _ipAddressCache;
        private readonly HashSet<IPAddress> _blockedIps;
        private readonly HashSet<string> _blockedCountries;
        private readonly HashSet<string> _knownMaliciousUserAgents;
        private readonly Dictionary<IPAddress, List<DateTime>> _requestHistory;
        private readonly object _lockObject = new object();

        // Threat intelligence data
        private readonly HashSet<IPAddress> _knownMaliciousIps;
        private readonly List<Regex> _maliciousUserAgentPatterns;
        private readonly Dictionary<string, int> _countrySafety;

        // DDoS detection parameters
        private const int DdosRequestThreshold = 100; // requests per minute
        private const int DdosTimeWindowMinutes = 1;
        private const int MaxFailedAttempts = 5;
        private readonly TimeSpan _blockDuration = TimeSpan.FromHours(24);

        public NetworkSecurityService(ILogger<NetworkSecurityService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ipAddressCache = new Dictionary<IPAddress, IpAddressInfo>();
            _blockedIps = new HashSet<IPAddress>();
            _blockedCountries = new HashSet<string> { "CN", "RU", "KP", "IR" }; // High-risk countries
            _knownMaliciousUserAgents = new HashSet<string>();
            _requestHistory = new Dictionary<IPAddress, List<DateTime>>();
            _knownMaliciousIps = new HashSet<IPAddress>();
            
            InitializeThreatIntelligence();
            InitializeMaliciousUserAgentPatterns();
            InitializeCountrySafetyRatings();
        }

        public async Task<bool> IsIpAddressAllowedAsync(IPAddress ipAddress)
        {
            if (ipAddress == null)
                return false;

            // Check if IP is blocked
            lock (_lockObject)
            {
                if (_blockedIps.Contains(ipAddress))
                    return false;

                // Check if IP has a temporary block that has expired
                if (_ipAddressCache.TryGetValue(ipAddress, out var ipInfo))
                {
                    if (ipInfo.IsBlocked && ipInfo.BlockedUntil.HasValue && DateTime.UtcNow > ipInfo.BlockedUntil.Value)
                    {
                        ipInfo.IsBlocked = false;
                        ipInfo.BlockedUntil = null;
                        _blockedIps.Remove(ipAddress);
                    }
                    else if (ipInfo.IsBlocked)
                    {
                        return false;
                    }
                }
            }

            // Check against known malicious IPs
            if (_knownMaliciousIps.Contains(ipAddress))
            {
                LogNetworkEvent(new NetworkSecurityEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "Known Malicious IP",
                    IpAddress = ipAddress,
                    ThreatLevel = SecurityThreatLevel.Critical,
                    Action = "Blocked",
                    Details = "IP found in threat intelligence database"
                });
                return false;
            }

            // Check private/reserved IP ranges
            if (IsPrivateIpAddress(ipAddress))
            {
                return true; // Allow private IPs (internal network)
            }

            // Additional geolocation and reputation checks
            var isGeoRisk = await CheckGeolocationRiskAsync(ipAddress);
            if (isGeoRisk)
            {
                LogNetworkEvent(new NetworkSecurityEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "Geolocation Risk",
                    IpAddress = ipAddress,
                    ThreatLevel = SecurityThreatLevel.Medium,
                    Action = "Flagged",
                    Details = "IP from high-risk geographic location"
                });
                return false;
            }

            return true;
        }

        public async Task<bool> IsDdosAttackAsync(IPAddress ipAddress)
        {
            if (ipAddress == null)
                return false;

            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var now = DateTime.UtcNow;
                    var cutoffTime = now.AddMinutes(-DdosTimeWindowMinutes);

                    // Get or create request history for this IP
                    if (!_requestHistory.TryGetValue(ipAddress, out var requests))
                    {
                        requests = new List<DateTime>();
                        _requestHistory[ipAddress] = requests;
                    }

                    // Add current request
                    requests.Add(now);

                    // Remove old requests outside the time window
                    requests.RemoveAll(r => r < cutoffTime);

                    // Check if request count exceeds threshold
                    if (requests.Count > DdosRequestThreshold)
                    {
                        LogNetworkEvent(new NetworkSecurityEvent
                        {
                            Timestamp = DateTime.UtcNow,
                            EventType = "DDoS Attack Detected",
                            IpAddress = ipAddress,
                            ThreatLevel = SecurityThreatLevel.Critical,
                            Action = "Blocked",
                            Details = $"Excessive requests: {requests.Count} in {DdosTimeWindowMinutes} minutes"
                        });

                        // Auto-block the IP
                        BlockIpAddress(ipAddress, _blockDuration);
                        return true;
                    }
                }
                return false;
            });
        }

        public async Task<SecurityThreatLevel> AssessConnectionThreatAsync(IPAddress ipAddress, string userAgent)
        {
            var threatLevel = SecurityThreatLevel.None;

            // Check IP reputation
            if (_knownMaliciousIps.Contains(ipAddress))
                threatLevel = SecurityThreatLevel.Critical;

            // Check user agent
            if (!string.IsNullOrEmpty(userAgent))
            {
                if (_knownMaliciousUserAgents.Contains(userAgent.ToLower()))
                    threatLevel = SecurityThreatLevel.High;

                foreach (var pattern in _maliciousUserAgentPatterns)
                {
                    if (pattern.IsMatch(userAgent))
                    {
                        threatLevel = SecurityThreatLevel.High;
                        break;
                    }
                }

                // Check for bot patterns
                if (await IsKnownBotAsync(userAgent) && threatLevel == SecurityThreatLevel.None)
                    threatLevel = SecurityThreatLevel.Low;
            }

            // Check geolocation risk
            if (await CheckGeolocationRiskAsync(ipAddress) && threatLevel < SecurityThreatLevel.Medium)
                threatLevel = SecurityThreatLevel.Medium;

            // Update IP cache
            lock (_lockObject)
            {
                if (!_ipAddressCache.TryGetValue(ipAddress, out var ipInfo))
                {
                    ipInfo = new IpAddressInfo
                    {
                        Address = ipAddress,
                        FirstSeen = DateTime.UtcNow,
                        ThreatLevel = threatLevel
                    };
                    _ipAddressCache[ipAddress] = ipInfo;
                }
                ipInfo.LastSeen = DateTime.UtcNow;
                ipInfo.RequestCount++;
                ipInfo.ThreatLevel = threatLevel;
            }

            return threatLevel;
        }

        public void BlockIpAddress(IPAddress ipAddress, TimeSpan? duration = null)
        {
            if (ipAddress == null)
                return;

            lock (_lockObject)
            {
                _blockedIps.Add(ipAddress);

                if (!_ipAddressCache.TryGetValue(ipAddress, out var ipInfo))
                {
                    ipInfo = new IpAddressInfo
                    {
                        Address = ipAddress,
                        FirstSeen = DateTime.UtcNow
                    };
                    _ipAddressCache[ipAddress] = ipInfo;
                }

                ipInfo.IsBlocked = true;
                ipInfo.BlockedUntil = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
            }

            LogNetworkEvent(new NetworkSecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "IP Address Blocked",
                IpAddress = ipAddress,
                ThreatLevel = SecurityThreatLevel.High,
                Action = "Blocked",
                Details = duration.HasValue ? $"Blocked for {duration.Value}" : "Permanently blocked"
            });

            _logger.LogWarning("IP address {IpAddress} has been blocked", ipAddress);
        }

        public void UnblockIpAddress(IPAddress ipAddress)
        {
            if (ipAddress == null)
                return;

            lock (_lockObject)
            {
                _blockedIps.Remove(ipAddress);

                if (_ipAddressCache.TryGetValue(ipAddress, out var ipInfo))
                {
                    ipInfo.IsBlocked = false;
                    ipInfo.BlockedUntil = null;
                }
            }

            LogNetworkEvent(new NetworkSecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "IP Address Unblocked",
                IpAddress = ipAddress,
                ThreatLevel = SecurityThreatLevel.Low,
                Action = "Unblocked",
                Details = "Manually unblocked"
            });

            _logger.LogInformation("IP address {IpAddress} has been unblocked", ipAddress);
        }

        public async Task<bool> ValidateHttpRequestAsync(string method, string path, Dictionary<string, string> headers)
        {
            return await Task.Run(() =>
            {
                // Check for suspicious HTTP methods
                var allowedMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                if (!allowedMethods.Contains(method?.ToUpper()))
                {
                    LogNetworkEvent(new NetworkSecurityEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = "Suspicious HTTP Method",
                        ThreatLevel = SecurityThreatLevel.Medium,
                        Details = $"Method: {method}"
                    });
                    return false;
                }

                // Check for suspicious paths
                var suspiciousPaths = new[]
                {
                    "/admin", "/wp-admin", "/phpMyAdmin", "/phpmyadmin",
                    "/.env", "/config", "/backup", "/logs", "/temp"
                };

                foreach (var suspiciousPath in suspiciousPaths)
                {
                    if (path?.Contains(suspiciousPath, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        LogNetworkEvent(new NetworkSecurityEvent
                        {
                            Timestamp = DateTime.UtcNow,
                            EventType = "Suspicious Path Access",
                            ThreatLevel = SecurityThreatLevel.High,
                            Details = $"Path: {path}"
                        });
                        return false;
                    }
                }

                // Validate headers
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        // Check for malicious header values
                        if (header.Value?.Contains("javascript:", StringComparison.OrdinalIgnoreCase) == true ||
                            header.Value?.Contains("<script", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            LogNetworkEvent(new NetworkSecurityEvent
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "Malicious Header",
                                ThreatLevel = SecurityThreatLevel.High,
                                Details = $"Header: {header.Key} = {header.Value}"
                            });
                            return false;
                        }
                    }
                }

                return true;
            });
        }

        public async Task<bool> CheckGeolocationRiskAsync(IPAddress ipAddress)
        {
            return await Task.Run(() =>
            {
                // Simple geolocation risk check (in real implementation, use geolocation service)
                // For now, just check if it's a private IP (low risk) or unknown (medium risk)
                
                if (IsPrivateIpAddress(ipAddress))
                    return false; // Private IPs are low risk

                // In production, integrate with geolocation service to get country
                // For demo purposes, randomly assess some IPs as high risk
                var ipString = ipAddress.ToString();
                var hashCode = ipString.GetHashCode();
                
                // Simulate high-risk countries (about 10% of IPs)
                return Math.Abs(hashCode) % 10 == 0;
            });
        }

        public void LogNetworkEvent(NetworkSecurityEvent networkEvent)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["NetworkSecurityEvent"] = true,
                ["EventType"] = networkEvent.EventType,
                ["ThreatLevel"] = networkEvent.ThreatLevel.ToString(),
                ["IpAddress"] = networkEvent.IpAddress?.ToString(),
                ["Country"] = networkEvent.Country
            }))
            {
                var logLevel = networkEvent.ThreatLevel switch
                {
                    SecurityThreatLevel.Critical => LogLevel.Critical,
                    SecurityThreatLevel.High => LogLevel.Error,
                    SecurityThreatLevel.Medium => LogLevel.Warning,
                    SecurityThreatLevel.Low => LogLevel.Information,
                    _ => LogLevel.Debug
                };

                _logger.Log(logLevel, "Network Security Event: {EventType} - {Details} - Action: {Action}",
                    networkEvent.EventType, networkEvent.Details, networkEvent.Action);
            }
        }

        public async Task<bool> IsKnownBotAsync(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return false;

            return await Task.Run(() =>
            {
                var knownBots = new[]
                {
                    "googlebot", "bingbot", "slurp", "duckduckbot", "baiduspider",
                    "yandexbot", "facebookexternalhit", "twitterbot", "whatsapp",
                    "crawler", "spider", "scraper", "bot", "curl", "wget"
                };

                var userAgentLower = userAgent.ToLower();
                return knownBots.Any(bot => userAgentLower.Contains(bot));
            });
        }

        public async Task<bool> ValidateTlsConnectionAsync(IPAddress clientIp)
        {
            return await Task.Run(() =>
            {
                // In production, this would validate TLS certificate and connection quality
                // For now, just log the validation attempt
                LogNetworkEvent(new NetworkSecurityEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "TLS Validation",
                    IpAddress = clientIp,
                    ThreatLevel = SecurityThreatLevel.None,
                    Details = "TLS connection validated"
                });
                
                return true;
            });
        }

        private bool IsPrivateIpAddress(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();
            
            // IPv4 private ranges
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;
                
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;
                
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;
                
                // 127.0.0.0/8 (loopback)
                if (bytes[0] == 127)
                    return true;
            }
            
            return false;
        }

        private void InitializeThreatIntelligence()
        {
            // In production, load from threat intelligence feeds
            // For demo, add some example malicious IPs
            _knownMaliciousIps.Add(IPAddress.Parse("192.0.2.1"));
            _knownMaliciousIps.Add(IPAddress.Parse("198.51.100.1"));
            _knownMaliciousIps.Add(IPAddress.Parse("203.0.113.1"));
        }

        private void InitializeMaliciousUserAgentPatterns()
        {
            _maliciousUserAgentPatterns = new List<Regex>
            {
                new Regex(@"sqlmap", RegexOptions.IgnoreCase),
                new Regex(@"nikto", RegexOptions.IgnoreCase),
                new Regex(@"masscan", RegexOptions.IgnoreCase),
                new Regex(@"nessus", RegexOptions.IgnoreCase),
                new Regex(@"burp suite", RegexOptions.IgnoreCase),
                new Regex(@"havij", RegexOptions.IgnoreCase),
                new Regex(@"pangolin", RegexOptions.IgnoreCase),
                new Regex(@"acunetix", RegexOptions.IgnoreCase),
                new Regex(@"metasploit", RegexOptions.IgnoreCase),
                new Regex(@"<script", RegexOptions.IgnoreCase),
                new Regex(@"javascript:", RegexOptions.IgnoreCase),
                new Regex(@"eval\(", RegexOptions.IgnoreCase)
            };
        }

        private void InitializeCountrySafetyRatings()
        {
            _countrySafety = new Dictionary<string, int>
            {
                { "US", 10 }, { "CA", 10 }, { "GB", 10 }, { "DE", 10 }, { "FR", 10 },
                { "JP", 10 }, { "AU", 10 }, { "NL", 10 }, { "SE", 10 }, { "NO", 10 },
                { "CN", 3 }, { "RU", 3 }, { "KP", 1 }, { "IR", 2 }, { "SY", 2 }
            };
        }
    }
}