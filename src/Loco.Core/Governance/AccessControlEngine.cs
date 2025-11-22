// Phase 10: Advanced Access Control & RBAC
// Role-based access control, attribute-based access control, and permissions management
// Enterprise-grade access control with delegation and least privilege enforcement

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Governance;

/// <summary>
/// Role definition
/// </summary>
public class Role
{
    public string RoleId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public bool IsBuiltIn { get; set; }
    public int Priority { get; set; } = 100; // Higher = stronger
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User role assignment
/// </summary>
public class RoleAssignment
{
    public string AssignmentId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>
/// Permission definition
/// </summary>
public class Permission
{
    public string PermissionId { get; set; } = Guid.NewGuid().ToString();
    public string PermissionName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty; // workflow, execution, settings, audit
    public string Action { get; set; } = string.Empty; // read, write, delete, approve, execute
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Resource-level access control
/// </summary>
public class ResourceACL
{
    public string ACLId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public Dictionary<string, List<string>> AccessMap { get; set; } = new(); // userId -> permissions
    public string OwnerUserId { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Access request
/// </summary>
public class AccessRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string RequestedAction { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, approved, denied, revoked
    public int DurationHours { get; set; } = 8;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? ApprovedBy { get; set; }
}

/// <summary>
/// Attribute-based access control (ABAC) policy
/// </summary>
public class ABACPolicy
{
    public string PolicyId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty; // allow, deny
    public Dictionary<string, object> Conditions { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Access control interface
/// </summary>
public interface IAccessControlEngine
{
    // Roles
    Task<Role> CreateRoleAsync(
        string tenantId,
        Role role,
        CancellationToken ct = default);

    Task<Role?> GetRoleAsync(
        string roleId,
        CancellationToken ct = default);

    Task<List<Role>> GetRolesAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> UpdateRoleAsync(
        string roleId,
        Role role,
        CancellationToken ct = default);

    Task<bool> DeleteRoleAsync(
        string roleId,
        CancellationToken ct = default);

    // Role assignments
    Task<RoleAssignment> AssignRoleAsync(
        string tenantId,
        string userId,
        string roleId,
        DateTime? expiresAt = null,
        CancellationToken ct = default);

    Task<List<RoleAssignment>> GetUserRolesAsync(
        string userId,
        CancellationToken ct = default);

    Task<List<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken ct = default);

    Task<bool> RevokeRoleAsync(
        string assignmentId,
        CancellationToken ct = default);

    // Permissions
    Task<Permission> CreatePermissionAsync(
        Permission permission,
        CancellationToken ct = default);

    Task<List<Permission>> GetPermissionsAsync(
        string? resourceType = null,
        CancellationToken ct = default);

    // Resource ACL
    Task<ResourceACL> CreateResourceACLAsync(
        string tenantId,
        ResourceACL acl,
        CancellationToken ct = default);

    Task<bool> GrantAccessAsync(
        string aclId,
        string userId,
        string permission,
        CancellationToken ct = default);

    Task<bool> RevokeAccessAsync(
        string aclId,
        string userId,
        string permission,
        CancellationToken ct = default);

    Task<List<string>> GetUserResourceAccessAsync(
        string userId,
        string resourceType,
        string resourceId,
        CancellationToken ct = default);

    // Access requests
    Task<AccessRequest> RequestAccessAsync(
        string tenantId,
        string userId,
        string resourceType,
        string resourceId,
        string action,
        string reason,
        CancellationToken ct = default);

    Task<bool> ApproveAccessRequestAsync(
        string requestId,
        string approverId,
        CancellationToken ct = default);

    Task<bool> DenyAccessRequestAsync(
        string requestId,
        string approverId,
        CancellationToken ct = default);

    // Access control checks
    Task<bool> CanAccessAsync(
        string userId,
        string resourceType,
        string resourceId,
        string action,
        CancellationToken ct = default);

    // ABAC
    Task<ABACPolicy> CreateABACPolicyAsync(
        string tenantId,
        ABACPolicy policy,
        CancellationToken ct = default);

    Task<List<ABACPolicy>> GetABACPoliciesAsync(
        string tenantId,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetAccessControlAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Access control engine implementation
/// </summary>
public class AccessControlEngine : IAccessControlEngine
{
    private readonly ILogger<AccessControlEngine> _logger;
    private readonly Dictionary<string, Role> _roles;
    private readonly Dictionary<string, RoleAssignment> _assignments;
    private readonly Dictionary<string, Permission> _permissions;
    private readonly Dictionary<string, ResourceACL> _acls;
    private readonly Dictionary<string, AccessRequest> _accessRequests;
    private readonly Dictionary<string, ABACPolicy> _abacPolicies;

    public AccessControlEngine(ILogger<AccessControlEngine> logger)
    {
        _logger = logger;
        _roles = new Dictionary<string, Role>();
        _assignments = new Dictionary<string, RoleAssignment>();
        _permissions = new Dictionary<string, Permission>();
        _acls = new Dictionary<string, ResourceACL>();
        _accessRequests = new Dictionary<string, AccessRequest>();
        _abacPolicies = new Dictionary<string, ABACPolicy>();
        InitializeDefaultRoles();
    }

    // Roles
    public async Task<Role> CreateRoleAsync(
        string tenantId,
        Role role,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        role.TenantId = tenantId;
        _roles[role.RoleId] = role;

        _logger.LogInformation(
            "Role created: RoleId={RoleId}, TenantId={TenantId}, Name={RoleName}",
            role.RoleId, tenantId, role.RoleName);

        return role;
    }

    public async Task<Role?> GetRoleAsync(
        string roleId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _roles.TryGetValue(roleId, out var role);
        return role;
    }

    public async Task<List<Role>> GetRolesAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _roles.Values
            .Where(r => r.TenantId == tenantId || r.IsBuiltIn)
            .ToList();
    }

    public async Task<bool> UpdateRoleAsync(
        string roleId,
        Role role,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_roles.TryGetValue(roleId, out _))
            return false;

        role.RoleId = roleId;
        _roles[roleId] = role;

        _logger.LogInformation(
            "Role updated: RoleId={RoleId}",
            roleId);

        return true;
    }

    public async Task<bool> DeleteRoleAsync(
        string roleId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_roles.Remove(roleId))
        {
            _logger.LogInformation(
                "Role deleted: RoleId={RoleId}",
                roleId);
            return true;
        }

        return false;
    }

    // Role assignments
    public async Task<RoleAssignment> AssignRoleAsync(
        string tenantId,
        string userId,
        string roleId,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var assignment = new RoleAssignment
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            ExpiresAt = expiresAt,
            AssignedBy = "system",
        };

        _assignments[assignment.AssignmentId] = assignment;

        _logger.LogInformation(
            "Role assigned: UserId={UserId}, RoleId={RoleId}, TenantId={TenantId}",
            userId, roleId, tenantId);

        return assignment;
    }

    public async Task<List<RoleAssignment>> GetUserRolesAsync(
        string userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _assignments.Values
            .Where(a => a.UserId == userId && (!a.ExpiresAt.HasValue || a.ExpiresAt > DateTime.UtcNow))
            .ToList();
    }

    public async Task<List<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var assignments = await GetUserRolesAsync(userId, ct);
        var permissions = new HashSet<string>();

        foreach (var assignment in assignments)
        {
            var role = await GetRoleAsync(assignment.RoleId, ct);
            if (role != null)
            {
                foreach (var perm in role.Permissions)
                {
                    permissions.Add(perm);
                }
            }
        }

        return permissions.ToList();
    }

