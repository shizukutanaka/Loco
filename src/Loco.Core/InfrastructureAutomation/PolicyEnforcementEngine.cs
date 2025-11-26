using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.InfrastructureAutomation
{
    /// <summary>
    /// Policy Enforcement Engine implementing OPA Gatekeeper and Kyverno patterns
    ///
    /// Research sources:
    /// - Kyverno vs OPA/Gatekeeper: https://nirmata.com/2025/02/07/kubernetes-policy-comparison-kyverno-vs-opa-gatekeeper/
    /// - Policy as Code 2025: https://policyascode.dev/blog/opa-gatekeeper-vs-kyverno/
    /// - Admission Controllers: https://kubernetes.io/docs/reference/access-authn-authz/extensible-admission-controllers/
    /// - Policy Enforcement: https://www.stackgenie.io/analysing-kubernetes-policy-enforcement-using-opa-gatekeeper-and-kyverno/
    ///
    /// Capabilities:
    /// - OPA/Gatekeeper Constraint Templates and Constraints
    /// - Kyverno ClusterPolicy and Policy with YAML-based rules
    /// - Validating admission control for resource validation
    /// - Mutating admission control for automatic resource modification
    /// - Generate policies for creating default resources
    /// - Policy reporting and audit mode
    /// - CEL-based admission control for lightweight validation
    /// - Multi-tenancy and namespace-scoped policies
    /// </summary>
    public interface IPolicyEnforcementEngine
    {
        Task<Policy> CreatePolicyAsync(string tenantId, Policy policy, CancellationToken cancellation = default);
        Task<PolicyViolation> ValidateResourceAsync(string tenantId, string policyId, object resource, CancellationToken cancellation = default);
        Task<object> MutateResourceAsync(string tenantId, string policyId, object resource, CancellationToken cancellation = default);
        Task<List<object>> GenerateResourcesAsync(string tenantId, string policyId, object trigger, CancellationToken cancellation = default);
        Task<ConstraintTemplate> CreateConstraintTemplateAsync(string tenantId, ConstraintTemplate template, CancellationToken cancellation = default);
        Task<Constraint> CreateConstraintAsync(string tenantId, Constraint constraint, CancellationToken cancellation = default);
        Task<PolicyReport> GenerateReportAsync(string tenantId, string? namespaceFilter = null, CancellationToken cancellation = default);
        Task<List<PolicyViolation>> AuditNamespaceAsync(string tenantId, string namespace_name, CancellationToken cancellation = default);
    }

    public class PolicyEnforcementEngine : IPolicyEnforcementEngine
    {
        private readonly Dictionary<string, Policy> _policies = new();
        private readonly Dictionary<string, ConstraintTemplate> _templates = new();
        private readonly Dictionary<string, Constraint> _constraints = new();
        private readonly Dictionary<string, List<PolicyViolation>> _violations = new();
        private readonly Dictionary<string, List<object>> _generatedResources = new();

        public async Task<Policy> CreatePolicyAsync(string tenantId, Policy policy, CancellationToken cancellation = default)
        {
            policy.Id = Guid.NewGuid().ToString();
            policy.TenantId = tenantId;
            policy.CreatedAt = DateTime.UtcNow;
            policy.Status = new PolicyStatus
            {
                Ready = true,
                RuleCount = policy.Spec.Rules?.Count ?? 0
            };

            _policies[$"{tenantId}:{policy.Id}"] = policy;

            return await Task.FromResult(policy);
        }

        public async Task<PolicyViolation> ValidateResourceAsync(string tenantId, string policyId, object resource, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{policyId}";
            if (!_policies.TryGetValue(key, out var policy))
                throw new InvalidOperationException($"Policy {policyId} not found");

            var violation = new PolicyViolation
            {
                PolicyId = policyId,
                PolicyName = policy.Name,
                Resource = resource,
                Violations = new List<RuleViolation>()
            };

            // Extract resource metadata
            var resourceDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(resource)) ?? new Dictionary<string, object>();

            // Check resource selector
            if (!MatchesResourceSelector(policy.Spec.ResourceSelector, resourceDict))
            {
                violation.Allowed = true;
                return await Task.FromResult(violation);
            }

            // Evaluate validation rules
            foreach (var rule in policy.Spec.Rules ?? new List<PolicyRule>())
            {
                if (rule.Validate != null)
                {
                    var ruleViolation = await EvaluateValidationRuleAsync(tenantId, rule, resourceDict, cancellation);
                    if (ruleViolation != null)
                    {
                        violation.Violations.Add(ruleViolation);
                    }
                }
            }

            violation.Allowed = !violation.Violations.Any();

            // Store violation if not allowed
            if (!violation.Allowed)
            {
                if (!_violations.ContainsKey(key))
                    _violations[key] = new List<PolicyViolation>();
                _violations[key].Add(violation);
            }

            return await Task.FromResult(violation);
        }

        public async Task<object> MutateResourceAsync(string tenantId, string policyId, object resource, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{policyId}";
            if (!_policies.TryGetValue(key, out var policy))
                throw new InvalidOperationException($"Policy {policyId} not found");

            var resourceDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(resource)) ?? new Dictionary<string, object>();

            // Check resource selector
            if (!MatchesResourceSelector(policy.Spec.ResourceSelector, resourceDict))
            {
                return await Task.FromResult(resource);
            }

            // Apply mutation rules
            foreach (var rule in policy.Spec.Rules ?? new List<PolicyRule>())
            {
                if (rule.Mutate != null)
                {
                    resourceDict = await ApplyMutationAsync(tenantId, rule.Mutate, resourceDict, cancellation);
                }
            }

            return await Task.FromResult(resourceDict);
        }

        public async Task<List<object>> GenerateResourcesAsync(string tenantId, string policyId, object trigger, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{policyId}";
            if (!_policies.TryGetValue(key, out var policy))
                throw new InvalidOperationException($"Policy {policyId} not found");

            var generated = new List<object>();

            var triggerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(trigger)) ?? new Dictionary<string, object>();

            // Check resource selector
            if (!MatchesResourceSelector(policy.Spec.ResourceSelector, triggerDict))
            {
                return await Task.FromResult(generated);
            }

            // Apply generation rules
            foreach (var rule in policy.Spec.Rules ?? new List<PolicyRule>())
            {
                if (rule.Generate != null)
                {
                    var resources = await ApplyGenerationAsync(tenantId, rule.Generate, triggerDict, cancellation);
                    generated.AddRange(resources);
                }
            }

            // Store generated resources
            if (!_generatedResources.ContainsKey(key))
                _generatedResources[key] = new List<object>();
            _generatedResources[key].AddRange(generated);

            return await Task.FromResult(generated);
        }

        public async Task<ConstraintTemplate> CreateConstraintTemplateAsync(string tenantId, ConstraintTemplate template, CancellationToken cancellation = default)
        {
            template.Id = Guid.NewGuid().ToString();
            template.TenantId = tenantId;
            template.CreatedAt = DateTime.UtcNow;

            _templates[$"{tenantId}:{template.Id}"] = template;

            return await Task.FromResult(template);
        }

        public async Task<Constraint> CreateConstraintAsync(string tenantId, Constraint constraint, CancellationToken cancellation = default)
        {
            constraint.Id = Guid.NewGuid().ToString();
            constraint.TenantId = tenantId;
            constraint.CreatedAt = DateTime.UtcNow;
            constraint.Status = new ConstraintStatus
            {
                TotalViolations = 0
            };

            _constraints[$"{tenantId}:{constraint.Id}"] = constraint;

            return await Task.FromResult(constraint);
        }

        public async Task<PolicyReport> GenerateReportAsync(string tenantId, string? namespaceFilter = null, CancellationToken cancellation = default)
        {
            var report = new PolicyReport
            {
                GeneratedAt = DateTime.UtcNow,
                Namespace = namespaceFilter,
                Results = new List<PolicyReportResult>()
            };

            // Aggregate violations from all policies
            foreach (var kvp in _violations)
            {
                if (!kvp.Key.StartsWith($"{tenantId}:"))
                    continue;

                foreach (var violation in kvp.Value)
                {
                    if (namespaceFilter != null && !MatchesNamespace(violation.Resource, namespaceFilter))
                        continue;

                    foreach (var ruleViolation in violation.Violations)
                    {
                        report.Results.Add(new PolicyReportResult
                        {
                            Policy = violation.PolicyName,
                            Rule = ruleViolation.RuleName,
                            Result = PolicyResult.Fail,
                            Severity = ruleViolation.Severity,
                            Message = ruleViolation.Message,
                            Resource = new ResourceReference
                            {
                                ApiVersion = GetStringValue(violation.Resource, "apiVersion"),
                                Kind = GetStringValue(violation.Resource, "kind"),
                                Name = GetStringValue(violation.Resource, "metadata.name"),
                                Namespace = GetStringValue(violation.Resource, "metadata.namespace")
                            }
                        });
                    }
                }
            }

            // Count summaries
            report.Summary = new PolicyReportSummary
            {
                Pass = report.Results.Count(r => r.Result == PolicyResult.Pass),
                Fail = report.Results.Count(r => r.Result == PolicyResult.Fail),
                Warn = report.Results.Count(r => r.Result == PolicyResult.Warn),
                Error = report.Results.Count(r => r.Result == PolicyResult.Error),
                Skip = report.Results.Count(r => r.Result == PolicyResult.Skip)
            };

            return await Task.FromResult(report);
        }

        public async Task<List<PolicyViolation>> AuditNamespaceAsync(string tenantId, string namespace_name, CancellationToken cancellation = default)
        {
            var violations = new List<PolicyViolation>();

            // Simulate fetching all resources in namespace
            var resources = await GetNamespaceResourcesAsync(tenantId, namespace_name, cancellation);

            // Validate each resource against all policies
            foreach (var policy in _policies.Values.Where(p => p.TenantId == tenantId))
            {
                foreach (var resource in resources)
                {
                    var violation = await ValidateResourceAsync(tenantId, policy.Id!, resource, cancellation);
                    if (!violation.Allowed)
                    {
                        violations.Add(violation);
                    }
                }
            }

            return await Task.FromResult(violations);
        }

        // Private helper methods

        private bool MatchesResourceSelector(ResourceSelector? selector, Dictionary<string, object> resource)
        {
            if (selector == null)
                return true;

            // Check kinds
            if (selector.Kinds?.Any() == true)
            {
                var kind = GetStringValue(resource, "kind");
                if (!selector.Kinds.Contains(kind))
                    return false;
            }

            // Check namespaces
            if (selector.Namespaces?.Any() == true)
            {
                var ns = GetStringValue(resource, "metadata.namespace");
                if (!selector.Namespaces.Contains(ns))
                    return false;
            }

            // Check labels
            if (selector.MatchLabels?.Any() == true)
            {
                var labels = GetDictValue(resource, "metadata.labels");
                foreach (var kvp in selector.MatchLabels)
                {
                    if (!labels.TryGetValue(kvp.Key, out var value) || value?.ToString() != kvp.Value)
                        return false;
                }
            }

            return true;
        }

        private async Task<RuleViolation?> EvaluateValidationRuleAsync(string tenantId, PolicyRule rule, Dictionary<string, object> resource, CancellationToken cancellation)
        {
            await Task.CompletedTask;

            var validation = rule.Validate!;

            // Pattern validation
            if (validation.Pattern != null)
            {
                if (!MatchesPattern(resource, validation.Pattern))
                {
                    return new RuleViolation
                    {
                        RuleName = rule.Name,
                        Message = validation.Message ?? $"Resource does not match required pattern",
                        Severity = rule.ValidationFailureAction == ValidationFailureAction.Enforce ? "high" : "medium"
                    };
                }
            }

            // AnyPattern validation
            if (validation.AnyPattern?.Any() == true)
            {
                var matchesAny = validation.AnyPattern.Any(pattern => MatchesPattern(resource, pattern));
                if (!matchesAny)
                {
                    return new RuleViolation
                    {
                        RuleName = rule.Name,
                        Message = validation.Message ?? "Resource does not match any required pattern",
                        Severity = "medium"
                    };
                }
            }

            // Deny conditions
            if (validation.Deny?.Any() == true)
            {
                foreach (var deny in validation.Deny)
                {
                    if (EvaluateConditions(resource, deny.Conditions))
                    {
                        return new RuleViolation
                        {
                            RuleName = rule.Name,
                            Message = deny.Message ?? "Resource matches deny condition",
                            Severity = "high"
                        };
                    }
                }
            }

            // CEL validation
            if (!string.IsNullOrEmpty(validation.CEL))
            {
                var result = EvaluateCEL(resource, validation.CEL);
                if (!result)
                {
                    return new RuleViolation
                    {
                        RuleName = rule.Name,
                        Message = validation.Message ?? "CEL validation failed",
                        Severity = "medium"
                    };
                }
            }

            return null;
        }

        private async Task<Dictionary<string, object>> ApplyMutationAsync(string tenantId, MutateRule mutate, Dictionary<string, object> resource, CancellationToken cancellation)
        {
            await Task.CompletedTask;

            // Strategic merge patch
            if (mutate.PatchStrategicMerge != null)
            {
                resource = MergeObjects(resource, mutate.PatchStrategicMerge);
            }

            // JSON Patch (RFC 6902)
            if (mutate.PatchesJson6902?.Any() == true)
            {
                foreach (var patch in mutate.PatchesJson6902)
                {
                    resource = ApplyJsonPatch(resource, patch);
                }
            }

            // foreach mutation
            if (mutate.Foreach?.Any() == true)
            {
                foreach (var foreachMutation in mutate.Foreach)
                {
                    var items = GetArrayValue(resource, foreachMutation.List);
                    foreach (var item in items)
                    {
                        // Apply mutation to each item
                        if (foreachMutation.PatchStrategicMerge != null)
                        {
                            var itemDict = item as Dictionary<string, object> ?? new Dictionary<string, object>();
                            MergeObjects(itemDict, foreachMutation.PatchStrategicMerge);
                        }
                    }
                }
            }

            return resource;
        }

        private async Task<List<object>> ApplyGenerationAsync(string tenantId, GenerateRule generate, Dictionary<string, object> trigger, CancellationToken cancellation)
        {
            await Task.CompletedTask;

            var generated = new List<object>();

            if (generate.Kind == GenerateKind.ConfigMap || generate.Kind == GenerateKind.Secret)
            {
                // Generate resource from template
                var resource = new Dictionary<string, object>
                {
                    ["apiVersion"] = "v1",
                    ["kind"] = generate.Kind.ToString(),
                    ["metadata"] = new Dictionary<string, object>
                    {
                        ["name"] = generate.Name ?? $"{GetStringValue(trigger, "metadata.name")}-{generate.Kind.ToString().ToLower()}",
                        ["namespace"] = generate.Namespace ?? GetStringValue(trigger, "metadata.namespace")
                    },
                    ["data"] = generate.Data ?? new Dictionary<string, object>()
                };

                // Apply synchronization if configured
                if (generate.Synchronize)
                {
                    // Mark for synchronization (will be updated when trigger changes)
                    ((Dictionary<string, object>)resource["metadata"])["annotations"] = new Dictionary<string, object>
                    {
                        ["kyverno.io/synchronize"] = "true"
                    };
                }

                generated.Add(resource);
            }
            else if (generate.CloneFrom != null)
            {
                // Clone existing resource
                var sourceResource = await GetResourceAsync(tenantId, generate.CloneFrom, cancellation);
                if (sourceResource != null)
                {
                    generated.Add(sourceResource);
                }
            }

            return generated;
        }

        private bool MatchesPattern(Dictionary<string, object> resource, Dictionary<string, object> pattern)
        {
            foreach (var kvp in pattern)
            {
                if (!resource.TryGetValue(kvp.Key, out var value))
                    return false;

                if (kvp.Value is Dictionary<string, object> nestedPattern && value is Dictionary<string, object> nestedResource)
                {
                    if (!MatchesPattern(nestedResource, nestedPattern))
                        return false;
                }
                else if (!Equals(value, kvp.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private bool EvaluateConditions(Dictionary<string, object> resource, List<Condition>? conditions)
        {
            if (conditions == null || !conditions.Any())
                return true;

            foreach (var condition in conditions)
            {
                var value = GetValue(resource, condition.Key);
                var conditionValue = condition.Value;

                var result = condition.Operator switch
                {
                    ConditionOperator.Equals => Equals(value, conditionValue),
                    ConditionOperator.NotEquals => !Equals(value, conditionValue),
                    ConditionOperator.In => conditionValue is List<object> list && list.Contains(value),
                    ConditionOperator.NotIn => conditionValue is List<object> notInList && !notInList.Contains(value),
                    _ => false
                };

                if (!result)
                    return false;
            }

            return true;
        }

        private bool EvaluateCEL(Dictionary<string, object> resource, string celExpression)
        {
            // Simplified CEL evaluation
            // In production, use CEL library (e.g., Google.Api.CommonProtos.Expr)

            // Example: "object.spec.replicas > 3"
            if (celExpression.Contains(">"))
            {
                var parts = celExpression.Split('>');
                var fieldPath = parts[0].Trim().Replace("object.", "");
                var threshold = int.Parse(parts[1].Trim());
                var value = GetValue(resource, fieldPath);

                return value is int intValue && intValue > threshold;
            }

            return true; // Default allow
        }

        private Dictionary<string, object> MergeObjects(Dictionary<string, object> target, Dictionary<string, object> patch)
        {
            foreach (var kvp in patch)
            {
                if (target.TryGetValue(kvp.Key, out var existingValue) &&
                    existingValue is Dictionary<string, object> existingDict &&
                    kvp.Value is Dictionary<string, object> patchDict)
                {
                    target[kvp.Key] = MergeObjects(existingDict, patchDict);
                }
                else
                {
                    target[kvp.Key] = kvp.Value;
                }
            }

            return target;
        }

        private Dictionary<string, object> ApplyJsonPatch(Dictionary<string, object> resource, JsonPatch patch)
        {
            // Simplified JSON Patch implementation
            switch (patch.Op)
            {
                case "add":
                case "replace":
                    SetValue(resource, patch.Path, patch.Value);
                    break;
                case "remove":
                    RemoveValue(resource, patch.Path);
                    break;
            }

            return resource;
        }

        private bool MatchesNamespace(object resource, string namespace_name)
        {
            return GetStringValue(resource, "metadata.namespace") == namespace_name;
        }

        private string GetStringValue(object obj, string path)
        {
            var value = GetValue(obj, path);
            return value?.ToString() ?? "";
        }

        private Dictionary<string, object> GetDictValue(object obj, string path)
        {
            var value = GetValue(obj, path);
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private List<object> GetArrayValue(object obj, string path)
        {
            var value = GetValue(obj, path);
            return value as List<object> ?? new List<object>();
        }

        private object? GetValue(object obj, string path)
        {
            var current = obj;
            var parts = path.Split('.');

            foreach (var part in parts)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                        return null;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        private void SetValue(Dictionary<string, object> obj, string path, object? value)
        {
            var parts = path.TrimStart('/').Split('/');
            var current = obj;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!current.TryGetValue(parts[i], out var next) || next is not Dictionary<string, object> nextDict)
                {
                    nextDict = new Dictionary<string, object>();
                    current[parts[i]] = nextDict;
                }
                current = nextDict;
            }

            if (value != null)
                current[parts[^1]] = value;
        }

        private void RemoveValue(Dictionary<string, object> obj, string path)
        {
            var parts = path.TrimStart('/').Split('/');
            var current = obj;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!current.TryGetValue(parts[i], out var next) || next is not Dictionary<string, object> nextDict)
                    return;
                current = nextDict;
            }

            current.Remove(parts[^1]);
        }

        private async Task<List<object>> GetNamespaceResourcesAsync(string tenantId, string namespace_name, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate fetching resources
            return new List<object>();
        }

        private async Task<object?> GetResourceAsync(string tenantId, CloneFrom cloneFrom, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);
            return null; // Simulate resource not found
        }
    }

    // Model classes

    public class Policy
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Name { get; set; } = "";
        public PolicyType Type { get; set; }
        public PolicySpec Spec { get; set; } = new();
        public PolicyStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public enum PolicyType
    {
        Kyverno,
        OPA
    }

    public class PolicySpec
    {
        public ResourceSelector? ResourceSelector { get; set; }
        public List<PolicyRule>? Rules { get; set; }
        public bool Background { get; set; } = true;
        public ValidationFailureAction ValidationFailureAction { get; set; } = ValidationFailureAction.Audit;
    }

    public class ResourceSelector
    {
        public List<string>? Kinds { get; set; }
        public List<string>? Namespaces { get; set; }
        public Dictionary<string, string>? MatchLabels { get; set; }
        public List<LabelSelectorRequirement>? MatchExpressions { get; set; }
    }

    public class LabelSelectorRequirement
    {
        public string Key { get; set; } = "";
        public string Operator { get; set; } = "";
        public List<string>? Values { get; set; }
    }

    public class PolicyRule
    {
        public string Name { get; set; } = "";
        public ValidationRule? Validate { get; set; }
        public MutateRule? Mutate { get; set; }
        public GenerateRule? Generate { get; set; }
        public VerifyRule? Verify { get; set; }
        public ValidationFailureAction ValidationFailureAction { get; set; } = ValidationFailureAction.Audit;
    }

    public enum ValidationFailureAction
    {
        Audit,
        Enforce
    }

    public class ValidationRule
    {
        public string? Message { get; set; }
        public Dictionary<string, object>? Pattern { get; set; }
        public List<Dictionary<string, object>>? AnyPattern { get; set; }
        public List<DenyCondition>? Deny { get; set; }
        public string? CEL { get; set; }
    }

    public class DenyCondition
    {
        public List<Condition>? Conditions { get; set; }
        public string? Message { get; set; }
    }

    public class Condition
    {
        public string Key { get; set; } = "";
        public ConditionOperator Operator { get; set; }
        public object? Value { get; set; }
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        In,
        NotIn,
        GreaterThan,
        LessThan
    }

    public class MutateRule
    {
        public Dictionary<string, object>? PatchStrategicMerge { get; set; }
        public List<JsonPatch>? PatchesJson6902 { get; set; }
        public List<ForeachMutation>? Foreach { get; set; }
    }

    public class JsonPatch
    {
        public string Op { get; set; } = "";
        public string Path { get; set; } = "";
        public object? Value { get; set; }
    }

    public class ForeachMutation
    {
        public string List { get; set; } = "";
        public Dictionary<string, object>? PatchStrategicMerge { get; set; }
    }

    public class GenerateRule
    {
        public GenerateKind Kind { get; set; }
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public CloneFrom? CloneFrom { get; set; }
        public bool Synchronize { get; set; }
    }

    public enum GenerateKind
    {
        ConfigMap,
        Secret,
        NetworkPolicy,
        ResourceQuota,
        LimitRange
    }

    public class CloneFrom
    {
        public string Namespace { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class VerifyRule
    {
        public List<ImageVerification>? Images { get; set; }
    }

    public class ImageVerification
    {
        public string ImageReferences { get; set; } = "";
        public List<Attestor>? Attestors { get; set; }
        public AttestationType? Attestations { get; set; }
    }

    public class Attestor
    {
        public string? PublicKey { get; set; }
        public string? Repository { get; set; }
    }

    public class AttestationType
    {
        public List<Attestation>? Predicates { get; set; }
    }

    public class Attestation
    {
        public string PredicateType { get; set; } = "";
        public Dictionary<string, object>? Conditions { get; set; }
    }

    public class PolicyStatus
    {
        public bool Ready { get; set; }
        public int RuleCount { get; set; }
        public Dictionary<string, int>? ValidationStats { get; set; }
    }

    public class PolicyViolation
    {
        public string PolicyId { get; set; } = "";
        public string PolicyName { get; set; } = "";
        public object Resource { get; set; } = new();
        public bool Allowed { get; set; }
        public List<RuleViolation> Violations { get; set; } = new();
    }

    public class RuleViolation
    {
        public string RuleName { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "";
    }

    public class ConstraintTemplate
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Name { get; set; } = "";
        public ConstraintTemplateSpec Spec { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ConstraintTemplateSpec
    {
        public CRD Crd { get; set; } = new();
        public List<Target> Targets { get; set; } = new();
    }

    public class CRD
    {
        public CRDSpec Spec { get; set; } = new();
    }

    public class CRDSpec
    {
        public List<string> Names { get; set; } = new();
        public Validation? Validation { get; set; }
    }

    public class Validation
    {
        public Dictionary<string, object>? OpenAPIV3Schema { get; set; }
    }

    public class Target
    {
        public string Target { get; set; } = "";
        public string Rego { get; set; } = "";
    }

    public class Constraint
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public ConstraintSpec Spec { get; set; } = new();
        public ConstraintStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ConstraintSpec
    {
        public Match? Match { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class Match
    {
        public List<string>? Kinds { get; set; }
        public List<string>? Namespaces { get; set; }
        public List<string>? ExcludedNamespaces { get; set; }
    }

    public class ConstraintStatus
    {
        public int TotalViolations { get; set; }
        public List<ConstraintViolation>? Violations { get; set; }
    }

    public class ConstraintViolation
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class PolicyReport
    {
        public DateTime GeneratedAt { get; set; }
        public string? Namespace { get; set; }
        public PolicyReportSummary Summary { get; set; } = new();
        public List<PolicyReportResult> Results { get; set; } = new();
    }

    public class PolicyReportSummary
    {
        public int Pass { get; set; }
        public int Fail { get; set; }
        public int Warn { get; set; }
        public int Error { get; set; }
        public int Skip { get; set; }
    }

    public class PolicyReportResult
    {
        public string Policy { get; set; } = "";
        public string Rule { get; set; } = "";
        public PolicyResult Result { get; set; }
        public string Severity { get; set; } = "";
        public string Message { get; set; } = "";
        public ResourceReference Resource { get; set; } = new();
    }

    public enum PolicyResult
    {
        Pass,
        Fail,
        Warn,
        Error,
        Skip
    }

    public class ResourceReference
    {
        public string ApiVersion { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Namespace { get; set; }
    }
}
