using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// ロールベースアクセス制御 (RBAC) システム
/// </summary>
public class RoleBasedAccessControl
{
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, Role> _roles = new();
    private readonly Dictionary<string, Permission> _permissions = new();
    private readonly Dictionary<string, Resource> _resources = new();
    private readonly AccessAuditLogger _auditLogger;

    public RoleBasedAccessControl(AccessAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
        InitializeDefaultPermissions();
    }

    /// <summary>
    /// ユーザーを追加
    /// </summary>
    public async Task AddUserAsync(string userId, string userName, string[] roles)
    {
        var user = new User
        {
            Id = userId,
            Name = userName,
            Roles = roles.ToList(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _users[userId] = user;
        await _auditLogger.LogAsync(new AccessAuditEvent
        {
            EventType = AuditEventType.UserCreated,
            UserId = userId,
            ResourceId = userId,
            Action = "UserCreated",
            Timestamp = DateTime.UtcNow,
            Success = true
        });
    }

    /// <summary>
    /// ロールを追加
    /// </summary>
    public async Task AddRoleAsync(string roleId, string roleName, string[] permissions)
    {
        var role = new Role
        {
            Id = roleId,
            Name = roleName,
            Permissions = permissions.ToList(),
            IsActive = true
        };

        _roles[roleId] = role;
        await _auditLogger.LogAsync(new AccessAuditEvent
        {
            EventType = AuditEventType.RoleCreated,
            UserId = "system",
            ResourceId = roleId,
            Action = "RoleCreated",
            Timestamp = DateTime.UtcNow,
            Success = true
        });
    }

    /// <summary>
    /// 権限を追加
    /// </summary>
    public void AddPermission(string permissionId, string name, string description, string resourceType)
    {
        var permission = new Permission
        {
            Id = permissionId,
            Name = name,
            Description = description,
            ResourceType = resourceType
        };

        _permissions[permissionId] = permission;
    }

    /// <summary>
    /// リソースを追加
    /// </summary>
    public void AddResource(string resourceId, string type, string ownerId)
    {
        var resource = new Resource
        {
            Id = resourceId,
            Type = type,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };

        _resources[resourceId] = resource;
    }

    /// <summary>
    /// アクセス権限をチェック
    /// </summary>
    public async Task<AccessDecision> CheckAccessAsync(string userId, string resourceId, string action)
    {
        var decision = new AccessDecision
        {
            UserId = userId,
            ResourceId = resourceId,
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // ユーザーの存在とアクティブ状態を確認
            if (!_users.TryGetValue(userId, out var user) || !user.IsActive)
            {
                decision.Granted = false;
                decision.Reason = "User not found or inactive";
                return decision;
            }

            // リソースの存在を確認
            if (!_resources.TryGetValue(resourceId, out var resource))
            {
                decision.Granted = false;
                decision.Reason = "Resource not found";
                return decision;
            }

            // リソース所有者は常にアクセス可能
            if (resource.OwnerId == userId)
            {
                decision.Granted = true;
                decision.Reason = "Resource owner";
                return decision;
            }

            // ユーザーのロールから権限を確認
            var userPermissions = GetUserPermissions(userId);
            var requiredPermission = $"{action}:{resource.Type}";

            if (userPermissions.Contains(requiredPermission))
            {
                decision.Granted = true;
                decision.Reason = "Permission granted";
                return decision;
            }

            // ワイルドカード権限を確認
            if (userPermissions.Contains($"{action}:*") || userPermissions.Contains($"*:{resource.Type}"))
            {
                decision.Granted = true;
                decision.Reason = "Wildcard permission granted";
                return decision;
            }

            decision.Granted = false;
            decision.Reason = "Insufficient permissions";
        }
        finally
        {
            await _auditLogger.LogAsync(new AccessAuditEvent
            {
                EventType = decision.Granted ? AuditEventType.AccessGranted : AuditEventType.AccessDenied,
                UserId = userId,
                ResourceId = resourceId,
                Action = action,
                Timestamp = decision.Timestamp,
                Success = decision.Granted,
                Details = decision.Reason
            });
        }

        return decision;
    }

    /// <summary>
    /// ユーザーにロールを割り当て
    /// </summary>
    public async Task AssignRoleAsync(string userId, string roleId)
    {
        if (_users.TryGetValue(userId, out var user) && _roles.ContainsKey(roleId))
        {
            if (!user.Roles.Contains(roleId))
            {
                user.Roles.Add(roleId);
                await _auditLogger.LogAsync(new AccessAuditEvent
                {
                    EventType = AuditEventType.RoleAssigned,
                    UserId = "system",
                    ResourceId = userId,
                    Action = "RoleAssigned",
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    Details = $"Role: {roleId}"
                });
            }
        }
    }

    /// <summary>
    /// ユーザーからロールを削除
    /// </summary>
    public async Task RevokeRoleAsync(string userId, string roleId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            if (user.Roles.Remove(roleId))
            {
                await _auditLogger.LogAsync(new AccessAuditEvent
                {
                    EventType = AuditEventType.RoleRevoked,
                    UserId = "system",
                    ResourceId = userId,
                    Action = "RoleRevoked",
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    Details = $"Role: {roleId}"
                });
            }
        }
    }

    /// <summary>
    /// ユーザーの権限を取得
    /// </summary>
    public List<string> GetUserPermissions(string userId)
    {
        if (!_users.TryGetValue(userId, out var user) || !user.IsActive)
        {
            return new List<string>();
        }

        var permissions = new HashSet<string>();

        foreach (var roleId in user.Roles)
        {
            if (_roles.TryGetValue(roleId, out var role) && role.IsActive)
            {
                foreach (var permissionId in role.Permissions)
                {
                    permissions.Add(permissionId);
                }
            }
        }

        return permissions.ToList();
    }

    /// <summary>
    /// ユーザーを無効化
    /// </summary>
    public async Task DeactivateUserAsync(string userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            user.IsActive = false;
            await _auditLogger.LogAsync(new AccessAuditEvent
            {
                EventType = AuditEventType.UserDeactivated,
                UserId = "system",
                ResourceId = userId,
                Action = "UserDeactivated",
                Timestamp = DateTime.UtcNow,
                Success = true
            });
        }
    }

