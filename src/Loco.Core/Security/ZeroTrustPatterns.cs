#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security;

/// <summary>
/// Zero-Trust Security Architecture Patterns
/// Identity federation, temporary tokens, workload verification, never trust always verify
/// </summary>

/// <summary>
/// Workload identity configuration
/// </summary>
public class WorkloadIdentity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("serviceAccount")]
    public string ServiceAccount { get; set; } = string.Empty;

    [JsonPropertyName("cloudProvider")]
    public string CloudProvider { get; set; } = string.Empty; // AWS, Azure, GCP

    [JsonPropertyName("principalArn")]
    public string PrincipalArn { get; set; } = string.Empty;

    [JsonPropertyName("trustedDomains")]
    public List<string> TrustedDomains { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Temporary security token
/// </summary>
public class TemporaryToken
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workloadId")]
    public string WorkloadId { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("issuedAt")]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

    [JsonPropertyName("scope")]
    public List<string> Scope { get; set; } = new(); // Requested permissions

    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("audience")]
    public List<string> Audience { get; set; } = new();

    [JsonPropertyName("revoked")]
    public bool Revoked { get; set; }
}

/// <summary>
/// Access policy with fine-grained controls
/// </summary>
public class ZeroTrustAccessPolicy
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("principalType")]
    public string PrincipalType { get; set; } = string.Empty; // User, Workload, Service

    [JsonPropertyName("principal")]
    public string Principal { get; set; } = string.Empty;

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("actions")]
    public List<string> Actions { get; set; } = new(); // read, write, delete

    [JsonPropertyName("conditions")]
    public List<PolicyCondition> Conditions { get; set; } = new();

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = "Allow"; // Allow, Deny

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Policy condition for attribute-based access control (ABAC)
/// </summary>
public class PolicyCondition
{
    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = string.Empty; // ip, deviceStatus, timeRange, location

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty; // equals, notEquals, in, notIn, matches

    [JsonPropertyName("value")]
    public List<string> Value { get; set; } = new();

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// Federated identity provider
/// </summary>
public class IdentityProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // OIDC, SAML, AWS STS, Azure AD

    [JsonPropertyName("issuerUrl")]
    public string IssuerUrl { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("discoveryEndpoint")]
    public string DiscoveryEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("trustDomain")]
    public string TrustDomain { get; set; } = string.Empty;

    [JsonPropertyName("spiffeEnabled")]
    public bool SpiffeEnabled { get; set; } = true;

    [JsonPropertyName("mtlsRequired")]
    public bool MtlsRequired { get; set; } = true;

    [JsonPropertyName("lastHealthCheck")]
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device posture check
/// </summary>
public class DevicePosture
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty; // Desktop, Mobile, Tablet

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("diskEncryption")]
    public bool DiskEncryption { get; set; }

    [JsonPropertyName("firewallEnabled")]
    public bool FirewallEnabled { get; set; }

    [JsonPropertyName("antimalwareStatus")]
    public string AntimalwareStatus { get; set; } = string.Empty; // Good, Poor, Unknown

    [JsonPropertyName("lastSecurityUpdate")]
    public DateTime LastSecurityUpdate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("complianceScore")]
    public int ComplianceScore { get; set; } = 100; // 0-100

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High, Critical
}

/// <summary>
/// Verification result from zero-trust check
/// </summary>
public class VerificationResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("verificationTime")]
    public DateTime VerificationTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("principal")]
    public string Principal { get; set; } = string.Empty;

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("allowed")]
    public bool Allowed { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("checksPerformed")]
    public List<string> ChecksPerformed { get; set; } = new(); // IdentityVerification, DeviceComplianceCheck, NetworkCheck

    [JsonPropertyName("trustScore")]
    public double TrustScore { get; set; } = 0; // 0-1
}

/// <summary>
/// Zero-trust security engine
/// </summary>
public class ZeroTrustEngine
{
    private readonly Dictionary<string, WorkloadIdentity> _workloads = new();
    private readonly Dictionary<string, TemporaryToken> _tokens = new();
    private readonly List<ZeroTrustAccessPolicy> _policies = new();
    private readonly List<IdentityProvider> _idps = new();
    private readonly Dictionary<string, DevicePosture> _devicePostures = new();
    private readonly List<VerificationResult> _auditLog = new();
    private readonly ILogger<ZeroTrustEngine> _logger;

