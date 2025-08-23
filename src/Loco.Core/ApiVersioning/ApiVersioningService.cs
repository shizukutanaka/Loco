using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Loco.Core.ApiVersioning
{
    public interface IApiVersioningService
    {
        ApiVersion ParseVersion(string versionString);
        ApiVersion GetLatestVersion();
        ApiVersion GetDefaultVersion();
        bool IsVersionSupported(ApiVersion version);
        bool IsVersionDeprecated(ApiVersion version);
        List<ApiVersion> GetSupportedVersions();
        ApiVersionNegotiationResult NegotiateVersion(string requestedVersion, string acceptHeader);
    }

    public class ApiVersioningService : IApiVersioningService
    {
        private readonly ILogger<ApiVersioningService> _logger;
        private readonly List<ApiVersionConfiguration> _versions;
        private readonly ApiVersioningOptions _options;

        public ApiVersioningService(ILogger<ApiVersioningService> logger, ApiVersioningOptions options = null)
        {
            _logger = logger;
            _options = options ?? new ApiVersioningOptions();
            _versions = InitializeVersions();
        }

        public ApiVersion ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            // Try different version formats
            // Format: v1, v1.0, v1.0.0, 1.0, 2024-01-01
            
            // Numeric version (v1, v1.0, 1.0, etc.)
            var numericMatch = Regex.Match(versionString, @"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:-([\w\-\.]+))?$");
            if (numericMatch.Success)
            {
                var major = int.Parse(numericMatch.Groups[1].Value);
                var minor = numericMatch.Groups[2].Success ? int.Parse(numericMatch.Groups[2].Value) : 0;
                var patch = numericMatch.Groups[3].Success ? int.Parse(numericMatch.Groups[3].Value) : 0;
                var preRelease = numericMatch.Groups[4].Success ? numericMatch.Groups[4].Value : null;

                return new ApiVersion
                {
                    Major = major,
                    Minor = minor,
                    Patch = patch,
                    PreRelease = preRelease
                };
            }

            // Date version (2024-01-01)
            var dateMatch = Regex.Match(versionString, @"^(\d{4})-(\d{2})-(\d{2})$");
            if (dateMatch.Success)
            {
                var year = int.Parse(dateMatch.Groups[1].Value);
                var month = int.Parse(dateMatch.Groups[2].Value);
                var day = int.Parse(dateMatch.Groups[3].Value);

                return new ApiVersion
                {
                    Major = year,
                    Minor = month,
                    Patch = day,
                    IsDateVersion = true
                };
            }

            _logger.LogWarning("Unable to parse version string: {Version}", versionString);
            return null;
        }

        public ApiVersion GetLatestVersion()
        {
            return _versions
                .Where(v => v.Status == VersionStatus.Current)
                .Select(v => v.Version)
                .OrderByDescending(v => v)
                .FirstOrDefault();
        }

        public ApiVersion GetDefaultVersion()
        {
            return _options.DefaultVersion ?? GetLatestVersion();
        }

        public bool IsVersionSupported(ApiVersion version)
        {
            if (version == null) return false;

            return _versions.Any(v => 
                v.Version.Equals(version) && 
                v.Status != VersionStatus.Retired);
        }

        public bool IsVersionDeprecated(ApiVersion version)
        {
            if (version == null) return false;

            var config = _versions.FirstOrDefault(v => v.Version.Equals(version));
            return config?.Status == VersionStatus.Deprecated;
        }

        public List<ApiVersion> GetSupportedVersions()
        {
            return _versions
                .Where(v => v.Status != VersionStatus.Retired)
                .Select(v => v.Version)
                .OrderBy(v => v)
                .ToList();
        }

        public ApiVersionNegotiationResult NegotiateVersion(string requestedVersion, string acceptHeader)
        {
            var result = new ApiVersionNegotiationResult();

            // Try explicit version first
            if (!string.IsNullOrWhiteSpace(requestedVersion))
            {
                var version = ParseVersion(requestedVersion);
                if (version != null)
                {
                    if (IsVersionSupported(version))
                    {
                        result.Version = version;
                        result.Success = true;
                        result.IsDeprecated = IsVersionDeprecated(version);
                        
                        if (result.IsDeprecated)
                        {
                            var config = _versions.First(v => v.Version.Equals(version));
                            result.DeprecationInfo = new DeprecationInfo
                            {
                                Message = config.DeprecationMessage,
                                SunsetDate = config.SunsetDate
                            };
                        }
                        
                        return result;
                    }

                    result.Error = $"Version {version} is not supported";
                    result.SuggestedVersions = GetSupportedVersions();
                    return result;
                }
            }

            // Try Accept header
            if (!string.IsNullOrWhiteSpace(acceptHeader))
            {
                var mediaType = ParseMediaTypeWithVersion(acceptHeader);
                if (mediaType?.Version != null)
                {
                    var version = ParseVersion(mediaType.Version);
                    if (version != null && IsVersionSupported(version))
                    {
                        result.Version = version;
                        result.Success = true;
                        result.IsDeprecated = IsVersionDeprecated(version);
                        return result;
                    }
                }
            }

            // Use default version
            if (_options.AssumeDefaultVersionWhenUnspecified)
            {
                result.Version = GetDefaultVersion();
                result.Success = true;
                result.IsDefault = true;
                return result;
            }

            result.Error = "No API version specified";
            result.SuggestedVersions = GetSupportedVersions();
            return result;
        }

        private MediaTypeWithVersion ParseMediaTypeWithVersion(string acceptHeader)
        {
            // Parse Accept header like: application/vnd.api+json;version=1.0
            var match = Regex.Match(acceptHeader, @"application/vnd\.[\w\.]+\+?([\w]+)?(?:;version=([^,;]+))?");
            if (match.Success && match.Groups[2].Success)
            {
                return new MediaTypeWithVersion
                {
                    MediaType = match.Groups[0].Value,
                    Version = match.Groups[2].Value
                };
            }

            return null;
        }

        private List<ApiVersionConfiguration> InitializeVersions()
        {
            var versions = new List<ApiVersionConfiguration>();

            // Define supported versions
            versions.Add(new ApiVersionConfiguration
            {
                Version = new ApiVersion { Major = 1, Minor = 0 },
                Status = VersionStatus.Deprecated,
                DeprecationMessage = "Version 1.0 is deprecated. Please upgrade to version 2.0 or later.",
                SunsetDate = new DateTime(2025, 12, 31)
            });

            versions.Add(new ApiVersionConfiguration
            {
                Version = new ApiVersion { Major = 1, Minor = 1 },
                Status = VersionStatus.Supported
            });

            versions.Add(new ApiVersionConfiguration
            {
                Version = new ApiVersion { Major = 2, Minor = 0 },
                Status = VersionStatus.Current
            });

            versions.Add(new ApiVersionConfiguration
            {
                Version = new ApiVersion { Major = 2, Minor = 1 },
                Status = VersionStatus.Current
            });

            versions.Add(new ApiVersionConfiguration
            {
                Version = new ApiVersion { Major = 3, Minor = 0, PreRelease = "beta" },
                Status = VersionStatus.Preview
            });

            return versions;
        }

        private class MediaTypeWithVersion
        {
            public string MediaType { get; set; }
            public string Version { get; set; }
        }
    }

    public class ApiVersion : IComparable<ApiVersion>, IEquatable<ApiVersion>
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string PreRelease { get; set; }
        public bool IsDateVersion { get; set; }

        public override string ToString()
        {
            if (IsDateVersion)
            {
                return $"{Major:D4}-{Minor:D2}-{Patch:D2}";
            }

            var version = $"{Major}.{Minor}";
            if (Patch > 0)
                version += $".{Patch}";
            if (!string.IsNullOrEmpty(PreRelease))
                version += $"-{PreRelease}";
            
            return version;
        }

        public int CompareTo(ApiVersion other)
        {
            if (other == null) return 1;

            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;

            var patchComparison = Patch.CompareTo(other.Patch);
            if (patchComparison != 0) return patchComparison;

            // Pre-release versions are considered less than release versions
            if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
                return 1;
            if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
                return -1;

            return string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(ApiVersion other)
        {
            if (other == null) return false;
            return Major == other.Major && 
                   Minor == other.Minor && 
                   Patch == other.Patch && 
                   PreRelease == other.PreRelease;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ApiVersion);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Major, Minor, Patch, PreRelease);
        }

        public static bool operator ==(ApiVersion left, ApiVersion right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(ApiVersion left, ApiVersion right)
        {
            return !(left == right);
        }

        public static bool operator <(ApiVersion left, ApiVersion right)
        {
            if (left is null) return right is not null;
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(ApiVersion left, ApiVersion right)
        {
            if (left is null) return false;
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(ApiVersion left, ApiVersion right)
        {
            if (left is null) return true;
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(ApiVersion left, ApiVersion right)
        {
            if (left is null) return right is null;
            return left.CompareTo(right) >= 0;
        }
    }

    public class ApiVersionConfiguration
    {
        public ApiVersion Version { get; set; }
        public VersionStatus Status { get; set; }
        public string DeprecationMessage { get; set; }
        public DateTime? SunsetDate { get; set; }
    }

    public enum VersionStatus
    {
        Preview,
        Current,
        Supported,
        Deprecated,
        Retired
    }

    public class ApiVersioningOptions
    {
        public ApiVersion DefaultVersion { get; set; } = new ApiVersion { Major = 2, Minor = 0 };
        public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;
        public bool ReportApiVersions { get; set; } = true;
        public string HeaderName { get; set; } = "X-API-Version";
        public string QueryStringParameterName { get; set; } = "api-version";
    }

    public class ApiVersionNegotiationResult
    {
        public bool Success { get; set; }
        public ApiVersion Version { get; set; }
        public string Error { get; set; }
        public bool IsDefault { get; set; }
        public bool IsDeprecated { get; set; }
        public DeprecationInfo DeprecationInfo { get; set; }
        public List<ApiVersion> SuggestedVersions { get; set; }
    }

    public class DeprecationInfo
    {
        public string Message { get; set; }
        public DateTime? SunsetDate { get; set; }
    }
}