    private void InitializeDefaultPermissions()
    {
        // ワークフロー権限
        AddPermission("workflow:read", "Read Workflow", "Read workflow definitions", "workflow");
        AddPermission("workflow:write", "Write Workflow", "Create and modify workflows", "workflow");
        AddPermission("workflow:execute", "Execute Workflow", "Execute workflows", "workflow");
        AddPermission("workflow:delete", "Delete Workflow", "Delete workflows", "workflow");

        // ルール権限
        AddPermission("rule:read", "Read Rule", "Read automation rules", "rule");
        AddPermission("rule:write", "Write Rule", "Create and modify rules", "rule");
        AddPermission("rule:delete", "Delete Rule", "Delete rules", "rule");

        // システム権限
        AddPermission("system:admin", "System Admin", "Full system access", "system");
        AddPermission("system:monitor", "System Monitor", "Monitor system status", "system");
        AddPermission("system:config", "System Config", "Configure system settings", "system");

        // ユーザー権限
        AddPermission("user:manage", "Manage Users", "Manage user accounts", "user");
        AddPermission("role:manage", "Manage Roles", "Manage user roles", "role");
    }

    // データモデル
    public class User
    {
        public string Id = "";
        public string Name = "";
        public List<string> Roles = new();
        public bool IsActive;
        public DateTime CreatedAt;
        public DateTime? LastLogin;
    }

    public class Role
    {
        public string Id = "";
        public string Name = "";
        public List<string> Permissions = new();
        public bool IsActive;
    }

    public class Permission
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string ResourceType = "";
    }

    public class Resource
    {
        public string Id = "";
        public string Type = "";
        public string OwnerId = "";
        public DateTime CreatedAt;
    }

    public class AccessDecision
    {
        public string UserId = "";
        public string ResourceId = "";
        public string Action = "";
        public bool Granted;
        public string Reason = "";
        public DateTime Timestamp;
    }
}

/// <summary>
/// 属性ベースアクセス制御 (ABAC)
/// </summary>
public class AttributeBasedAccessControl
{
    private readonly List<AccessPolicy> _policies = new();
    private readonly AccessAuditLogger _auditLogger;