    public async Task<bool> RevokeRoleAsync(
        string assignmentId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_assignments.Remove(assignmentId))
        {
            _logger.LogInformation(
                "Role revoked: AssignmentId={AssignmentId}",
                assignmentId);
            return true;
        }

        return false;
    }

    // Permissions
    public async Task<Permission> CreatePermissionAsync(
        Permission permission,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _permissions[permission.PermissionId] = permission;

        _logger.LogInformation(
            "Permission created: PermissionId={PermissionId}, Name={PermissionName}",
            permission.PermissionId, permission.PermissionName);

        return permission;
    }

    public async Task<List<Permission>> GetPermissionsAsync(
        string? resourceType = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _permissions.Values
            .Where(p => resourceType == null || p.ResourceType == resourceType)
            .ToList();

        return results;
    }

    // Resource ACL
    public async Task<ResourceACL> CreateResourceACLAsync(
        string tenantId,
        ResourceACL acl,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        acl.TenantId = tenantId;
        _acls[acl.ACLId] = acl;

        _logger.LogInformation(
            "Resource ACL created: ACLId={ACLId}, ResourceType={ResourceType}, ResourceId={ResourceId}",
            acl.ACLId, acl.ResourceType, acl.ResourceId);

        return acl;
    }

    public async Task<bool> GrantAccessAsync(
        string aclId,
        string userId,
        string permission,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_acls.TryGetValue(aclId, out var acl))
            return false;

        if (!acl.AccessMap.ContainsKey(userId))
        {
            acl.AccessMap[userId] = new List<string>();
        }

        if (!acl.AccessMap[userId].Contains(permission))
        {
            acl.AccessMap[userId].Add(permission);
        }

        _logger.LogInformation(
            "Access granted: ACLId={ACLId}, UserId={UserId}, Permission={Permission}",
            aclId, userId, permission);

        return true;
    }

    public async Task<bool> RevokeAccessAsync(
        string aclId,
        string userId,
        string permission,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_acls.TryGetValue(aclId, out var acl))
            return false;

        if (acl.AccessMap.TryGetValue(userId, out var permissions))
        {
            permissions.Remove(permission);
        }

        _logger.LogInformation(
            "Access revoked: ACLId={ACLId}, UserId={UserId}, Permission={Permission}",
            aclId, userId, permission);

        return true;
    }