    public ZeroTrustEngine(ILogger<ZeroTrustEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register workload identity
    /// </summary>
    public async Task RegisterWorkloadIdentityAsync(WorkloadIdentity identity)
    {
        _workloads[identity.Id] = identity;

        _logger.LogInformation(
            "Registered workload identity: {Name} ({Namespace}/{ServiceAccount})",
            identity.Name,
            identity.Namespace,
            identity.ServiceAccount);
    }

    /// <summary>
    /// Issue temporary token to workload
    /// </summary>
    public async Task<TemporaryToken> IssueTemporaryTokenAsync(
        string workloadId,
        List<string> scopes,
        TimeSpan? duration = null)
    {
        if (!_workloads.TryGetValue(workloadId, out var workload))
            throw new InvalidOperationException("Workload not found");

        var token = new TemporaryToken
        {
            WorkloadId = workloadId,
            Token = GenerateSecureToken(),
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(duration?.TotalMinutes ?? 15),
            Scope = scopes,
            Subject = workload.ServiceAccount,
            Issuer = "zero-trust-engine"
        };

        _tokens[token.Id] = token;

        _logger.LogInformation(
            "Issued temporary token for workload {WorkloadId}, expires in {Minutes} minutes",
            workloadId,
            (token.ExpiresAt - DateTime.UtcNow).TotalMinutes);

        return token;
    }

    /// <summary>
    /// Register identity provider
    /// </summary>
    public async Task RegisterIdentityProviderAsync(IdentityProvider idp)
    {
        _idps.Add(idp);

        _logger.LogInformation(
            "Registered identity provider: {Name} ({Type})",
            idp.Name,
            idp.Type);
    }

    /// <summary>
    /// Add access policy
    /// </summary>
    public async Task AddAccessPolicyAsync(ZeroTrustAccessPolicy policy)
    {
        _policies.Add(policy);

        _logger.LogInformation(
            "Added access policy: {Name} - {Principal} → {Resource} ({Actions})",
            policy.Name,
            policy.Principal,
            policy.Resource,
            string.Join(", ", policy.Actions));
    }

    /// <summary>
    /// Verify access request (never trust, always verify)
    /// </summary>
    public async Task<VerificationResult> VerifyAccessAsync(
        string requestId,
        string principal,
        string resource,
        string action,
        DevicePosture? devicePosture = null)
    {
        var result = new VerificationResult
        {
            RequestId = requestId,
            Principal = principal,
            Resource = resource,
            Action = action
        };

        // Check 1: Identity verification (token validity)
        result.ChecksPerformed.Add("IdentityVerification");
        var identityValid = VerifyIdentity(principal, out var trustScore);
        result.TrustScore += trustScore * 0.4;

        // Check 2: Device compliance check
        result.ChecksPerformed.Add("DeviceComplianceCheck");
        if (devicePosture != null)
        {
            var complianceValid = VerifyDeviceCompliance(devicePosture, out var complianceScore);
            result.TrustScore += complianceScore * 0.3;
        }

        // Check 3: Policy evaluation
        result.ChecksPerformed.Add("PolicyEvaluation");
        var policyAllows = EvaluatePolicy(principal, resource, action, result);
        result.TrustScore += policyAllows ? 0.3 : 0;

        // Final decision: all checks must pass
        result.Allowed = identityValid && policyAllows && (devicePosture == null || VerifyDeviceCompliance(devicePosture, out _));
        result.Reason = result.Allowed ? "All verification checks passed" : "One or more verification checks failed";

        _auditLog.Add(result);

        _logger.LogInformation(
            "Access verification: {Principal} → {Resource} ({Action}): {Decision} (TrustScore: {Score:F2})",
            principal,
            resource,
            action,
            result.Allowed ? "ALLOWED" : "DENIED",
            result.TrustScore);

        return result;
    }

    /// <summary>
    /// Register device for posture checking
    /// </summary>
    public async Task RegisterDeviceAsync(DevicePosture posture)
    {
        _devicePostures[posture.DeviceId] = posture;

        _logger.LogInformation(
            "Registered device: {DeviceId} - Compliance: {Score}%, Risk: {Risk}",
            posture.DeviceId,
            posture.ComplianceScore,
            posture.RiskLevel);
    }

    /// <summary>
    /// Get access policies for principal
    /// </summary>
    public List<ZeroTrustAccessPolicy> GetApplicablePolicies(string principal)
    {
        return _policies.Where(p => p.Principal == principal || p.Principal == "*")
            .OrderByDescending(p => p.Priority)
            .ToList();
    }

    /// <summary>
    /// Revoke token
    /// </summary>
    public async Task RevokeTokenAsync(string tokenId)
    {
        if (_tokens.TryGetValue(tokenId, out var token))
        {
            token.Revoked = true;
            _logger.LogWarning("Revoked token: {TokenId}", tokenId);
        }
    }

    /// <summary>
    /// Get zero-trust stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var recentResults = _auditLog.Where(r =>
            r.VerificationTime > DateTime.UtcNow.AddHours(-1)).ToList();

