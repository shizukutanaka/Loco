using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Authorization
{
    public interface IAuthorizationPolicyService
    {
        Task<bool> AuthorizeAsync(ClaimsPrincipal user, string resource, string action);
        Task<bool> AuthorizeWithPolicyAsync(ClaimsPrincipal user, string policyName);
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission);
        Task<bool> HasRoleAsync(ClaimsPrincipal user, string role);
        Task<bool> HasAnyRoleAsync(ClaimsPrincipal user, params string[] roles);
        Task<bool> HasAllRolesAsync(ClaimsPrincipal user, params string[] roles);
        void RegisterPolicy(string name, IAuthorizationPolicy policy);
        void RegisterRequirement(string name, IAuthorizationRequirement requirement);
    }

    public class AuthorizationPolicyService : IAuthorizationPolicyService
    {
        private readonly ILogger<AuthorizationPolicyService> _logger;
        private readonly Dictionary<string, IAuthorizationPolicy> _policies;
        private readonly Dictionary<string, IAuthorizationRequirement> _requirements;
        private readonly IPermissionService _permissionService;
        private readonly IRoleService _roleService;

        public AuthorizationPolicyService(
            ILogger<AuthorizationPolicyService> logger,
            IPermissionService permissionService,
            IRoleService roleService)
        {
            _logger = logger;
            _permissionService = permissionService;
            _roleService = roleService;
            _policies = new Dictionary<string, IAuthorizationPolicy>(StringComparer.OrdinalIgnoreCase);
            _requirements = new Dictionary<string, IAuthorizationRequirement>(StringComparer.OrdinalIgnoreCase);
            InitializeDefaultPolicies();
        }

        private void InitializeDefaultPolicies()
        {
            RegisterPolicy("AdminOnly", new RoleBasedPolicy("Admin"));
            RegisterPolicy("ModeratorOrAdmin", new RoleBasedPolicy("Admin", "Moderator"));
            RegisterPolicy("AuthenticatedUser", new AuthenticatedUserPolicy());
            RegisterPolicy("EmailVerified", new ClaimRequirementPolicy("email_verified", "true"));
            RegisterPolicy("PremiumUser", new RoleBasedPolicy("Premium"));
            RegisterPolicy("CanRead", new PermissionBasedPolicy("read"));
            RegisterPolicy("CanWrite", new PermissionBasedPolicy("write"));
            RegisterPolicy("CanDelete", new PermissionBasedPolicy("delete"));
            RegisterPolicy("CanManageUsers", new PermissionBasedPolicy("users:manage"));
            RegisterPolicy("CanViewReports", new PermissionBasedPolicy("reports:view"));
            
            RegisterRequirement("MinimumAge", new MinimumAgeRequirement(18));
            RegisterRequirement("EmailDomain", new EmailDomainRequirement("company.com"));
            RegisterRequirement("TimeWindow", new TimeWindowRequirement(TimeSpan.FromHours(9), TimeSpan.FromHours(17)));
            RegisterRequirement("IpWhitelist", new IpWhitelistRequirement());
            RegisterRequirement("MfaEnabled", new MfaEnabledRequirement());
        }

        public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string resource, string action)
        {
            try
            {
                if (user == null || !user.Identity.IsAuthenticated)
                {
                    _logger.LogWarning("Authorization failed: User not authenticated");
                    return false;
                }

                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Authorization failed: User ID not found in claims");
                    return false;
                }

                if (await HasRoleAsync(user, "Admin"))
                {
                    _logger.LogInformation("Authorization granted: Admin role for user {UserId}", userId);
                    return true;
                }

                var requiredPermission = $"{resource}:{action}".ToLower();
                var hasPermission = await HasPermissionAsync(user, requiredPermission);

                if (hasPermission)
                {
                    _logger.LogInformation("Authorization granted: Permission {Permission} for user {UserId}", 
                        requiredPermission, userId);
                }
                else
                {
                    _logger.LogWarning("Authorization denied: Missing permission {Permission} for user {UserId}", 
                        requiredPermission, userId);
                }

                return hasPermission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization for resource: {Resource}, action: {Action}", 
                    resource, action);
                return false;
            }
        }

        public async Task<bool> AuthorizeWithPolicyAsync(ClaimsPrincipal user, string policyName)
        {
            try
            {
                if (!_policies.TryGetValue(policyName, out var policy))
                {
                    _logger.LogWarning("Policy not found: {PolicyName}", policyName);
                    return false;
                }

                var context = new AuthorizationContext
                {
                    User = user,
                    Resource = null,
                    Requirements = policy.Requirements
                };

                var result = await policy.EvaluateAsync(context);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Policy authorization granted: {PolicyName} for user {UserId}", 
                        policyName, user?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                }
                else
                {
                    _logger.LogWarning("Policy authorization denied: {PolicyName}. Reasons: {Reasons}", 
                        policyName, string.Join(", ", result.FailureReasons));
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating policy: {PolicyName}", policyName);
                return false;
            }
        }

        public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission)
        {
            if (user == null || !user.Identity.IsAuthenticated)
                return false;

            var userPermissions = user.FindAll("permission").Select(c => c.Value);
            if (userPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                return true;

            var userRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
            foreach (var role in userRoles)
            {
                var rolePermissions = await _roleService.GetRolePermissionsAsync(role);
                if (rolePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var dynamicPermissions = await _permissionService.GetUserPermissionsAsync(Guid.Parse(userId));
                if (dynamicPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public async Task<bool> HasRoleAsync(ClaimsPrincipal user, string role)
        {
            if (user == null || !user.Identity.IsAuthenticated)
                return false;

            var hasRole = user.IsInRole(role);
            
            if (!hasRole)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var dynamicRoles = await _roleService.GetUserRolesAsync(Guid.Parse(userId));
                    hasRole = dynamicRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
                }
            }

            return hasRole;
        }

        public async Task<bool> HasAnyRoleAsync(ClaimsPrincipal user, params string[] roles)
        {
            foreach (var role in roles)
            {
                if (await HasRoleAsync(user, role))
                    return true;
            }
            return false;
        }

        public async Task<bool> HasAllRolesAsync(ClaimsPrincipal user, params string[] roles)
        {
            foreach (var role in roles)
            {
                if (!await HasRoleAsync(user, role))
                    return false;
            }
            return true;
        }

        public void RegisterPolicy(string name, IAuthorizationPolicy policy)
        {
            _policies[name] = policy;
            _logger.LogInformation("Registered authorization policy: {PolicyName}", name);
        }

        public void RegisterRequirement(string name, IAuthorizationRequirement requirement)
        {
            _requirements[name] = requirement;
            _logger.LogInformation("Registered authorization requirement: {RequirementName}", name);
        }
    }

    public interface IAuthorizationPolicy
    {
        List<IAuthorizationRequirement> Requirements { get; }
        Task<AuthorizationResult> EvaluateAsync(AuthorizationContext context);
    }

    public interface IAuthorizationRequirement
    {
        Task<bool> IsSatisfiedAsync(AuthorizationContext context);
        string GetFailureReason();
    }

    public class AuthorizationContext
    {
        public ClaimsPrincipal User { get; set; }
        public object Resource { get; set; }
        public List<IAuthorizationRequirement> Requirements { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class AuthorizationResult
    {
        public bool Succeeded { get; set; }
        public List<string> FailureReasons { get; set; } = new List<string>();
    }

    public class RoleBasedPolicy : IAuthorizationPolicy
    {
        private readonly string[] _requiredRoles;

        public RoleBasedPolicy(params string[] requiredRoles)
        {
            _requiredRoles = requiredRoles;
            Requirements = new List<IAuthorizationRequirement> 
            { 
                new RoleRequirement(_requiredRoles) 
            };
        }

        public List<IAuthorizationRequirement> Requirements { get; }

        public async Task<AuthorizationResult> EvaluateAsync(AuthorizationContext context)
        {
            var result = new AuthorizationResult();
            
            foreach (var requirement in Requirements)
            {
                if (!await requirement.IsSatisfiedAsync(context))
                {
                    result.FailureReasons.Add(requirement.GetFailureReason());
                }
            }

            result.Succeeded = !result.FailureReasons.Any();
            return result;
        }
    }

    public class PermissionBasedPolicy : IAuthorizationPolicy
    {
        private readonly string _requiredPermission;

        public PermissionBasedPolicy(string requiredPermission)
        {
            _requiredPermission = requiredPermission;
            Requirements = new List<IAuthorizationRequirement> 
            { 
                new PermissionRequirement(_requiredPermission) 
            };
        }

        public List<IAuthorizationRequirement> Requirements { get; }

        public async Task<AuthorizationResult> EvaluateAsync(AuthorizationContext context)
        {
            var result = new AuthorizationResult();
            
            foreach (var requirement in Requirements)
            {
                if (!await requirement.IsSatisfiedAsync(context))
                {
                    result.FailureReasons.Add(requirement.GetFailureReason());
                }
            }

            result.Succeeded = !result.FailureReasons.Any();
            return result;
        }
    }

    public class AuthenticatedUserPolicy : IAuthorizationPolicy
    {
        public List<IAuthorizationRequirement> Requirements { get; } = new List<IAuthorizationRequirement> 
        { 
            new AuthenticatedRequirement() 
        };

        public async Task<AuthorizationResult> EvaluateAsync(AuthorizationContext context)
        {
            var result = new AuthorizationResult();
            
            if (context.User == null || !context.User.Identity.IsAuthenticated)
            {
                result.FailureReasons.Add("User is not authenticated");
            }
            else
            {
                result.Succeeded = true;
            }

            return await Task.FromResult(result);
        }
    }

    public class ClaimRequirementPolicy : IAuthorizationPolicy
    {
        private readonly string _claimType;
        private readonly string _claimValue;

        public ClaimRequirementPolicy(string claimType, string claimValue)
        {
            _claimType = claimType;
            _claimValue = claimValue;
            Requirements = new List<IAuthorizationRequirement> 
            { 
                new ClaimRequirement(_claimType, _claimValue) 
            };
        }

        public List<IAuthorizationRequirement> Requirements { get; }

        public async Task<AuthorizationResult> EvaluateAsync(AuthorizationContext context)
        {
            var result = new AuthorizationResult();
            
            var claim = context.User?.FindFirst(_claimType);
            if (claim == null || !claim.Value.Equals(_claimValue, StringComparison.OrdinalIgnoreCase))
            {
                result.FailureReasons.Add($"Required claim {_claimType}={_claimValue} not found");
            }
            else
            {
                result.Succeeded = true;
            }

            return await Task.FromResult(result);
        }
    }

    public class RoleRequirement : IAuthorizationRequirement
    {
        private readonly string[] _roles;

        public RoleRequirement(params string[] roles)
        {
            _roles = roles;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            return await Task.FromResult(_roles.Any(role => context.User.IsInRole(role)));
        }

        public string GetFailureReason()
        {
            return $"User must have one of the following roles: {string.Join(", ", _roles)}";
        }
    }

    public class PermissionRequirement : IAuthorizationRequirement
    {
        private readonly string _permission;

        public PermissionRequirement(string permission)
        {
            _permission = permission;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var permissions = context.User.FindAll("permission").Select(c => c.Value);
            return await Task.FromResult(permissions.Contains(_permission, StringComparer.OrdinalIgnoreCase));
        }

        public string GetFailureReason()
        {
            return $"User must have permission: {_permission}";
        }
    }

    public class AuthenticatedRequirement : IAuthorizationRequirement
    {
        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            return await Task.FromResult(context.User?.Identity?.IsAuthenticated ?? false);
        }

        public string GetFailureReason()
        {
            return "User must be authenticated";
        }
    }

    public class ClaimRequirement : IAuthorizationRequirement
    {
        private readonly string _claimType;
        private readonly string _claimValue;

        public ClaimRequirement(string claimType, string claimValue)
        {
            _claimType = claimType;
            _claimValue = claimValue;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var claim = context.User?.FindFirst(_claimType);
            return await Task.FromResult(claim != null && claim.Value.Equals(_claimValue, StringComparison.OrdinalIgnoreCase));
        }

        public string GetFailureReason()
        {
            return $"User must have claim {_claimType} with value {_claimValue}";
        }
    }

    public class MinimumAgeRequirement : IAuthorizationRequirement
    {
        private readonly int _minimumAge;

        public MinimumAgeRequirement(int minimumAge)
        {
            _minimumAge = minimumAge;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var birthDateClaim = context.User?.FindFirst("birthdate");
            if (birthDateClaim == null || !DateTime.TryParse(birthDateClaim.Value, out var birthDate))
                return false;

            var age = DateTime.Today.Year - birthDate.Year;
            if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;

            return await Task.FromResult(age >= _minimumAge);
        }

        public string GetFailureReason()
        {
            return $"User must be at least {_minimumAge} years old";
        }
    }

    public class EmailDomainRequirement : IAuthorizationRequirement
    {
        private readonly string _requiredDomain;

        public EmailDomainRequirement(string requiredDomain)
        {
            _requiredDomain = requiredDomain;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var emailClaim = context.User?.FindFirst(ClaimTypes.Email);
            if (emailClaim == null)
                return false;

            var domain = emailClaim.Value.Split('@').LastOrDefault();
            return await Task.FromResult(domain?.Equals(_requiredDomain, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public string GetFailureReason()
        {
            return $"User email must be from domain: {_requiredDomain}";
        }
    }

    public class TimeWindowRequirement : IAuthorizationRequirement
    {
        private readonly TimeSpan _startTime;
        private readonly TimeSpan _endTime;

        public TimeWindowRequirement(TimeSpan startTime, TimeSpan endTime)
        {
            _startTime = startTime;
            _endTime = endTime;
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var currentTime = DateTime.Now.TimeOfDay;
            return await Task.FromResult(currentTime >= _startTime && currentTime <= _endTime);
        }

        public string GetFailureReason()
        {
            return $"Access allowed only between {_startTime:hh\\:mm} and {_endTime:hh\\:mm}";
        }
    }

    public class IpWhitelistRequirement : IAuthorizationRequirement
    {
        private readonly HashSet<string> _allowedIps;

        public IpWhitelistRequirement()
        {
            _allowedIps = new HashSet<string>();
        }

        public void AddAllowedIp(string ip)
        {
            _allowedIps.Add(ip);
        }

        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            if (_allowedIps.Count == 0)
                return true;

            var ipClaim = context.User?.FindFirst("client_ip");
            if (ipClaim == null)
                return false;

            return await Task.FromResult(_allowedIps.Contains(ipClaim.Value));
        }

        public string GetFailureReason()
        {
            return "Access denied from this IP address";
        }
    }

    public class MfaEnabledRequirement : IAuthorizationRequirement
    {
        public async Task<bool> IsSatisfiedAsync(AuthorizationContext context)
        {
            var mfaClaim = context.User?.FindFirst("mfa_enabled");
            return await Task.FromResult(mfaClaim != null && mfaClaim.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        public string GetFailureReason()
        {
            return "Multi-factor authentication must be enabled";
        }
    }

    public interface IPermissionService
    {
        Task<List<string>> GetUserPermissionsAsync(Guid userId);
    }

    public interface IRoleService
    {
        Task<List<string>> GetUserRolesAsync(Guid userId);
        Task<List<string>> GetRolePermissionsAsync(string role);
    }
}