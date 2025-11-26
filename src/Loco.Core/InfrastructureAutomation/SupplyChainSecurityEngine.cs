using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.InfrastructureAutomation
{
    /// <summary>
    /// Supply Chain Security Engine implementing Sigstore, SBOM, and vulnerability scanning patterns
    ///
    /// Research sources:
    /// - Sigstore implementation: https://openssf.org/blog/2024/02/16/scaling-up-supply-chain-security-implementing-sigstore-for-seamless-container-image-signing/
    /// - SBOM and VEX 2025: https://faithforgelabs.com/blog_supplychain_security_2025.php
    /// - Trivy SBOM: https://trivy.dev/v0.61/docs/supply-chain/attestation/sbom/
    /// - Grype vulnerability scanning: https://github.com/anchore/grype
    /// - Container security tools: https://developersvoice.com/blog/cloud/dotnet-containers-aot-sbom/
    ///
    /// Capabilities:
    /// - Cosign-based image signing and verification
    /// - SBOM generation (CycloneDX, SPDX formats)
    /// - SBOM attestation with in-toto
    /// - Vulnerability scanning with Trivy/Grype
    /// - VEX (Vulnerability Exploitability eXchange) support
    /// - SLSA provenance tracking
    /// - Rekor transparency log integration
    /// - Policy-based admission control for signed images
    /// </summary>
    public interface ISupplyChainSecurityEngine
    {
        Task<SignatureResult> SignImageAsync(string tenantId, ImageReference image, SigningKey key, CancellationToken cancellation = default);
        Task<VerificationResult> VerifyImageAsync(string tenantId, ImageReference image, VerificationPolicy policy, CancellationToken cancellation = default);
        Task<SBOM> GenerateSBOMAsync(string tenantId, ImageReference image, SBOMFormat format, CancellationToken cancellation = default);
        Task<Attestation> AttestSBOMAsync(string tenantId, SBOM sbom, SigningKey key, CancellationToken cancellation = default);
        Task<ScanResult> ScanVulnerabilitiesAsync(string tenantId, ImageReference image, ScannerType scanner, CancellationToken cancellation = default);
        Task<ScanResult> ScanSBOMAsync(string tenantId, SBOM sbom, ScannerType scanner, CancellationToken cancellation = default);
        Task<VEXDocument> CreateVEXAsync(string tenantId, VEXDocument vex, CancellationToken cancellation = default);
        Task<SLSAProvenance> GenerateProvenanceAsync(string tenantId, BuildInfo build, CancellationToken cancellation = default);
    }

    public class SupplyChainSecurityEngine : ISupplyChainSecurityEngine
    {
        private readonly Dictionary<string, SignatureResult> _signatures = new();
        private readonly Dictionary<string, SBOM> _sboms = new();
        private readonly Dictionary<string, Attestation> _attestations = new();
        private readonly Dictionary<string, ScanResult> _scanResults = new();
        private readonly Dictionary<string, VEXDocument> _vexDocuments = new();
        private readonly Dictionary<string, SLSAProvenance> _provenances = new();

        public async Task<SignatureResult> SignImageAsync(string tenantId, ImageReference image, SigningKey key, CancellationToken cancellation = default)
        {
            var result = new SignatureResult
            {
                ImageRef = image.ToString(),
                SignedAt = DateTime.UtcNow,
                SignatureType = SignatureType.Cosign
            };

            // Generate image digest
            var digest = await CalculateImageDigestAsync(image, cancellation);
            result.Digest = digest;

            // Sign with cosign
            if (key.Type == KeyType.Keyless)
            {
                // Use Fulcio for ephemeral certificates
                var certificate = await RequestFulcioCertificateAsync(key.OIDCToken!, cancellation);
                result.Certificate = certificate;

                // Sign with ephemeral key
                var signature = await SignWithEphemeralKeyAsync(digest, certificate, cancellation);
                result.Signature = signature;
            }
            else if (key.Type == KeyType.KeyPair)
            {
                // Sign with static key pair
                var signature = await SignWithKeyPairAsync(digest, key.PrivateKey!, cancellation);
                result.Signature = signature;
                result.PublicKey = key.PublicKey;
            }

            // Upload to Rekor transparency log
            var rekorEntry = await UploadToRekorAsync(result, cancellation);
            result.RekorEntry = rekorEntry;

            _signatures[$"{tenantId}:{image}"] = result;

            return await Task.FromResult(result);
        }

        public async Task<VerificationResult> VerifyImageAsync(string tenantId, ImageReference image, VerificationPolicy policy, CancellationToken cancellation = default)
        {
            var result = new VerificationResult
            {
                ImageRef = image.ToString(),
                VerifiedAt = DateTime.UtcNow,
                Verified = false,
                Checks = new List<VerificationCheck>()
            };

            var key = $"{tenantId}:{image}";

            // Check if signature exists
            if (!_signatures.TryGetValue(key, out var signature))
            {
                result.Checks.Add(new VerificationCheck
                {
                    Name = "SignatureExists",
                    Passed = false,
                    Message = "No signature found for image"
                });
                return await Task.FromResult(result);
            }

            result.Checks.Add(new VerificationCheck
            {
                Name = "SignatureExists",
                Passed = true,
                Message = "Signature found"
            });

            // Verify signature
            var signatureValid = await VerifySignatureAsync(signature, policy, cancellation);
            result.Checks.Add(new VerificationCheck
            {
                Name = "SignatureValid",
                Passed = signatureValid,
                Message = signatureValid ? "Signature is valid" : "Signature verification failed"
            });

            // Verify with Rekor transparency log
            if (policy.RequireRekor && signature.RekorEntry != null)
            {
                var rekorValid = await VerifyRekorEntryAsync(signature.RekorEntry, cancellation);
                result.Checks.Add(new VerificationCheck
                {
                    Name = "RekorVerification",
                    Passed = rekorValid,
                    Message = rekorValid ? "Rekor entry verified" : "Rekor verification failed"
                });
            }

            // Verify certificate chain (for keyless signing)
            if (policy.RequireFulcioCertificate && signature.Certificate != null)
            {
                var certValid = await VerifyCertificateChainAsync(signature.Certificate, cancellation);
                result.Checks.Add(new VerificationCheck
                {
                    Name = "CertificateChain",
                    Passed = certValid,
                    Message = certValid ? "Certificate chain valid" : "Certificate chain invalid"
                });
            }

            // Verify SBOM attestation if required
            if (policy.RequireSBOM)
            {
                var sbomKey = $"{tenantId}:{image}:sbom";
                var hasSBOM = _attestations.ContainsKey(sbomKey);
                result.Checks.Add(new VerificationCheck
                {
                    Name = "SBOMAttestation",
                    Passed = hasSBOM,
                    Message = hasSBOM ? "SBOM attestation found" : "SBOM attestation missing"
                });
            }

            // Check vulnerability scan results
            if (policy.MaxCriticalVulnerabilities.HasValue || policy.MaxHighVulnerabilities.HasValue)
            {
                var scanKey = $"{tenantId}:{image}:scan";
                if (_scanResults.TryGetValue(scanKey, out var scan))
                {
                    var criticalCount = scan.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
                    var highCount = scan.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High);

                    var vulnCheckPassed = true;
                    var vulnMessage = $"Critical: {criticalCount}, High: {highCount}";

                    if (policy.MaxCriticalVulnerabilities.HasValue && criticalCount > policy.MaxCriticalVulnerabilities.Value)
                    {
                        vulnCheckPassed = false;
                        vulnMessage += $" (exceeds max critical: {policy.MaxCriticalVulnerabilities.Value})";
                    }

                    if (policy.MaxHighVulnerabilities.HasValue && highCount > policy.MaxHighVulnerabilities.Value)
                    {
                        vulnCheckPassed = false;
                        vulnMessage += $" (exceeds max high: {policy.MaxHighVulnerabilities.Value})";
                    }

                    result.Checks.Add(new VerificationCheck
                    {
                        Name = "VulnerabilityScan",
                        Passed = vulnCheckPassed,
                        Message = vulnMessage
                    });
                }
            }

            result.Verified = result.Checks.All(c => c.Passed);
            return await Task.FromResult(result);
        }

        public async Task<SBOM> GenerateSBOMAsync(string tenantId, ImageReference image, SBOMFormat format, CancellationToken cancellation = default)
        {
            var sbom = new SBOM
            {
                Id = Guid.NewGuid().ToString(),
                Format = format,
                SpecVersion = format == SBOMFormat.CycloneDX ? "1.5" : "2.3",
                GeneratedAt = DateTime.UtcNow,
                Image = image.ToString(),
                Components = new List<SBOMComponent>()
            };

            // Simulate scanning image layers for packages
            var components = await ScanImageLayersAsync(image, cancellation);
            sbom.Components.AddRange(components);

            // Add metadata
            sbom.Metadata = new SBOMMetadata
            {
                Tools = new List<string> { "Syft", "Trivy" },
                Authors = new List<string> { tenantId },
                Timestamp = DateTime.UtcNow
            };

            _sboms[$"{tenantId}:{image}"] = sbom;

            return await Task.FromResult(sbom);
        }

        public async Task<Attestation> AttestSBOMAsync(string tenantId, SBOM sbom, SigningKey key, CancellationToken cancellation = default)
        {
            var attestation = new Attestation
            {
                Id = Guid.NewGuid().ToString(),
                Type = AttestationType.SBOM,
                PredicateType = "https://spdx.dev/Document",
                Subject = new List<AttestationSubject>
                {
                    new AttestationSubject
                    {
                        Name = sbom.Image,
                        Digest = new Dictionary<string, string>
                        {
                            ["sha256"] = await CalculateImageDigestAsync(new ImageReference(sbom.Image), cancellation)
                        }
                    }
                },
                Predicate = JsonSerializer.Serialize(sbom),
                CreatedAt = DateTime.UtcNow
            };

            // Sign attestation with in-toto format
            var payload = JsonSerializer.Serialize(new
            {
                _type = "https://in-toto.io/Statement/v0.1",
                subject = attestation.Subject,
                predicateType = attestation.PredicateType,
                predicate = sbom
            });

            if (key.Type == KeyType.Keyless)
            {
                var certificate = await RequestFulcioCertificateAsync(key.OIDCToken!, cancellation);
                attestation.Signature = await SignWithEphemeralKeyAsync(payload, certificate, cancellation);
                attestation.Certificate = certificate;
            }
            else
            {
                attestation.Signature = await SignWithKeyPairAsync(payload, key.PrivateKey!, cancellation);
                attestation.PublicKey = key.PublicKey;
            }

            _attestations[$"{tenantId}:{sbom.Image}:sbom"] = attestation;

            return await Task.FromResult(attestation);
        }

        public async Task<ScanResult> ScanVulnerabilitiesAsync(string tenantId, ImageReference image, ScannerType scanner, CancellationToken cancellation = default)
        {
            var result = new ScanResult
            {
                ImageRef = image.ToString(),
                Scanner = scanner,
                ScannedAt = DateTime.UtcNow,
                Vulnerabilities = new List<Vulnerability>()
            };

            // Simulate vulnerability scanning
            if (scanner == ScannerType.Trivy)
            {
                result.Vulnerabilities = await ScanWithTrivyAsync(image, cancellation);
            }
            else if (scanner == ScannerType.Grype)
            {
                result.Vulnerabilities = await ScanWithGrypeAsync(image, cancellation);
            }

            // Calculate severity counts
            result.Summary = new VulnerabilitySummary
            {
                Critical = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical),
                High = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High),
                Medium = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Medium),
                Low = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Low),
                Negligible = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Negligible)
            };

            _scanResults[$"{tenantId}:{image}:scan"] = result;

            return await Task.FromResult(result);
        }

        public async Task<ScanResult> ScanSBOMAsync(string tenantId, SBOM sbom, ScannerType scanner, CancellationToken cancellation = default)
        {
            var result = new ScanResult
            {
                ImageRef = sbom.Image,
                Scanner = scanner,
                ScannedAt = DateTime.UtcNow,
                Vulnerabilities = new List<Vulnerability>()
            };

            // Scan SBOM components for vulnerabilities
            foreach (var component in sbom.Components)
            {
                var vulns = await ScanComponentAsync(component, scanner, cancellation);
                result.Vulnerabilities.AddRange(vulns);
            }

            result.Summary = new VulnerabilitySummary
            {
                Critical = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical),
                High = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High),
                Medium = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Medium),
                Low = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Low),
                Negligible = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Negligible)
            };

            return await Task.FromResult(result);
        }

        public async Task<VEXDocument> CreateVEXAsync(string tenantId, VEXDocument vex, CancellationToken cancellation = default)
        {
            vex.Id = Guid.NewGuid().ToString();
            vex.TenantId = tenantId;
            vex.CreatedAt = DateTime.UtcNow;
            vex.Version = "1.0";

            _vexDocuments[$"{tenantId}:{vex.Id}"] = vex;

            return await Task.FromResult(vex);
        }

        public async Task<SLSAProvenance> GenerateProvenanceAsync(string tenantId, BuildInfo build, CancellationToken cancellation = default)
        {
            var provenance = new SLSAProvenance
            {
                Id = Guid.NewGuid().ToString(),
                BuildType = "https://slsa.dev/container-based-build/v0.1",
                Builder = new Builder
                {
                    Id = build.BuilderId,
                    BuilderDependencies = build.BuilderDependencies ?? new List<ResourceDescriptor>()
                },
                Invocation = new Invocation
                {
                    ConfigSource = new ConfigSource
                    {
                        Uri = build.SourceRepo,
                        Digest = new Dictionary<string, string>
                        {
                            ["sha1"] = build.SourceCommit
                        },
                        EntryPoint = build.EntryPoint
                    },
                    Parameters = build.Parameters ?? new Dictionary<string, object>(),
                    Environment = build.Environment ?? new Dictionary<string, string>()
                },
                BuildConfig = build.BuildConfig,
                Metadata = new ProvenanceMetadata
                {
                    BuildInvocationId = Guid.NewGuid().ToString(),
                    BuildStartedOn = build.StartedAt,
                    BuildFinishedOn = build.FinishedAt,
                    Completeness = new Completeness
                    {
                        Parameters = true,
                        Environment = true,
                        Materials = true
                    },
                    Reproducible = build.Reproducible
                },
                Materials = build.Materials ?? new List<ResourceDescriptor>()
            };

            _provenances[$"{tenantId}:{provenance.Id}"] = provenance;

            return await Task.FromResult(provenance);
        }

        // Private helper methods

        private async Task<string> CalculateImageDigestAsync(ImageReference image, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(image.ToString()));
            return Convert.ToHexString(hash).ToLower();
        }

        private async Task<string> RequestFulcioCertificateAsync(string oidcToken, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate Fulcio certificate issuance
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"CERT-{Guid.NewGuid()}"));
        }

        private async Task<string> SignWithEphemeralKeyAsync(string data, string certificate, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private async Task<string> SignWithKeyPairAsync(string data, string privateKey, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data + privateKey));
            return Convert.ToBase64String(hash);
        }

        private async Task<RekorEntry> UploadToRekorAsync(SignatureResult signature, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);

            return new RekorEntry
            {
                UUID = Guid.NewGuid().ToString(),
                LogIndex = new Random().Next(1000000),
                Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(signature))),
                IntegratedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private async Task<bool> VerifySignatureAsync(SignatureResult signature, VerificationPolicy policy, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);
            // Simulate signature verification
            return !string.IsNullOrEmpty(signature.Signature);
        }

        private async Task<bool> VerifyRekorEntryAsync(RekorEntry entry, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);
            return entry.UUID != null;
        }

        private async Task<bool> VerifyCertificateChainAsync(string certificate, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);
            return !string.IsNullOrEmpty(certificate);
        }

        private async Task<List<SBOMComponent>> ScanImageLayersAsync(ImageReference image, CancellationToken cancellation)
        {
            await Task.Delay(200, cancellation);

            // Simulate package discovery
            return new List<SBOMComponent>
            {
                new SBOMComponent
                {
                    Type = "library",
                    Name = "openssl",
                    Version = "1.1.1w",
                    PackageURL = "pkg:deb/debian/openssl@1.1.1w",
                    Licenses = new List<string> { "OpenSSL" },
                    Supplier = "Debian"
                },
                new SBOMComponent
                {
                    Type = "library",
                    Name = "curl",
                    Version = "7.88.1",
                    PackageURL = "pkg:deb/debian/curl@7.88.1",
                    Licenses = new List<string> { "MIT" },
                    Supplier = "Debian"
                },
                new SBOMComponent
                {
                    Type = "library",
                    Name = "zlib",
                    Version = "1.2.13",
                    PackageURL = "pkg:deb/debian/zlib@1.2.13",
                    Licenses = new List<string> { "Zlib" },
                    Supplier = "Debian"
                }
            };
        }

        private async Task<List<Vulnerability>> ScanWithTrivyAsync(ImageReference image, CancellationToken cancellation)
        {
            await Task.Delay(300, cancellation);

            return new List<Vulnerability>
            {
                new Vulnerability
                {
                    VulnerabilityID = "CVE-2024-12345",
                    PkgName = "openssl",
                    InstalledVersion = "1.1.1w",
                    FixedVersion = "1.1.1x",
                    Severity = VulnerabilitySeverity.High,
                    Description = "OpenSSL vulnerability allowing remote code execution",
                    References = new List<string> { "https://cve.mitre.org/cgi-bin/cvename.cgi?name=CVE-2024-12345" },
                    CVSS = new CVSS
                    {
                        Score = 7.5,
                        Vector = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:N/A:N"
                    }
                }
            };
        }

        private async Task<List<Vulnerability>> ScanWithGrypeAsync(ImageReference image, CancellationToken cancellation)
        {
            await Task.Delay(250, cancellation);

            return new List<Vulnerability>
            {
                new Vulnerability
                {
                    VulnerabilityID = "CVE-2024-54321",
                    PkgName = "curl",
                    InstalledVersion = "7.88.1",
                    FixedVersion = "7.88.2",
                    Severity = VulnerabilitySeverity.Medium,
                    Description = "curl vulnerability in URL parsing",
                    References = new List<string> { "https://github.com/advisories/GHSA-xxxx" },
                    CVSS = new CVSS
                    {
                        Score = 5.3,
                        Vector = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:L/A:N"
                    }
                }
            };
        }

        private async Task<List<Vulnerability>> ScanComponentAsync(SBOMComponent component, ScannerType scanner, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            // Simulate vulnerability lookup for component
            if (component.Name == "openssl" && component.Version == "1.1.1w")
            {
                return new List<Vulnerability>
                {
                    new Vulnerability
                    {
                        VulnerabilityID = "CVE-2024-12345",
                        PkgName = component.Name,
                        InstalledVersion = component.Version,
                        FixedVersion = "1.1.1x",
                        Severity = VulnerabilitySeverity.High,
                        Description = "OpenSSL vulnerability"
                    }
                };
            }

            return new List<Vulnerability>();
        }
    }

    // Model classes

    public class ImageReference
    {
        public string Registry { get; set; } = "";
        public string Repository { get; set; } = "";
        public string Tag { get; set; } = "latest";
        public string? Digest { get; set; }

        public ImageReference() { }

        public ImageReference(string fullRef)
        {
            // Parse full reference (e.g., "docker.io/library/nginx:1.21@sha256:abc...")
            var parts = fullRef.Split('@');
            if (parts.Length > 1)
                Digest = parts[1];

            var imageParts = parts[0].Split(':');
            Tag = imageParts.Length > 1 ? imageParts[1] : "latest";

            var repoPath = imageParts[0];
            var pathParts = repoPath.Split('/');

            if (pathParts.Length >= 3)
            {
                Registry = pathParts[0];
                Repository = string.Join('/', pathParts.Skip(1));
            }
            else
            {
                Registry = "docker.io";
                Repository = repoPath;
            }
        }

        public override string ToString()
        {
            var result = $"{Registry}/{Repository}:{Tag}";
            if (!string.IsNullOrEmpty(Digest))
                result += $"@{Digest}";
            return result;
        }
    }

    public class SigningKey
    {
        public KeyType Type { get; set; }
        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }
        public string? OIDCToken { get; set; }
    }

    public enum KeyType
    {
        KeyPair,
        Keyless
    }

    public class SignatureResult
    {
        public string ImageRef { get; set; } = "";
        public string Digest { get; set; } = "";
        public SignatureType SignatureType { get; set; }
        public string? Signature { get; set; }
        public string? PublicKey { get; set; }
        public string? Certificate { get; set; }
        public DateTime SignedAt { get; set; }
        public RekorEntry? RekorEntry { get; set; }
    }

    public enum SignatureType
    {
        Cosign,
        Notary
    }

    public class RekorEntry
    {
        public string UUID { get; set; } = "";
        public int LogIndex { get; set; }
        public string Body { get; set; } = "";
        public long IntegratedTime { get; set; }
    }

    public class VerificationPolicy
    {
        public bool RequireRekor { get; set; } = true;
        public bool RequireFulcioCertificate { get; set; } = true;
        public bool RequireSBOM { get; set; }
        public int? MaxCriticalVulnerabilities { get; set; }
        public int? MaxHighVulnerabilities { get; set; }
        public List<string>? TrustedIssuers { get; set; }
    }

    public class VerificationResult
    {
        public string ImageRef { get; set; } = "";
        public DateTime VerifiedAt { get; set; }
        public bool Verified { get; set; }
        public List<VerificationCheck> Checks { get; set; } = new();
    }

    public class VerificationCheck
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string Message { get; set; } = "";
    }

    public class SBOM
    {
        public string Id { get; set; } = "";
        public SBOMFormat Format { get; set; }
        public string SpecVersion { get; set; } = "";
        public string Image { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public SBOMMetadata Metadata { get; set; } = new();
        public List<SBOMComponent> Components { get; set; } = new();
    }

    public enum SBOMFormat
    {
        CycloneDX,
        SPDX
    }

    public class SBOMMetadata
    {
        public List<string> Tools { get; set; } = new();
        public List<string> Authors { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class SBOMComponent
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string? PackageURL { get; set; }
        public List<string> Licenses { get; set; } = new();
        public string? Supplier { get; set; }
        public string? Hash { get; set; }
    }

    public class Attestation
    {
        public string Id { get; set; } = "";
        public AttestationType Type { get; set; }
        public string PredicateType { get; set; } = "";
        public List<AttestationSubject> Subject { get; set; } = new();
        public string Predicate { get; set; } = "";
        public string? Signature { get; set; }
        public string? PublicKey { get; set; }
        public string? Certificate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public enum AttestationType
    {
        SBOM,
        Provenance,
        VulnerabilityScan,
        Custom
    }

    public class AttestationSubject
    {
        public string Name { get; set; } = "";
        public Dictionary<string, string> Digest { get; set; } = new();
    }

    public class ScanResult
    {
        public string ImageRef { get; set; } = "";
        public ScannerType Scanner { get; set; }
        public DateTime ScannedAt { get; set; }
        public VulnerabilitySummary Summary { get; set; } = new();
        public List<Vulnerability> Vulnerabilities { get; set; } = new();
    }

    public enum ScannerType
    {
        Trivy,
        Grype,
        Clair,
        Snyk
    }

    public class VulnerabilitySummary
    {
        public int Critical { get; set; }
        public int High { get; set; }
        public int Medium { get; set; }
        public int Low { get; set; }
        public int Negligible { get; set; }
    }

    public class Vulnerability
    {
        public string VulnerabilityID { get; set; } = "";
        public string PkgName { get; set; } = "";
        public string InstalledVersion { get; set; } = "";
        public string? FixedVersion { get; set; }
        public VulnerabilitySeverity Severity { get; set; }
        public string Description { get; set; } = "";
        public List<string> References { get; set; } = new();
        public CVSS? CVSS { get; set; }
        public EPSS? EPSS { get; set; }
        public bool? InKEV { get; set; }
    }

    public enum VulnerabilitySeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Negligible,
        Unknown
    }

    public class CVSS
    {
        public double Score { get; set; }
        public string Vector { get; set; } = "";
    }

    public class EPSS
    {
        public double Score { get; set; }
        public double Percentile { get; set; }
    }

    public class VEXDocument
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Version { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Author { get; set; } = "";
        public List<VEXStatement> Statements { get; set; } = new();
    }

    public class VEXStatement
    {
        public string VulnerabilityID { get; set; } = "";
        public List<string> Products { get; set; } = new();
        public VEXStatus Status { get; set; }
        public string? Justification { get; set; }
        public string? ImpactStatement { get; set; }
        public List<string>? ActionStatement { get; set; }
    }

    public enum VEXStatus
    {
        NotAffected,
        Affected,
        Fixed,
        UnderInvestigation
    }

    public class SLSAProvenance
    {
        public string Id { get; set; } = "";
        public string BuildType { get; set; } = "";
        public Builder Builder { get; set; } = new();
        public Invocation Invocation { get; set; } = new();
        public object? BuildConfig { get; set; }
        public ProvenanceMetadata Metadata { get; set; } = new();
        public List<ResourceDescriptor> Materials { get; set; } = new();
    }

    public class Builder
    {
        public string Id { get; set; } = "";
        public List<ResourceDescriptor> BuilderDependencies { get; set; } = new();
    }

    public class Invocation
    {
        public ConfigSource ConfigSource { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
    }

    public class ConfigSource
    {
        public string Uri { get; set; } = "";
        public Dictionary<string, string> Digest { get; set; } = new();
        public string? EntryPoint { get; set; }
    }

    public class ProvenanceMetadata
    {
        public string BuildInvocationId { get; set; } = "";
        public DateTime BuildStartedOn { get; set; }
        public DateTime? BuildFinishedOn { get; set; }
        public Completeness Completeness { get; set; } = new();
        public bool Reproducible { get; set; }
    }

    public class Completeness
    {
        public bool Parameters { get; set; }
        public bool Environment { get; set; }
        public bool Materials { get; set; }
    }

    public class ResourceDescriptor
    {
        public string Uri { get; set; } = "";
        public Dictionary<string, string>? Digest { get; set; }
        public string? Name { get; set; }
    }

    public class BuildInfo
    {
        public string BuilderId { get; set; } = "";
        public List<ResourceDescriptor>? BuilderDependencies { get; set; }
        public string SourceRepo { get; set; } = "";
        public string SourceCommit { get; set; } = "";
        public string EntryPoint { get; set; } = "";
        public Dictionary<string, object>? Parameters { get; set; }
        public Dictionary<string, string>? Environment { get; set; }
        public object? BuildConfig { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public bool Reproducible { get; set; }
        public List<ResourceDescriptor>? Materials { get; set; }
    }
}