        return new()
        {
            ["workloads"] = _workloads.Count,
            ["activeTokens"] = _tokens.Values.Count(t => !t.Revoked && t.ExpiresAt > DateTime.UtcNow),
            ["policies"] = _policies.Count,
            ["identityProviders"] = _idps.Count,
            ["registeredDevices"] = _devicePostures.Count,
            ["accessVerificationsLastHour"] = recentResults.Count,
            ["allowedAccessLastHour"] = recentResults.Count(r => r.Allowed),
            ["deniedAccessLastHour"] = recentResults.Count(r => !r.Allowed)
        };
    }

    private bool VerifyIdentity(string principal, out double trustScore)
    {
        var token = _tokens.Values.FirstOrDefault(t =>
            t.Subject == principal && !t.Revoked && t.ExpiresAt > DateTime.UtcNow);

        trustScore = token != null ? 1.0 : 0.0;
        return token != null;
    }

    private bool VerifyDeviceCompliance(DevicePosture posture, out double score)
    {
        score = posture.ComplianceScore / 100.0;
        return posture.ComplianceScore >= 80 && posture.RiskLevel == "Low";
    }

    private bool EvaluatePolicy(string principal, string resource, string action, VerificationResult result)
    {
        var applicablePolicies = GetApplicablePolicies(principal);

        foreach (var policy in applicablePolicies)
        {
            if (policy.Resource != resource && policy.Resource != "*")
                continue;

            if (!policy.Actions.Contains(action) && !policy.Actions.Contains("*"))
                continue;

            if (!EvaluateConditions(policy.Conditions, result))
                continue;

            return policy.Effect == "Allow";
        }

        return false; // Default deny
    }

    private bool EvaluateConditions(List<PolicyCondition> conditions, VerificationResult result)
    {
        foreach (var condition in conditions.Where(c => c.Required))
        {
            // Simplified condition evaluation
            // In production, would evaluate against actual request context
            if (condition.Attribute == "timeRange")
                if (!IsWithinTimeRange(condition.Value))
                    return false;
        }
        return true;
    }

    private bool IsWithinTimeRange(List<string> timeRange)
    {
        // Simplified time range check (0-24 hour format)
        var now = DateTime.UtcNow.Hour;
        if (timeRange.Count >= 2 && int.TryParse(timeRange[0], out var start) &&
            int.TryParse(timeRange[1], out var end))
        {
            return now >= start && now < end;
        }
        return true;
    }

    private string GenerateSecureToken()
    {
        using var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
        byte[] tokenBuffer = new byte[32];
        rng.GetBytes(tokenBuffer);
        return Convert.ToBase64String(tokenBuffer);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ZeroTrustExtensions
{
    public static IServiceCollection AddZeroTrustSecurity(this IServiceCollection services)
    {
        services.AddSingleton<ZeroTrustEngine>();
        return services;
    }
}