    public AttributeBasedAccessControl(AccessAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// アクセスポリシーを追加
    /// </summary>
    public void AddPolicy(AccessPolicy policy)
    {
        _policies.Add(policy);
    }

    /// <summary>
    /// 属性に基づいてアクセスを評価
    /// </summary>
    public async Task<AccessDecision> EvaluateAccessAsync(
        Dictionary<string, object> subjectAttributes,
        Dictionary<string, object> resourceAttributes,
        Dictionary<string, object> environmentAttributes,
        string action)
    {
        var decision = new AccessDecision
        {
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        foreach (var policy in _policies.OrderBy(p => p.Priority))
        {
            if (policy.Evaluate(subjectAttributes, resourceAttributes, environmentAttributes, action))
            {
                decision.Granted = policy.Effect == PolicyEffect.Allow;
                decision.Reason = policy.Name;
                decision.PolicyId = policy.Id;

                await _auditLogger.LogAsync(new AccessAuditEvent
                {
                    EventType = decision.Granted ? AuditEventType.AccessGranted : AuditEventType.AccessDenied,
                    UserId = subjectAttributes.GetValueOrDefault("userId", "unknown")?.ToString() ?? "unknown",
                    ResourceId = resourceAttributes.GetValueOrDefault("resourceId", "unknown")?.ToString() ?? "unknown",
                    Action = action,
                    Timestamp = decision.Timestamp,
                    Success = decision.Granted,
                    Details = $"Policy: {policy.Name}"
                });

                return decision;
            }
        }

        decision.Granted = false;
        decision.Reason = "No matching policy";

        await _auditLogger.LogAsync(new AccessAuditEvent
        {
            EventType = AuditEventType.AccessDenied,
            UserId = subjectAttributes.GetValueOrDefault("userId", "unknown")?.ToString() ?? "unknown",
            ResourceId = resourceAttributes.GetValueOrDefault("resourceId", "unknown")?.ToString() ?? "unknown",
            Action = action,
            Timestamp = decision.Timestamp,
            Success = false,
            Details = "No matching policy"
        });

        return decision;
    }

    // データモデル
    public class AccessPolicy
    {
        public string Id = "";
        public string Name = "";
        public int Priority;
        public PolicyEffect Effect;
        public List<PolicyRule> Rules = new();

        public bool Evaluate(
            Dictionary<string, object> subject,
            Dictionary<string, object> resource,
            Dictionary<string, object> environment,
            string action)
        {
            return Rules.All(rule => rule.Evaluate(subject, resource, environment, action));
        }
    }

    public class PolicyRule
    {
        public string Attribute;
        public string Operator;
        public object Value;

        public bool Evaluate(
            Dictionary<string, object> subject,
            Dictionary<string, object> resource,
            Dictionary<string, object> environment,
            string action)
        {
            object? actualValue = null;

            // 属性のソースを決定
            if (Attribute.StartsWith("subject."))
            {
                actualValue = subject.GetValueOrDefault(Attribute.Substring(8));
            }
            else if (Attribute.StartsWith("resource."))
            {
                actualValue = resource.GetValueOrDefault(Attribute.Substring(9));
            }
            else if (Attribute.StartsWith("environment."))
            {
                actualValue = environment.GetValueOrDefault(Attribute.Substring(12));
            }
            else if (Attribute == "action")
            {
                actualValue = action;
            }

            return EvaluateCondition(actualValue, Operator, Value);
        }

        private bool EvaluateCondition(object? actualValue, string op, object expectedValue)
        {
            if (actualValue == null) return false;

            switch (op.ToLower())
            {
                case "equals":
                case "eq":
                    return actualValue.Equals(expectedValue);
                case "not_equals":
                case "ne":
                    return !actualValue.Equals(expectedValue);
                case "contains":
                    return actualValue.ToString()?.Contains(expectedValue.ToString() ?? "") == true;
                case "starts_with":
                    return actualValue.ToString()?.StartsWith(expectedValue.ToString() ?? "") == true;
                case "ends_with":
                    return actualValue.ToString()?.EndsWith(expectedValue.ToString() ?? "") == true;
                case "greater_than":
                    return CompareValues(actualValue, expectedValue) > 0;
                case "less_than":
                    return CompareValues(actualValue, expectedValue) < 0;
                case "in":
                    if (expectedValue is IEnumerable<object> list)
                    {
                        return list.Contains(actualValue);
                    }
                    return false;
                default:
                    return false;
            }
        }

        private int CompareValues(object a, object b)
        {
            if (a is IComparable comparableA && b is IComparable comparableB)
            {
                return comparableA.CompareTo(comparableB);
            }
            return 0;
        }
    }

    public enum PolicyEffect
    {
        Allow,
        Deny
    }

    public class AccessDecision
    {
        public string Action = "";
        public bool Granted;
        public string Reason = "";
        public string? PolicyId;
        public DateTime Timestamp;
    }
}

/// <summary>
/// アクセス監査ロガー
/// </summary>
public class AccessAuditLogger
{
    private readonly List<AccessAuditEvent> _auditLog = new();
    private readonly object _logLock = new();

    public async Task LogAsync(AccessAuditEvent auditEvent)
    {
        lock (_logLock)
        {
            _auditLog.Add(auditEvent);

            // 古いログを削除（最新10000件のみ保持）
            if (_auditLog.Count > 10000)
            {
                _auditLog.RemoveRange(0, _auditLog.Count - 10000);
            }
        }

        // 実際の実装ではファイルやデータベースに永続化
        await Task.CompletedTask;
    }

    public IEnumerable<AccessAuditEvent> GetAuditLog(
        string? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AuditEventType? eventType = null)
    {
        lock (_logLock)
        {
            var query = _auditLog.AsEnumerable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(e => e.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value);

            if (eventType.HasValue)
                query = query.Where(e => e.EventType == eventType.Value);

            return query.OrderByDescending(e => e.Timestamp).ToList();
        }
    }

    public void ClearAuditLog()
    {
        lock (_logLock)
        {
            _auditLog.Clear();
        }
    }
}

/// <summary>
/// 監査イベント
/// </summary>
public class AccessAuditEvent
{
    public AuditEventType EventType;
    public string UserId = "";
    public string ResourceId = "";
    public string Action = "";
    public DateTime Timestamp;
    public bool Success;
    public string? Details;
    public string? IpAddress;
    public string? UserAgent;
}

/// <summary>
/// 監査イベントタイプ
/// </summary>
public enum AuditEventType
{
    Login,
    Logout,
    AccessGranted,
    AccessDenied,
    UserCreated,
    UserUpdated,
    UserDeactivated,
    RoleAssigned,
    RoleRevoked,
    RoleCreated,
    PermissionGranted,
    PermissionRevoked,
    ResourceCreated,
    ResourceModified,
    ResourceDeleted,
    SecurityViolation,
    SuspiciousActivity
}