    public async Task<List<string>> GetUserResourceAccessAsync(
        string userId,
        string resourceType,
        string resourceId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var acls = _acls.Values
            .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .ToList();

        var accessList = new HashSet<string>();

        foreach (var acl in acls)
        {
            if (acl.AccessMap.TryGetValue(userId, out var permissions))
            {
                foreach (var perm in permissions)
                {
                    accessList.Add(perm);
                }
            }
        }

        return accessList.ToList();
    }

    // Access requests
    public async Task<AccessRequest> RequestAccessAsync(
        string tenantId,
        string userId,
        string resourceType,
        string resourceId,
        string action,
        string reason,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var request = new AccessRequest
        {
            TenantId = tenantId,
            UserId = userId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            RequestedAction = action,
            Reason = reason,
        };

        _accessRequests[request.RequestId] = request;

        _logger.LogInformation(
            "Access request created: RequestId={RequestId}, UserId={UserId}, Resource={ResourceType}/{ResourceId}",
            request.RequestId, userId, resourceType, resourceId);

        return request;
    }

    public async Task<bool> ApproveAccessRequestAsync(
        string requestId,
        string approverId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_accessRequests.TryGetValue(requestId, out var request))
            return false;

        request.Status = "approved";
        request.ApprovedBy = approverId;
        request.ExpiresAt = DateTime.UtcNow.AddHours(request.DurationHours);

        _logger.LogInformation(
            "Access request approved: RequestId={RequestId}, ApprovedBy={ApprovedBy}",
            requestId, approverId);

        return true;
    }

    public async Task<bool> DenyAccessRequestAsync(
        string requestId,
        string approverId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_accessRequests.TryGetValue(requestId, out var request))
            return false;

        request.Status = "denied";

        _logger.LogWarning(
            "Access request denied: RequestId={RequestId}, DeniedBy={ApprovedBy}",
            requestId, approverId);

        return true;
    }

    // Access control checks
    public async Task<bool> CanAccessAsync(
        string userId,
        string resourceType,
        string resourceId,
        string action,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var userPermissions = await GetUserPermissionsAsync(userId, ct);
        var resourceAccess = await GetUserResourceAccessAsync(userId, resourceType, resourceId, ct);

        var requiredPermission = $"{resourceType}:{action}";
        return userPermissions.Contains(requiredPermission) || resourceAccess.Contains(action);
    }

    // ABAC
    public async Task<ABACPolicy> CreateABACPolicyAsync(
        string tenantId,
        ABACPolicy policy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        policy.TenantId = tenantId;
        _abacPolicies[policy.PolicyId] = policy;

        _logger.LogInformation(
            "ABAC policy created: PolicyId={PolicyId}, Name={PolicyName}",
            policy.PolicyId, policy.PolicyName);

        return policy;
    }

    public async Task<List<ABACPolicy>> GetABACPoliciesAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _abacPolicies.Values
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToList();
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetAccessControlAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var roles = await GetRolesAsync(tenantId, ct);
        var assignments = _assignments.Values.Where(a => a.TenantId == tenantId).ToList();
        var requests = _accessRequests.Values.Where(r => r.TenantId == tenantId).ToList();

        return new Dictionary<string, object>
        {
            ["total_roles"] = roles.Count,
            ["total_assignments"] = assignments.Count,
            ["active_assignments"] = assignments.Count(a => !a.ExpiresAt.HasValue || a.ExpiresAt > DateTime.UtcNow),
            ["total_access_requests"] = requests.Count,
            ["approved_requests"] = requests.Count(r => r.Status == "approved"),
            ["pending_requests"] = requests.Count(r => r.Status == "pending"),
            ["abac_policies"] = _abacPolicies.Values.Count(p => p.TenantId == tenantId),
        };
    }

    // Helpers
    private void InitializeDefaultRoles()
    {
        var adminRole = new Role
        {
            TenantId = "system",
            RoleName = "Admin",
            Description = "Full administrative access",
            IsBuiltIn = true,
            Priority = 1000,
            Permissions = new List<string>
            {
                "workflow:read", "workflow:write", "workflow:delete",
                "execution:read", "execution:write",
                "settings:read", "settings:write",
                "audit:read", "approve:all"
            }
        };

        var editorRole = new Role
        {
            TenantId = "system",
            RoleName = "Editor",
            Description = "Can create and modify workflows",
            IsBuiltIn = true,
            Priority = 500,
            Permissions = new List<string>
            {
                "workflow:read", "workflow:write",
                "execution:read", "execution:write"
            }
        };

        var viewerRole = new Role
        {
            TenantId = "system",
            RoleName = "Viewer",
            Description = "Read-only access",
            IsBuiltIn = true,
            Priority = 100,
            Permissions = new List<string>
            {
                "workflow:read", "execution:read", "audit:read"
            }
        };

        _roles[adminRole.RoleId] = adminRole;
        _roles[editorRole.RoleId] = editorRole;
        _roles[viewerRole.RoleId] = viewerRole;
    }
}
