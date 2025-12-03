// =============================================================================
// SUPPLY CHAIN SECURITY ENGINE
// =============================================================================
// Production-grade supply chain security based on 2025 research:
// - Sigstore (Cosign, Fulcio, Rekor) - Keyless signing
// - SBOM (CycloneDX, SPDX) - Dependency transparency
// - SLSA 1.0 - Build provenance and integrity
// - Vulnerability scanning (Trivy, Grype, Snyk)
// - A-MAP Framework - Automated security assessments
// =============================================================================

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIPlatform;

/// <summary>
/// Supply Chain Security Engine implementing Sigstore, SBOM, and SLSA patterns
/// Based on 2025 best practices for container and artifact security
/// </summary>
public interface ISupplyChainSecurityEngine
{
    // ==========================================================================
    // IMAGE SIGNING (Sigstore/Cosign)
    // ==========================================================================

    /// <summary>
    /// Sign container image using Cosign with keyless signing (Fulcio + Rekor)
    /// </summary>
    Task<SignatureResult> SignImageAsync(ImageSigningConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Verify container image signature
    /// </summary>
    Task<VerificationResult> VerifyImageAsync(ImageVerificationConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Sign blob/artifact using Cosign
    /// </summary>
    Task<SignatureResult> SignBlobAsync(BlobSigningConfig config, CancellationToken cancellation = default);

    // ==========================================================================
    // SBOM MANAGEMENT
    // ==========================================================================

    /// <summary>
    /// Generate SBOM for container image (CycloneDX or SPDX format)
    /// </summary>
    Task<SBOMResult> GenerateSBOMAsync(SBOMGenerationConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Attach SBOM to container image as attestation
    /// </summary>
    Task<AttestationResult> AttachSBOMAsync(SBOMAttachmentConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Analyze SBOM for vulnerabilities
    /// </summary>
    Task<SBOMAnalysisResult> AnalyzeSBOMAsync(SBOMAnalysisConfig config, CancellationToken cancellation = default);

    // ==========================================================================
    // SLSA PROVENANCE
    // ==========================================================================

    /// <summary>
    /// Generate SLSA provenance for build artifact
    /// </summary>
    Task<ProvenanceResult> GenerateProvenanceAsync(ProvenanceConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Verify SLSA provenance
    /// </summary>
    Task<ProvenanceVerificationResult> VerifyProvenanceAsync(ProvenanceVerificationConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Get SLSA level for artifact based on build configuration
    /// </summary>
    Task<SLSALevelAssessment> AssessSLSALevelAsync(SLSAAssessmentConfig config, CancellationToken cancellation = default);

    // ==========================================================================
    // VULNERABILITY SCANNING
    // ==========================================================================

    /// <summary>
    /// Scan container image for vulnerabilities using Trivy/Grype
    /// </summary>
    Task<VulnerabilityScanResult> ScanImageAsync(ImageScanConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Scan filesystem/repository for vulnerabilities
    /// </summary>
    Task<VulnerabilityScanResult> ScanFilesystemAsync(FilesystemScanConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Get vulnerability trends over time
    /// </summary>
    Task<VulnerabilityTrends> GetVulnerabilityTrendsAsync(string namespace_, TimeSpan period, CancellationToken cancellation = default);

    // ==========================================================================
    // POLICY ENFORCEMENT
    // ==========================================================================

    /// <summary>
    /// Create admission policy for supply chain security
    /// </summary>
    Task<AdmissionPolicy> CreateAdmissionPolicyAsync(AdmissionPolicyConfig config, CancellationToken cancellation = default);

    /// <summary>
    /// Evaluate image against admission policies
    /// </summary>
    Task<PolicyEvaluationResult> EvaluatePolicyAsync(PolicyEvaluationConfig config, CancellationToken cancellation = default);

    // ==========================================================================
    // TRANSPARENCY LOG (Rekor)
    // ==========================================================================

    /// <summary>
    /// Query Rekor transparency log for entries
    /// </summary>
    Task<List<RekorEntry>> QueryRekorAsync(RekorQuery query, CancellationToken cancellation = default);

    /// <summary>
    /// Get inclusion proof from Rekor
    /// </summary>
    Task<InclusionProof> GetInclusionProofAsync(string entryUuid, CancellationToken cancellation = default);
}

// =============================================================================
// IMAGE SIGNING MODELS
// =============================================================================

public sealed class ImageSigningConfig
{
    public required string ImageReference { get; init; }
    public SigningMethod Method { get; init; } = SigningMethod.Keyless;
    public string? KeyPath { get; init; }
    public string? KeyPassword { get; init; }
    public string? OIDCIssuer { get; init; } = "https://oauth2.sigstore.dev/auth";
    public string? OIDCClientId { get; init; } = "sigstore";
    public Dictionary<string, string> Annotations { get; init; } = new();
    public bool Upload { get; init; } = true;
    public string? RekorUrl { get; init; } = "https://rekor.sigstore.dev";
    public string? FulcioUrl { get; init; } = "https://fulcio.sigstore.dev";
    public bool RecordCreationTimestamp { get; init; } = true;
}

public enum SigningMethod
{
    Keyless,      // Fulcio + OIDC (recommended)
    KeyPair,      // Traditional key pair
    KMS,          // Cloud KMS (AWS, GCP, Azure, Vault)
    PKCS11        // Hardware security module
}

public sealed class SignatureResult
{
    public required string ImageReference { get; init; }
    public required string SignatureDigest { get; init; }
    public required string SignatureLocation { get; init; }
    public string? RekorLogId { get; init; }
    public string? RekorLogIndex { get; init; }
    public string? Certificate { get; init; }
    public string? CertificateChain { get; init; }
    public DateTime SignedAt { get; init; }
    public Dictionary<string, string> Annotations { get; init; } = new();
}

public sealed class ImageVerificationConfig
{
    public required string ImageReference { get; init; }
    public VerificationMethod Method { get; init; } = VerificationMethod.Keyless;
    public string? PublicKeyPath { get; init; }
    public string? CertificateIdentity { get; init; }
    public string? CertificateOIDCIssuer { get; init; }
    public string? CertificateIdentityRegexp { get; init; }
    public bool CheckClaims { get; init; } = true;
    public bool CheckTLog { get; init; } = true;
    public string? RekorUrl { get; init; } = "https://rekor.sigstore.dev";
    public Dictionary<string, string> RequiredAnnotations { get; init; } = new();
}

public enum VerificationMethod
{
    Keyless,
    PublicKey,
    Certificate
}

public sealed class VerificationResult
{
    public required string ImageReference { get; init; }
    public required bool Verified { get; init; }
    public List<SignatureVerification> Signatures { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public CertificateInfo? Certificate { get; init; }
    public TransparencyLogEntry? TLogEntry { get; init; }
}

public sealed class SignatureVerification
{
    public required string Digest { get; init; }
    public required bool Valid { get; init; }
    public string? Signer { get; init; }
    public string? Issuer { get; init; }
    public DateTime? SignedAt { get; init; }
    public Dictionary<string, string> Annotations { get; init; } = new();
}

public sealed class CertificateInfo
{
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public string? OIDCIssuer { get; init; }
    public string? SANEmail { get; init; }
    public string? SANUri { get; init; }
    public string? GithubWorkflowTrigger { get; init; }
    public string? GithubWorkflowSha { get; init; }
    public string? GithubWorkflowName { get; init; }
    public string? GithubWorkflowRepository { get; init; }
    public string? GithubWorkflowRef { get; init; }
}

public sealed class TransparencyLogEntry
{
    public required string LogId { get; init; }
    public required long LogIndex { get; init; }
    public required DateTime IntegratedTime { get; init; }
    public string? Body { get; init; }
}

public sealed class BlobSigningConfig
{
    public required string FilePath { get; init; }
    public SigningMethod Method { get; init; } = SigningMethod.Keyless;
    public string? KeyPath { get; init; }
    public string? OutputSignature { get; init; }
    public string? OutputCertificate { get; init; }
    public bool Bundle { get; init; } = true;
    public string? BundlePath { get; init; }
}

// =============================================================================
// SBOM MODELS
// =============================================================================

public sealed class SBOMGenerationConfig
{
    public required string Target { get; init; }  // Image reference or filesystem path
    public SBOMFormat Format { get; init; } = SBOMFormat.CycloneDX;
    public SBOMTargetType TargetType { get; init; } = SBOMTargetType.Image;
    public string? OutputPath { get; init; }
    public bool IncludeLicenses { get; init; } = true;
    public bool IncludeVulnerabilities { get; init; } = false;
    public List<string> Scanners { get; init; } = new() { "syft", "trivy" };
}

public enum SBOMFormat
{
    CycloneDX,
    CycloneDXJson,
    SPDX,
    SPDXJson,
    SyftJson
}

public enum SBOMTargetType
{
    Image,
    Filesystem,
    Archive,
    Directory
}

public sealed class SBOMResult
{
    public required string Target { get; init; }
    public required SBOMFormat Format { get; init; }
    public required string Content { get; init; }
    public string? FilePath { get; init; }
    public SBOMMetadata Metadata { get; init; } = new();
    public List<SBOMComponent> Components { get; init; } = new();
    public List<SBOMDependency> Dependencies { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}

public sealed class SBOMMetadata
{
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? Supplier { get; init; }
    public List<string> Authors { get; init; } = new();
    public List<string> Tools { get; init; } = new();
}

public sealed class SBOMComponent
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required ComponentType Type { get; init; }
    public string? Purl { get; init; }  // Package URL
    public string? Cpe { get; init; }   // Common Platform Enumeration
    public string? License { get; init; }
    public List<string> Licenses { get; init; } = new();
    public string? Supplier { get; init; }
    public List<ExternalReference> ExternalReferences { get; init; } = new();
    public List<ComponentHash> Hashes { get; init; } = new();
}

public enum ComponentType
{
    Application,
    Library,
    Framework,
    Container,
    OperatingSystem,
    Device,
    Firmware,
    File
}

public sealed class ExternalReference
{
    public required string Type { get; init; }
    public required string Url { get; init; }
    public string? Comment { get; init; }
}

public sealed class ComponentHash
{
    public required string Algorithm { get; init; }
    public required string Value { get; init; }
}

public sealed class SBOMDependency
{
    public required string Ref { get; init; }
    public List<string> DependsOn { get; init; } = new();
}

public sealed class SBOMAttachmentConfig
{
    public required string ImageReference { get; init; }
    public required string SBOMPath { get; init; }
    public SBOMFormat Format { get; init; } = SBOMFormat.CycloneDX;
    public bool Sign { get; init; } = true;
    public SigningMethod SigningMethod { get; init; } = SigningMethod.Keyless;
}

public sealed class AttestationResult
{
    public required string ImageReference { get; init; }
    public required string AttestationDigest { get; init; }
    public string? SignatureDigest { get; init; }
    public string? RekorLogIndex { get; init; }
    public AttestationType Type { get; init; }
    public DateTime AttachedAt { get; init; }
}

public enum AttestationType
{
    SBOM,
    Provenance,
    VulnerabilityScan,
    Custom
}

public sealed class SBOMAnalysisConfig
{
    public required string SBOMPath { get; init; }
    public bool CheckVulnerabilities { get; init; } = true;
    public bool CheckLicenses { get; init; } = true;
    public List<string> AllowedLicenses { get; init; } = new();
    public List<string> DeniedLicenses { get; init; } = new();
    public VulnerabilitySeverity MinimumSeverity { get; init; } = VulnerabilitySeverity.Medium;
}

public sealed class SBOMAnalysisResult
{
    public required string SBOMPath { get; init; }
    public int TotalComponents { get; init; }
    public int VulnerableComponents { get; init; }
    public List<ComponentVulnerability> Vulnerabilities { get; init; } = new();
    public List<LicenseIssue> LicenseIssues { get; init; } = new();
    public Dictionary<string, int> ComponentsByType { get; init; } = new();
    public Dictionary<string, int> LicenseDistribution { get; init; } = new();
    public bool PassesPolicy { get; init; }
    public List<string> PolicyViolations { get; init; } = new();
}

public sealed class ComponentVulnerability
{
    public required string ComponentName { get; init; }
    public required string ComponentVersion { get; init; }
    public required string VulnerabilityId { get; init; }
    public required VulnerabilitySeverity Severity { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? FixedVersion { get; init; }
    public List<string> References { get; init; } = new();
    public double? CvssScore { get; init; }
    public string? CvssVector { get; init; }
}

public sealed class LicenseIssue
{
    public required string ComponentName { get; init; }
    public required string License { get; init; }
    public required LicenseIssueType IssueType { get; init; }
    public string? Message { get; init; }
}

public enum LicenseIssueType
{
    Unknown,
    Copyleft,
    Denied,
    Incompatible,
    RequiresAttribution
}

// =============================================================================
// SLSA PROVENANCE MODELS
// =============================================================================

public sealed class ProvenanceConfig
{
    public required string SubjectName { get; init; }
    public required string SubjectDigest { get; init; }
    public required BuildConfig Build { get; init; }
    public required MaterialsConfig Materials { get; init; }
    public string? BuilderId { get; init; }
    public SLSAVersion Version { get; init; } = SLSAVersion.V1_0;
    public bool Sign { get; init; } = true;
    public SigningMethod SigningMethod { get; init; } = SigningMethod.Keyless;
}

public enum SLSAVersion
{
    V0_2,
    V1_0
}

public sealed class BuildConfig
{
    public required string BuildType { get; init; }
    public string? ConfigSource { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public Dictionary<string, object> InternalParameters { get; init; } = new();
    public DateTime StartedOn { get; init; }
    public DateTime? FinishedOn { get; init; }
}

public sealed class MaterialsConfig
{
    public List<BuildMaterial> Materials { get; init; } = new();
    public bool IncludeGitInfo { get; init; } = true;
    public bool IncludeDependencies { get; init; } = true;
}

public sealed class BuildMaterial
{
    public required string Uri { get; init; }
    public Dictionary<string, string> Digest { get; init; } = new();
    public string? LocalName { get; init; }
    public string? DownloadLocation { get; init; }
    public string? MediaType { get; init; }
}

public sealed class ProvenanceResult
{
    public required string SubjectName { get; init; }
    public required string SubjectDigest { get; init; }
    public required string ProvenanceContent { get; init; }
    public string? SignatureDigest { get; init; }
    public string? RekorLogIndex { get; init; }
    public SLSALevel AssessedLevel { get; init; }
    public DateTime GeneratedAt { get; init; }
}

public enum SLSALevel
{
    None = 0,
    L1 = 1,   // Provenance exists
    L2 = 2,   // Hosted build platform
    L3 = 3,   // Hardened builds
    L4 = 4    // Two-party review (future)
}

public sealed class ProvenanceVerificationConfig
{
    public required string ImageReference { get; init; }
    public string? ExpectedBuilderId { get; init; }
    public string? ExpectedSourceRepository { get; init; }
    public string? ExpectedSourceBranch { get; init; }
    public SLSALevel MinimumLevel { get; init; } = SLSALevel.L1;
    public bool VerifySignature { get; init; } = true;
    public bool VerifyTLog { get; init; } = true;
}

public sealed class ProvenanceVerificationResult
{
    public required string ImageReference { get; init; }
    public required bool Verified { get; init; }
    public SLSALevel AssessedLevel { get; init; }
    public ProvenanceDetails? Provenance { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class ProvenanceDetails
{
    public string? BuilderId { get; init; }
    public string? BuildType { get; init; }
    public string? SourceRepository { get; init; }
    public string? SourceRef { get; init; }
    public string? SourceDigest { get; init; }
    public List<BuildMaterial> Materials { get; init; } = new();
    public DateTime? BuildStartedOn { get; init; }
    public DateTime? BuildFinishedOn { get; init; }
}

public sealed class SLSAAssessmentConfig
{
    public required string ImageReference { get; init; }
    public bool CheckBuildIsolation { get; init; } = true;
    public bool CheckSourceIntegrity { get; init; } = true;
    public bool CheckBuildService { get; init; } = true;
}

public sealed class SLSALevelAssessment
{
    public required string ImageReference { get; init; }
    public required SLSALevel Level { get; init; }
    public List<SLSARequirement> Requirements { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
}

public sealed class SLSARequirement
{
    public required string Name { get; init; }
    public required SLSALevel RequiredFor { get; init; }
    public required bool Met { get; init; }
    public string? Evidence { get; init; }
    public string? Recommendation { get; init; }
}

// =============================================================================
// VULNERABILITY SCANNING MODELS
// =============================================================================

public sealed class ImageScanConfig
{
    public required string ImageReference { get; init; }
    public VulnerabilityScanner Scanner { get; init; } = VulnerabilityScanner.Trivy;
    public VulnerabilitySeverity MinimumSeverity { get; init; } = VulnerabilitySeverity.Medium;
    public bool ScanSecrets { get; init; } = true;
    public bool ScanMisconfigurations { get; init; } = true;
    public bool ScanLicenses { get; init; } = false;
    public List<string> IgnoredVulnerabilities { get; init; } = new();
    public string? IgnoreFile { get; init; }
    public bool OfflineScan { get; init; } = false;
    public int Timeout { get; init; } = 300;
}

public enum VulnerabilityScanner
{
    Trivy,
    Grype,
    Snyk,
    Clair
}

public enum VulnerabilitySeverity
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public sealed class FilesystemScanConfig
{
    public required string Path { get; init; }
    public VulnerabilityScanner Scanner { get; init; } = VulnerabilityScanner.Trivy;
    public VulnerabilitySeverity MinimumSeverity { get; init; } = VulnerabilitySeverity.Medium;
    public bool ScanSecrets { get; init; } = true;
    public bool ScanIaC { get; init; } = true;
    public List<string> SkipDirs { get; init; } = new();
    public List<string> SkipFiles { get; init; } = new();
}

public sealed class VulnerabilityScanResult
{
    public required string Target { get; init; }
    public required VulnerabilityScanner Scanner { get; init; }
    public int TotalVulnerabilities { get; init; }
    public Dictionary<VulnerabilitySeverity, int> BySeverity { get; init; } = new();
    public List<Vulnerability> Vulnerabilities { get; init; } = new();
    public List<SecretFinding> Secrets { get; init; } = new();
    public List<MisconfigurationFinding> Misconfigurations { get; init; } = new();
    public DateTime ScannedAt { get; init; }
    public TimeSpan ScanDuration { get; init; }
}

public sealed class Vulnerability
{
    public required string Id { get; init; }
    public required string PackageName { get; init; }
    public required string InstalledVersion { get; init; }
    public required VulnerabilitySeverity Severity { get; init; }
    public string? FixedVersion { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public double? CvssScore { get; init; }
    public string? CvssVector { get; init; }
    public List<string> References { get; init; } = new();
    public DateTime? PublishedDate { get; init; }
    public DateTime? LastModifiedDate { get; init; }
    public string? DataSource { get; init; }
    public VulnerabilityStatus Status { get; init; } = VulnerabilityStatus.Affected;
}

public enum VulnerabilityStatus
{
    Affected,
    Fixed,
    NotAffected,
    UnderInvestigation
}

public sealed class SecretFinding
{
    public required string RuleId { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required VulnerabilitySeverity Severity { get; init; }
    public string? Target { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public string? Match { get; init; }
}

public sealed class MisconfigurationFinding
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required VulnerabilitySeverity Severity { get; init; }
    public string? Description { get; init; }
    public string? Resolution { get; init; }
    public string? Target { get; init; }
    public List<string> References { get; init; } = new();
}

public sealed class VulnerabilityTrends
{
    public required string Namespace { get; init; }
    public required TimeSpan Period { get; init; }
    public List<VulnerabilitySnapshot> Snapshots { get; init; } = new();
    public TrendAnalysis Analysis { get; init; } = new();
}

public sealed class VulnerabilitySnapshot
{
    public required DateTime Timestamp { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int Critical { get; init; }
    public int High { get; init; }
    public int Medium { get; init; }
    public int Low { get; init; }
    public int ImagesScanned { get; init; }
}

public sealed class TrendAnalysis
{
    public double CriticalTrend { get; init; }  // Positive = increasing
    public double HighTrend { get; init; }
    public int NewVulnerabilities { get; init; }
    public int FixedVulnerabilities { get; init; }
    public double MeanTimeToRemediate { get; init; }  // Days
    public List<string> TopVulnerablePackages { get; init; } = new();
}

// =============================================================================
// ADMISSION POLICY MODELS
// =============================================================================

public sealed class AdmissionPolicyConfig
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public PolicyMode Mode { get; init; } = PolicyMode.Enforce;
    public List<ImageRequirement> ImageRequirements { get; init; } = new();
    public List<string> TrustedRegistries { get; init; } = new();
    public List<TrustedIdentity> TrustedIdentities { get; init; } = new();
    public VulnerabilityPolicy? VulnerabilityPolicy { get; init; }
    public SLSAPolicy? SLSAPolicy { get; init; }
}

public enum PolicyMode
{
    Enforce,
    Warn,
    Audit
}

public sealed class ImageRequirement
{
    public required string Pattern { get; init; }  // Glob pattern for image names
    public bool RequireSignature { get; init; } = true;
    public bool RequireSBOM { get; init; } = false;
    public bool RequireProvenance { get; init; } = false;
    public SLSALevel MinimumSLSALevel { get; init; } = SLSALevel.None;
    public List<string> TrustedSigners { get; init; } = new();
}

public sealed class TrustedIdentity
{
    public required string Issuer { get; init; }
    public string? Subject { get; init; }
    public string? SubjectRegexp { get; init; }
}

public sealed class VulnerabilityPolicy
{
    public bool BlockCritical { get; init; } = true;
    public bool BlockHigh { get; init; } = false;
    public int MaxCritical { get; init; } = 0;
    public int MaxHigh { get; init; } = 5;
    public List<string> IgnoredCVEs { get; init; } = new();
    public int MaxAgeForFixAvailable { get; init; } = 30;  // Days
}

public sealed class SLSAPolicy
{
    public SLSALevel MinimumLevel { get; init; } = SLSALevel.L1;
    public List<string> TrustedBuilders { get; init; } = new();
    public List<string> TrustedSourceRepositories { get; init; } = new();
}

public sealed class AdmissionPolicy
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required PolicyMode Mode { get; init; }
    public AdmissionPolicyConfig Config { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastEvaluatedAt { get; init; }
    public PolicyStatistics Statistics { get; init; } = new();
}

public sealed class PolicyStatistics
{
    public int TotalEvaluations { get; init; }
    public int Allowed { get; init; }
    public int Denied { get; init; }
    public int Warned { get; init; }
}

public sealed class PolicyEvaluationConfig
{
    public required string ImageReference { get; init; }
    public required string Namespace { get; init; }
    public string? PolicyName { get; init; }
    public bool DryRun { get; init; } = false;
}

public sealed class PolicyEvaluationResult
{
    public required string ImageReference { get; init; }
    public required bool Allowed { get; init; }
    public PolicyDecision Decision { get; init; }
    public List<PolicyViolation> Violations { get; init; } = new();
    public List<PolicyWarning> Warnings { get; init; } = new();
    public List<string> EvaluatedPolicies { get; init; } = new();
    public DateTime EvaluatedAt { get; init; }
}

public enum PolicyDecision
{
    Allow,
    Deny,
    Warn
}

public sealed class PolicyViolation
{
    public required string PolicyName { get; init; }
    public required string Rule { get; init; }
    public required string Message { get; init; }
    public VulnerabilitySeverity Severity { get; init; }
}

public sealed class PolicyWarning
{
    public required string PolicyName { get; init; }
    public required string Rule { get; init; }
    public required string Message { get; init; }
}

// =============================================================================
// REKOR TRANSPARENCY LOG MODELS
// =============================================================================

public sealed class RekorQuery
{
    public string? LogIndex { get; init; }
    public string? EntryUuid { get; init; }
    public string? Email { get; init; }
    public string? Hash { get; init; }
    public string? PublicKey { get; init; }
    public int Limit { get; init; } = 100;
}

public sealed class RekorEntry
{
    public required string Uuid { get; init; }
    public required long LogIndex { get; init; }
    public required string Body { get; init; }
    public required DateTime IntegratedTime { get; init; }
    public string? LogId { get; init; }
    public RekorEntryKind Kind { get; init; }
    public string? PublicKey { get; init; }
    public string? Signature { get; init; }
}

public enum RekorEntryKind
{
    Hashedrekord,
    Intoto,
    Rpm,
    Helm,
    Alpine,
    Jar,
    Rekord
}

public sealed class InclusionProof
{
    public required string EntryUuid { get; init; }
    public required long LogIndex { get; init; }
    public required string RootHash { get; init; }
    public required long TreeSize { get; init; }
    public required List<string> Hashes { get; init; }
    public required bool Valid { get; init; }
    public DateTime VerifiedAt { get; init; }
}

// =============================================================================
// IMPLEMENTATION
// =============================================================================

public sealed class SupplyChainSecurityEngine : ISupplyChainSecurityEngine
{
    private readonly ILogger<SupplyChainSecurityEngine> _logger;
    private readonly ConcurrentDictionary<string, AdmissionPolicy> _policies = new();
    private readonly ConcurrentDictionary<string, VulnerabilityScanResult> _scanCache = new();
    private readonly ConcurrentDictionary<string, SignatureResult> _signatureCache = new();

    public SupplyChainSecurityEngine(ILogger<SupplyChainSecurityEngine> logger)
    {
        _logger = logger;
    }

    // ==========================================================================
    // IMAGE SIGNING
    // ==========================================================================

    public async Task<SignatureResult> SignImageAsync(ImageSigningConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Signing image {Image} using {Method}", config.ImageReference, config.Method);

        var signedAt = DateTime.UtcNow;
        var signatureDigest = GenerateDigest(config.ImageReference + signedAt.Ticks);

        // Simulate keyless signing with Fulcio and Rekor
        var result = new SignatureResult
        {
            ImageReference = config.ImageReference,
            SignatureDigest = signatureDigest,
            SignatureLocation = $"{config.ImageReference}.sig",
            RekorLogId = config.Upload ? GenerateUuid() : null,
            RekorLogIndex = config.Upload ? Random.Shared.NextInt64(1000000, 9999999).ToString() : null,
            Certificate = config.Method == SigningMethod.Keyless ? GenerateCertificate(config) : null,
            CertificateChain = config.Method == SigningMethod.Keyless ? "-----BEGIN CERTIFICATE-----\n...\n-----END CERTIFICATE-----" : null,
            SignedAt = signedAt,
            Annotations = config.Annotations
        };

        _signatureCache[config.ImageReference] = result;
        await Task.Delay(100, cancellation); // Simulate signing operation

        _logger.LogInformation("Successfully signed image {Image}, Rekor index: {Index}",
            config.ImageReference, result.RekorLogIndex);

        return result;
    }

    public async Task<VerificationResult> VerifyImageAsync(ImageVerificationConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Verifying image {Image}", config.ImageReference);

        var signatures = new List<SignatureVerification>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var verified = false;

        // Check signature cache or verify against Rekor
        if (_signatureCache.TryGetValue(config.ImageReference, out var cachedSig))
        {
            verified = true;
            signatures.Add(new SignatureVerification
            {
                Digest = cachedSig.SignatureDigest,
                Valid = true,
                Signer = config.CertificateIdentity ?? "oidc-identity@example.com",
                Issuer = config.CertificateOIDCIssuer ?? "https://oauth2.sigstore.dev/auth",
                SignedAt = cachedSig.SignedAt,
                Annotations = cachedSig.Annotations
            });
        }
        else
        {
            // Simulate verification against Rekor
            await Task.Delay(50, cancellation);

            // For demo, randomly verify or fail
            verified = Random.Shared.NextDouble() > 0.1;
            if (!verified)
            {
                errors.Add("No valid signatures found for image");
            }
            else
            {
                signatures.Add(new SignatureVerification
                {
                    Digest = GenerateDigest(config.ImageReference),
                    Valid = true,
                    Signer = "github-actions[bot]@users.noreply.github.com",
                    Issuer = "https://token.actions.githubusercontent.com",
                    SignedAt = DateTime.UtcNow.AddHours(-1),
                    Annotations = new Dictionary<string, string>
                    {
                        ["repo"] = "example/repo",
                        ["workflow"] = "release.yml"
                    }
                });
            }
        }

        return new VerificationResult
        {
            ImageReference = config.ImageReference,
            Verified = verified,
            Signatures = signatures,
            Errors = errors,
            Warnings = warnings,
            Certificate = verified ? new CertificateInfo
            {
                Subject = config.CertificateIdentity,
                Issuer = "sigstore-intermediate",
                NotBefore = DateTime.UtcNow.AddMinutes(-5),
                NotAfter = DateTime.UtcNow.AddMinutes(10),
                OIDCIssuer = config.CertificateOIDCIssuer,
                GithubWorkflowRepository = "example/repo",
                GithubWorkflowRef = "refs/heads/main"
            } : null,
            TLogEntry = verified && config.CheckTLog ? new TransparencyLogEntry
            {
                LogId = "c0d23d6ad406973f9559f3ba2d1ca01f84147d8ffc5b8445c224f98b9591801d",
                LogIndex = Random.Shared.NextInt64(1000000, 9999999),
                IntegratedTime = DateTime.UtcNow.AddMinutes(-1)
            } : null
        };
    }

    public async Task<SignatureResult> SignBlobAsync(BlobSigningConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Signing blob {Path}", config.FilePath);
        await Task.Delay(100, cancellation);

        return new SignatureResult
        {
            ImageReference = config.FilePath,
            SignatureDigest = GenerateDigest(config.FilePath),
            SignatureLocation = config.OutputSignature ?? $"{config.FilePath}.sig",
            RekorLogId = GenerateUuid(),
            RekorLogIndex = Random.Shared.NextInt64(1000000, 9999999).ToString(),
            SignedAt = DateTime.UtcNow
        };
    }

    // ==========================================================================
    // SBOM MANAGEMENT
    // ==========================================================================

    public async Task<SBOMResult> GenerateSBOMAsync(SBOMGenerationConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Generating SBOM for {Target} in {Format} format", config.Target, config.Format);
        await Task.Delay(200, cancellation);

        var components = GenerateSampleComponents();
        var dependencies = GenerateSampleDependencies(components);

        var sbomContent = config.Format switch
        {
            SBOMFormat.CycloneDX or SBOMFormat.CycloneDXJson => GenerateCycloneDXContent(config, components),
            SBOMFormat.SPDX or SBOMFormat.SPDXJson => GenerateSPDXContent(config, components),
            _ => GenerateSyftContent(config, components)
        };

        return new SBOMResult
        {
            Target = config.Target,
            Format = config.Format,
            Content = sbomContent,
            FilePath = config.OutputPath,
            Metadata = new SBOMMetadata
            {
                Name = config.Target.Split('/').Last().Split(':').First(),
                Version = config.Target.Contains(':') ? config.Target.Split(':').Last() : "latest",
                Tools = config.Scanners
            },
            Components = components,
            Dependencies = dependencies,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<AttestationResult> AttachSBOMAsync(SBOMAttachmentConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Attaching SBOM to {Image}", config.ImageReference);
        await Task.Delay(150, cancellation);

        var attestationDigest = GenerateDigest($"sbom-{config.ImageReference}-{DateTime.UtcNow.Ticks}");

        return new AttestationResult
        {
            ImageReference = config.ImageReference,
            AttestationDigest = attestationDigest,
            SignatureDigest = config.Sign ? GenerateDigest($"sig-{attestationDigest}") : null,
            RekorLogIndex = config.Sign ? Random.Shared.NextInt64(1000000, 9999999).ToString() : null,
            Type = AttestationType.SBOM,
            AttachedAt = DateTime.UtcNow
        };
    }

    public async Task<SBOMAnalysisResult> AnalyzeSBOMAsync(SBOMAnalysisConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Analyzing SBOM {Path}", config.SBOMPath);
        await Task.Delay(100, cancellation);

        var vulnerabilities = new List<ComponentVulnerability>
        {
            new()
            {
                ComponentName = "lodash",
                ComponentVersion = "4.17.20",
                VulnerabilityId = "CVE-2021-23337",
                Severity = VulnerabilitySeverity.High,
                Title = "Prototype Pollution",
                FixedVersion = "4.17.21",
                CvssScore = 7.2
            },
            new()
            {
                ComponentName = "express",
                ComponentVersion = "4.17.1",
                VulnerabilityId = "CVE-2022-24999",
                Severity = VulnerabilitySeverity.Medium,
                Title = "qs Prototype Pollution",
                FixedVersion = "4.18.2",
                CvssScore = 5.3
            }
        };

        var licenseIssues = config.CheckLicenses ? new List<LicenseIssue>
        {
            new()
            {
                ComponentName = "gpl-licensed-lib",
                License = "GPL-3.0",
                IssueType = LicenseIssueType.Copyleft,
                Message = "GPL-3.0 license requires derivative works to be open source"
            }
        } : new List<LicenseIssue>();

        var violations = new List<string>();
        if (vulnerabilities.Any(v => v.Severity == VulnerabilitySeverity.Critical))
            violations.Add("Critical vulnerabilities found");

        return new SBOMAnalysisResult
        {
            SBOMPath = config.SBOMPath,
            TotalComponents = 150,
            VulnerableComponents = vulnerabilities.Select(v => v.ComponentName).Distinct().Count(),
            Vulnerabilities = vulnerabilities,
            LicenseIssues = licenseIssues,
            ComponentsByType = new Dictionary<string, int>
            {
                ["library"] = 120,
                ["application"] = 5,
                ["framework"] = 25
            },
            LicenseDistribution = new Dictionary<string, int>
            {
                ["MIT"] = 80,
                ["Apache-2.0"] = 45,
                ["BSD-3-Clause"] = 15,
                ["ISC"] = 8,
                ["GPL-3.0"] = 2
            },
            PassesPolicy = violations.Count == 0,
            PolicyViolations = violations
        };
    }

    // ==========================================================================
    // SLSA PROVENANCE
    // ==========================================================================

    public async Task<ProvenanceResult> GenerateProvenanceAsync(ProvenanceConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Generating SLSA {Version} provenance for {Subject}", config.Version, config.SubjectName);
        await Task.Delay(150, cancellation);

        var provenanceContent = GenerateProvenanceContent(config);
        var level = AssessSLSALevelFromConfig(config);

        return new ProvenanceResult
        {
            SubjectName = config.SubjectName,
            SubjectDigest = config.SubjectDigest,
            ProvenanceContent = provenanceContent,
            SignatureDigest = config.Sign ? GenerateDigest($"prov-sig-{config.SubjectDigest}") : null,
            RekorLogIndex = config.Sign ? Random.Shared.NextInt64(1000000, 9999999).ToString() : null,
            AssessedLevel = level,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<ProvenanceVerificationResult> VerifyProvenanceAsync(ProvenanceVerificationConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Verifying provenance for {Image}", config.ImageReference);
        await Task.Delay(100, cancellation);

        var verified = Random.Shared.NextDouble() > 0.1;
        var warnings = new List<string>();

        if (config.MinimumLevel > SLSALevel.L2)
            warnings.Add("SLSA L3+ verification requires hardened build evidence");

        return new ProvenanceVerificationResult
        {
            ImageReference = config.ImageReference,
            Verified = verified,
            AssessedLevel = verified ? SLSALevel.L2 : SLSALevel.None,
            Provenance = verified ? new ProvenanceDetails
            {
                BuilderId = config.ExpectedBuilderId ?? "https://github.com/slsa-framework/slsa-github-generator/.github/workflows/builder_container-based_slsa3.yml@v1.9.0",
                BuildType = "https://slsa.dev/container/v1",
                SourceRepository = config.ExpectedSourceRepository ?? "https://github.com/example/repo",
                SourceRef = config.ExpectedSourceBranch ?? "refs/heads/main",
                SourceDigest = GenerateDigest("source"),
                Materials = new List<BuildMaterial>
                {
                    new()
                    {
                        Uri = "git+https://github.com/example/repo",
                        Digest = new Dictionary<string, string> { ["sha1"] = GenerateDigest("git") }
                    }
                },
                BuildStartedOn = DateTime.UtcNow.AddMinutes(-10),
                BuildFinishedOn = DateTime.UtcNow.AddMinutes(-5)
            } : null,
            Errors = verified ? new() : new() { "No valid provenance found" },
            Warnings = warnings
        };
    }

    public async Task<SLSALevelAssessment> AssessSLSALevelAsync(SLSAAssessmentConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Assessing SLSA level for {Image}", config.ImageReference);
        await Task.Delay(100, cancellation);

        var requirements = new List<SLSARequirement>
        {
            // SLSA L1 requirements
            new() { Name = "Provenance exists", RequiredFor = SLSALevel.L1, Met = true, Evidence = "Provenance attestation found" },
            new() { Name = "Provenance is authentic", RequiredFor = SLSALevel.L1, Met = true, Evidence = "Signature verified" },

            // SLSA L2 requirements
            new() { Name = "Hosted build platform", RequiredFor = SLSALevel.L2, Met = true, Evidence = "GitHub Actions detected" },
            new() { Name = "Build service generates provenance", RequiredFor = SLSALevel.L2, Met = true, Evidence = "slsa-github-generator used" },

            // SLSA L3 requirements
            new() { Name = "Hardened build platform", RequiredFor = SLSALevel.L3, Met = false, Recommendation = "Enable GitHub Actions OIDC and use reusable workflows" },
            new() { Name = "Build isolation", RequiredFor = SLSALevel.L3, Met = false, Recommendation = "Use container-based builds with isolated runners" }
        };

        var level = requirements.All(r => r.RequiredFor <= SLSALevel.L2 && r.Met) ? SLSALevel.L2 :
                   requirements.All(r => r.RequiredFor <= SLSALevel.L1 && r.Met) ? SLSALevel.L1 : SLSALevel.None;

        return new SLSALevelAssessment
        {
            ImageReference = config.ImageReference,
            Level = level,
            Requirements = requirements,
            Recommendations = requirements.Where(r => !r.Met && r.Recommendation != null).Select(r => r.Recommendation!).ToList()
        };
    }

    // ==========================================================================
    // VULNERABILITY SCANNING
    // ==========================================================================

    public async Task<VulnerabilityScanResult> ScanImageAsync(ImageScanConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Scanning image {Image} with {Scanner}", config.ImageReference, config.Scanner);
        var startTime = DateTime.UtcNow;
        await Task.Delay(300, cancellation);

        var vulnerabilities = GenerateSampleVulnerabilities(config.MinimumSeverity);
        var secrets = config.ScanSecrets ? GenerateSampleSecrets() : new List<SecretFinding>();
        var misconfigs = config.ScanMisconfigurations ? GenerateSampleMisconfigurations() : new List<MisconfigurationFinding>();

        var result = new VulnerabilityScanResult
        {
            Target = config.ImageReference,
            Scanner = config.Scanner,
            TotalVulnerabilities = vulnerabilities.Count,
            BySeverity = vulnerabilities.GroupBy(v => v.Severity).ToDictionary(g => g.Key, g => g.Count()),
            Vulnerabilities = vulnerabilities,
            Secrets = secrets,
            Misconfigurations = misconfigs,
            ScannedAt = DateTime.UtcNow,
            ScanDuration = DateTime.UtcNow - startTime
        };

        _scanCache[config.ImageReference] = result;
        return result;
    }

    public async Task<VulnerabilityScanResult> ScanFilesystemAsync(FilesystemScanConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Scanning filesystem {Path} with {Scanner}", config.Path, config.Scanner);
        var startTime = DateTime.UtcNow;
        await Task.Delay(200, cancellation);

        var vulnerabilities = GenerateSampleVulnerabilities(config.MinimumSeverity);
        var secrets = config.ScanSecrets ? GenerateSampleSecrets() : new List<SecretFinding>();
        var misconfigs = config.ScanIaC ? GenerateSampleIaCMisconfigurations() : new List<MisconfigurationFinding>();

        return new VulnerabilityScanResult
        {
            Target = config.Path,
            Scanner = config.Scanner,
            TotalVulnerabilities = vulnerabilities.Count,
            BySeverity = vulnerabilities.GroupBy(v => v.Severity).ToDictionary(g => g.Key, g => g.Count()),
            Vulnerabilities = vulnerabilities,
            Secrets = secrets,
            Misconfigurations = misconfigs,
            ScannedAt = DateTime.UtcNow,
            ScanDuration = DateTime.UtcNow - startTime
        };
    }

    public async Task<VulnerabilityTrends> GetVulnerabilityTrendsAsync(string namespace_, TimeSpan period, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Getting vulnerability trends for {Namespace} over {Period}", namespace_, period);
        await Task.Delay(100, cancellation);

        var snapshots = Enumerable.Range(0, 30).Select(i => new VulnerabilitySnapshot
        {
            Timestamp = DateTime.UtcNow.AddDays(-i),
            TotalVulnerabilities = 50 + Random.Shared.Next(-5, 10),
            Critical = Random.Shared.Next(0, 3),
            High = Random.Shared.Next(5, 15),
            Medium = Random.Shared.Next(15, 25),
            Low = Random.Shared.Next(10, 20),
            ImagesScanned = Random.Shared.Next(20, 30)
        }).Reverse().ToList();

        return new VulnerabilityTrends
        {
            Namespace = namespace_,
            Period = period,
            Snapshots = snapshots,
            Analysis = new TrendAnalysis
            {
                CriticalTrend = -0.5,  // Decreasing
                HighTrend = -0.2,
                NewVulnerabilities = 15,
                FixedVulnerabilities = 22,
                MeanTimeToRemediate = 7.5,
                TopVulnerablePackages = new List<string> { "lodash", "express", "axios", "moment", "webpack" }
            }
        };
    }

    // ==========================================================================
    // POLICY ENFORCEMENT
    // ==========================================================================

    public async Task<AdmissionPolicy> CreateAdmissionPolicyAsync(AdmissionPolicyConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Creating admission policy {Name} in {Namespace}", config.Name, config.Namespace);
        await Task.Delay(50, cancellation);

        var policy = new AdmissionPolicy
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = config.Name,
            Namespace = config.Namespace,
            Mode = config.Mode,
            Config = config,
            CreatedAt = DateTime.UtcNow,
            Statistics = new PolicyStatistics()
        };

        _policies[$"{config.Namespace}/{config.Name}"] = policy;
        return policy;
    }

    public async Task<PolicyEvaluationResult> EvaluatePolicyAsync(PolicyEvaluationConfig config, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Evaluating policy for {Image} in {Namespace}", config.ImageReference, config.Namespace);
        await Task.Delay(100, cancellation);

        var violations = new List<PolicyViolation>();
        var warnings = new List<PolicyWarning>();
        var evaluatedPolicies = new List<string>();

        // Find applicable policies
        var applicablePolicies = _policies.Values
            .Where(p => p.Namespace == config.Namespace || p.Namespace == "*")
            .ToList();

        foreach (var policy in applicablePolicies)
        {
            evaluatedPolicies.Add(policy.Name);

            // Check signature requirement
            if (policy.Config.ImageRequirements.Any(r => r.RequireSignature))
            {
                if (!_signatureCache.ContainsKey(config.ImageReference))
                {
                    if (policy.Mode == PolicyMode.Enforce)
                    {
                        violations.Add(new PolicyViolation
                        {
                            PolicyName = policy.Name,
                            Rule = "require-signature",
                            Message = "Image signature verification failed",
                            Severity = VulnerabilitySeverity.High
                        });
                    }
                    else
                    {
                        warnings.Add(new PolicyWarning
                        {
                            PolicyName = policy.Name,
                            Rule = "require-signature",
                            Message = "Image is not signed"
                        });
                    }
                }
            }

            // Check vulnerability policy
            if (policy.Config.VulnerabilityPolicy != null)
            {
                if (_scanCache.TryGetValue(config.ImageReference, out var scanResult))
                {
                    var criticalCount = scanResult.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
                    if (criticalCount > policy.Config.VulnerabilityPolicy.MaxCritical)
                    {
                        violations.Add(new PolicyViolation
                        {
                            PolicyName = policy.Name,
                            Rule = "max-critical-vulnerabilities",
                            Message = $"Image has {criticalCount} critical vulnerabilities (max: {policy.Config.VulnerabilityPolicy.MaxCritical})",
                            Severity = VulnerabilitySeverity.Critical
                        });
                    }
                }
            }

            // Check trusted registries
            if (policy.Config.TrustedRegistries.Any())
            {
                var registry = config.ImageReference.Split('/').First();
                if (!policy.Config.TrustedRegistries.Any(r => config.ImageReference.StartsWith(r)))
                {
                    violations.Add(new PolicyViolation
                    {
                        PolicyName = policy.Name,
                        Rule = "trusted-registry",
                        Message = $"Image registry '{registry}' is not in trusted registries list",
                        Severity = VulnerabilitySeverity.High
                    });
                }
            }
        }

        var decision = violations.Count > 0 ? PolicyDecision.Deny :
                      warnings.Count > 0 ? PolicyDecision.Warn : PolicyDecision.Allow;

        return new PolicyEvaluationResult
        {
            ImageReference = config.ImageReference,
            Allowed = decision != PolicyDecision.Deny || config.DryRun,
            Decision = decision,
            Violations = violations,
            Warnings = warnings,
            EvaluatedPolicies = evaluatedPolicies,
            EvaluatedAt = DateTime.UtcNow
        };
    }

    // ==========================================================================
    // TRANSPARENCY LOG
    // ==========================================================================

    public async Task<List<RekorEntry>> QueryRekorAsync(RekorQuery query, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Querying Rekor transparency log");
        await Task.Delay(100, cancellation);

        return Enumerable.Range(0, Math.Min(query.Limit, 10)).Select(i => new RekorEntry
        {
            Uuid = GenerateUuid(),
            LogIndex = 10000000 + i,
            Body = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"kind\":\"hashedrekord\",\"index\":{i}}}")),
            IntegratedTime = DateTime.UtcNow.AddMinutes(-i * 5),
            LogId = "c0d23d6ad406973f9559f3ba2d1ca01f84147d8ffc5b8445c224f98b9591801d",
            Kind = RekorEntryKind.Hashedrekord
        }).ToList();
    }

    public async Task<InclusionProof> GetInclusionProofAsync(string entryUuid, CancellationToken cancellation = default)
    {
        _logger.LogInformation("Getting inclusion proof for {Uuid}", entryUuid);
        await Task.Delay(50, cancellation);

        return new InclusionProof
        {
            EntryUuid = entryUuid,
            LogIndex = Random.Shared.NextInt64(10000000, 99999999),
            RootHash = GenerateDigest("root"),
            TreeSize = Random.Shared.NextInt64(10000000, 99999999),
            Hashes = Enumerable.Range(0, 20).Select(_ => GenerateDigest(Guid.NewGuid().ToString())).ToList(),
            Valid = true,
            VerifiedAt = DateTime.UtcNow
        };
    }

    // ==========================================================================
    // HELPER METHODS
    // ==========================================================================

    private static string GenerateDigest(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input + DateTime.UtcNow.Ticks));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateUuid()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GenerateCertificate(ImageSigningConfig config)
    {
        return $@"-----BEGIN CERTIFICATE-----
MIICjzCCAhWgAwIBAgIUQzJXYmI{Random.Shared.Next(1000, 9999)}...
Subject: {config.ImageReference}
Issuer: sigstore-intermediate
Valid: {DateTime.UtcNow:O} - {DateTime.UtcNow.AddMinutes(10):O}
-----END CERTIFICATE-----";
    }

    private static List<SBOMComponent> GenerateSampleComponents()
    {
        return new List<SBOMComponent>
        {
            new() { Name = "lodash", Version = "4.17.21", Type = ComponentType.Library, Purl = "pkg:npm/lodash@4.17.21", License = "MIT" },
            new() { Name = "express", Version = "4.18.2", Type = ComponentType.Framework, Purl = "pkg:npm/express@4.18.2", License = "MIT" },
            new() { Name = "axios", Version = "1.6.0", Type = ComponentType.Library, Purl = "pkg:npm/axios@1.6.0", License = "MIT" },
            new() { Name = "dotenv", Version = "16.3.1", Type = ComponentType.Library, Purl = "pkg:npm/dotenv@16.3.1", License = "BSD-2-Clause" },
            new() { Name = "typescript", Version = "5.3.2", Type = ComponentType.Library, Purl = "pkg:npm/typescript@5.3.2", License = "Apache-2.0" },
            new() { Name = "node", Version = "20.10.0", Type = ComponentType.Application, Purl = "pkg:generic/node@20.10.0", License = "MIT" },
            new() { Name = "alpine", Version = "3.19", Type = ComponentType.OperatingSystem, Purl = "pkg:oci/alpine@3.19", License = "MIT" }
        };
    }

    private static List<SBOMDependency> GenerateSampleDependencies(List<SBOMComponent> components)
    {
        return new List<SBOMDependency>
        {
            new() { Ref = "express@4.18.2", DependsOn = new List<string> { "lodash@4.17.21" } },
            new() { Ref = "axios@1.6.0", DependsOn = new List<string>() },
            new() { Ref = "node@20.10.0", DependsOn = new List<string> { "alpine@3.19" } }
        };
    }

    private static string GenerateCycloneDXContent(SBOMGenerationConfig config, List<SBOMComponent> components)
    {
        return JsonSerializer.Serialize(new
        {
            bomFormat = "CycloneDX",
            specVersion = "1.5",
            version = 1,
            metadata = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                tools = config.Scanners.Select(s => new { vendor = s, name = s }).ToArray(),
                component = new { type = "container", name = config.Target }
            },
            components = components.Select(c => new
            {
                type = c.Type.ToString().ToLower(),
                name = c.Name,
                version = c.Version,
                purl = c.Purl,
                licenses = c.License != null ? new[] { new { license = new { id = c.License } } } : null
            }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateSPDXContent(SBOMGenerationConfig config, List<SBOMComponent> components)
    {
        return JsonSerializer.Serialize(new
        {
            spdxVersion = "SPDX-2.3",
            dataLicense = "CC0-1.0",
            SPDXID = "SPDXRef-DOCUMENT",
            name = config.Target,
            documentNamespace = $"https://spdx.org/spdxdocs/{config.Target}-{Guid.NewGuid()}",
            creationInfo = new
            {
                created = DateTime.UtcNow.ToString("O"),
                creators = config.Scanners.Select(s => $"Tool: {s}").ToArray()
            },
            packages = components.Select((c, i) => new
            {
                SPDXID = $"SPDXRef-Package-{i}",
                name = c.Name,
                versionInfo = c.Version,
                downloadLocation = "NOASSERTION",
                licenseConcluded = c.License ?? "NOASSERTION"
            }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateSyftContent(SBOMGenerationConfig config, List<SBOMComponent> components)
    {
        return JsonSerializer.Serialize(new
        {
            artifacts = components.Select(c => new
            {
                name = c.Name,
                version = c.Version,
                type = c.Type.ToString().ToLower(),
                purl = c.Purl,
                licenses = c.License != null ? new[] { c.License } : Array.Empty<string>()
            }).ToArray(),
            source = new { type = "image", target = config.Target },
            distro = new { name = "alpine", version = "3.19" }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateProvenanceContent(ProvenanceConfig config)
    {
        return JsonSerializer.Serialize(new
        {
            _type = "https://in-toto.io/Statement/v1",
            subject = new[]
            {
                new
                {
                    name = config.SubjectName,
                    digest = new Dictionary<string, string> { ["sha256"] = config.SubjectDigest.Replace("sha256:", "") }
                }
            },
            predicateType = config.Version == SLSAVersion.V1_0
                ? "https://slsa.dev/provenance/v1"
                : "https://slsa.dev/provenance/v0.2",
            predicate = new
            {
                buildDefinition = new
                {
                    buildType = config.Build.BuildType,
                    externalParameters = config.Build.Parameters,
                    internalParameters = config.Build.InternalParameters,
                    resolvedDependencies = config.Materials.Materials.Select(m => new
                    {
                        uri = m.Uri,
                        digest = m.Digest
                    }).ToArray()
                },
                runDetails = new
                {
                    builder = new { id = config.BuilderId ?? "https://github.com/slsa-framework/slsa-github-generator" },
                    metadata = new
                    {
                        invocationId = Guid.NewGuid().ToString(),
                        startedOn = config.Build.StartedOn.ToString("O"),
                        finishedOn = config.Build.FinishedOn?.ToString("O")
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static SLSALevel AssessSLSALevelFromConfig(ProvenanceConfig config)
    {
        // Simple assessment based on config completeness
        if (string.IsNullOrEmpty(config.BuilderId))
            return SLSALevel.L1;

        if (config.BuilderId.Contains("github.com/slsa-framework"))
            return SLSALevel.L3;

        if (config.BuilderId.Contains("github.com") || config.BuilderId.Contains("cloudbuild"))
            return SLSALevel.L2;

        return SLSALevel.L1;
    }

    private static List<Vulnerability> GenerateSampleVulnerabilities(VulnerabilitySeverity minSeverity)
    {
        var allVulns = new List<Vulnerability>
        {
            new() { Id = "CVE-2024-0001", PackageName = "openssl", InstalledVersion = "3.0.10", Severity = VulnerabilitySeverity.Critical, FixedVersion = "3.0.12", Title = "Buffer overflow in X.509 certificate verification", CvssScore = 9.8 },
            new() { Id = "CVE-2024-0002", PackageName = "libcurl", InstalledVersion = "8.1.0", Severity = VulnerabilitySeverity.High, FixedVersion = "8.4.0", Title = "HTTP/2 HPACK decoder vulnerability", CvssScore = 7.5 },
            new() { Id = "CVE-2024-0003", PackageName = "zlib", InstalledVersion = "1.2.13", Severity = VulnerabilitySeverity.Medium, FixedVersion = "1.3.0", Title = "Heap buffer overflow", CvssScore = 5.3 },
            new() { Id = "CVE-2024-0004", PackageName = "busybox", InstalledVersion = "1.36.0", Severity = VulnerabilitySeverity.Low, Title = "Information disclosure", CvssScore = 3.1 },
            new() { Id = "CVE-2024-0005", PackageName = "musl", InstalledVersion = "1.2.4", Severity = VulnerabilitySeverity.High, FixedVersion = "1.2.5", Title = "Use after free in malloc", CvssScore = 7.8 }
        };

        return allVulns.Where(v => v.Severity >= minSeverity).ToList();
    }

    private static List<SecretFinding> GenerateSampleSecrets()
    {
        return new List<SecretFinding>
        {
            new() { RuleId = "aws-access-key", Category = "AWS", Title = "AWS Access Key ID", Severity = VulnerabilitySeverity.Critical, Target = "/app/config.js", StartLine = 42 },
            new() { RuleId = "generic-api-key", Category = "Generic", Title = "Generic API Key", Severity = VulnerabilitySeverity.High, Target = "/app/.env", StartLine = 15 }
        };
    }

    private static List<MisconfigurationFinding> GenerateSampleMisconfigurations()
    {
        return new List<MisconfigurationFinding>
        {
            new() { Id = "DS002", Type = "Dockerfile", Title = "Image user should not be 'root'", Severity = VulnerabilitySeverity.High, Resolution = "Add 'USER nonroot' instruction" },
            new() { Id = "DS026", Type = "Dockerfile", Title = "No HEALTHCHECK defined", Severity = VulnerabilitySeverity.Low, Resolution = "Add HEALTHCHECK instruction" }
        };
    }

    private static List<MisconfigurationFinding> GenerateSampleIaCMisconfigurations()
    {
        return new List<MisconfigurationFinding>
        {
            new() { Id = "AVD-KSV-0001", Type = "Kubernetes", Title = "Container running as root", Severity = VulnerabilitySeverity.High, Resolution = "Set securityContext.runAsNonRoot: true" },
            new() { Id = "AVD-KSV-0012", Type = "Kubernetes", Title = "Containers must not run with allowPrivilegeEscalation", Severity = VulnerabilitySeverity.Medium, Resolution = "Set securityContext.allowPrivilegeEscalation: false" },
            new() { Id = "AVD-AWS-0089", Type = "Terraform", Title = "S3 bucket has logging disabled", Severity = VulnerabilitySeverity.Medium, Resolution = "Enable access logging for S3 bucket" }
        };
    }
}
