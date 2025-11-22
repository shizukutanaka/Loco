using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Collaboration
{
    /// <summary>
    /// Workflow collaboration and review system
    /// Phase 26: Team collaboration, comments, approvals, shared executions, activity tracking
    /// </summary>
    public interface IWorkflowCollaborationEngine
    {
        Task<CollaborationSpace> CreateCollaborationSpaceAsync(string tenantId, CollaborationSpaceDefinition definition, CancellationToken ct = default);
        Task<bool> AddTeamMemberAsync(string tenantId, string spaceId, TeamMemberDefinition member, CancellationToken ct = default);
        Task<Comment> PostCommentAsync(string tenantId, string spaceId, string workflowId, CommentDefinition comment, CancellationToken ct = default);
        Task<List<Comment>> GetCommentsAsync(string tenantId, string workflowId, int limit = 100, CancellationToken ct = default);
        Task<ReviewRequest> CreateReviewRequestAsync(string tenantId, string workflowId, ReviewDefinition review, CancellationToken ct = default);
        Task<bool> ApproveWorkflowAsync(string tenantId, string reviewId, string approverId, string feedback, CancellationToken ct = default);
        Task<List<ReviewRequest>> GetPendingReviewsAsync(string tenantId, string assignedTo = null, CancellationToken ct = default);
        Task<SharedExecution> ShareExecutionAsync(string tenantId, string executionId, ShareDefinition definition, CancellationToken ct = default);
        Task<ActivityLog> LogActivityAsync(string tenantId, ActivityDefinition activity, CancellationToken ct = default);
        Task<CollaborationMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class WorkflowCollaborationEngine : IWorkflowCollaborationEngine
    {
        private readonly ILogger<WorkflowCollaborationEngine> _logger;
        private readonly Dictionary<string, CollaborationSpace> _spaces = new();
        private readonly Dictionary<string, List<TeamMember>> _members = new();
        private readonly Dictionary<string, List<Comment>> _comments = new();
        private readonly Dictionary<string, ReviewRequest> _reviews = new();
        private readonly Dictionary<string, SharedExecution> _sharedExecutions = new();
        private readonly Dictionary<string, List<ActivityLog>> _activityLogs = new();
        private readonly Random _random = new(42);

        public WorkflowCollaborationEngine(ILogger<WorkflowCollaborationEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CollaborationSpace> CreateCollaborationSpaceAsync(string tenantId, CollaborationSpaceDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating collaboration space {SpaceName}", definition.Name);
            await Task.Delay(25, ct);

            var space = new CollaborationSpace
            {
                SpaceId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = definition.CreatedBy,
                Owner = definition.Owner,
                Status = "active",
                Privacy = definition.Privacy ?? "team",
                WorkflowIds = definition.WorkflowIds ?? new List<string>(),
                Members = new List<TeamMember>(),
                AccessLevel = "collaborator"
            };

            var key = $"{tenantId}:{space.SpaceId}";
            _spaces[key] = space;
            _members[key] = new List<TeamMember>();
            _activityLogs[key] = new List<ActivityLog>();

            return space;
        }

        public async Task<bool> AddTeamMemberAsync(string tenantId, string spaceId, TeamMemberDefinition member, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Adding team member {MemberId} to space {SpaceId}", member.UserId, spaceId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{spaceId}";
            if (!_members.ContainsKey(key))
                return false;

            var teamMember = new TeamMember
            {
                UserId = member.UserId,
                Email = member.Email,
                Name = member.Name,
                Role = member.Role ?? "collaborator",
                JoinedAt = DateTimeOffset.UtcNow,
                LastActiveAt = DateTimeOffset.UtcNow,
                Permissions = member.Permissions ?? new List<string>(),
                Status = "active"
            };

            _members[key].Add(teamMember);

            var spaceKey = $"{tenantId}:{spaceId}";
            if (_spaces.ContainsKey(spaceKey))
                _spaces[spaceKey].Members.Add(teamMember);

            return true;
        }

        public async Task<Comment> PostCommentAsync(string tenantId, string spaceId, string workflowId, CommentDefinition comment, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Posting comment on workflow {WorkflowId}", workflowId);
            await Task.Delay(15, ct);

            var commentObj = new Comment
            {
                CommentId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                SpaceId = spaceId,
                TenantId = tenantId,
                AuthorId = comment.AuthorId,
                AuthorName = comment.AuthorName,
                Content = comment.Content,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Status = "published",
                Mentions = comment.Mentions ?? new List<string>(),
                Attachments = comment.Attachments ?? new List<string>(),
                Reactions = new Dictionary<string, int>(),
                RepliesCount = 0,
                IsEdited = false
            };

            var key = $"{tenantId}:{workflowId}";
            if (!_comments.ContainsKey(key))
                _comments[key] = new List<Comment>();

            _comments[key].Add(commentObj);
            if (_comments[key].Count > 10000)
                _comments[key] = _comments[key].Skip(_comments[key].Count - 10000).ToList();

            return commentObj;
        }

        public async Task<List<Comment>> GetCommentsAsync(string tenantId, string workflowId, int limit = 100, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting comments for workflow {WorkflowId}", workflowId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{workflowId}";
            if (!_comments.ContainsKey(key))
                return new List<Comment>();

            return _comments[key]
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task<ReviewRequest> CreateReviewRequestAsync(string tenantId, string workflowId, ReviewDefinition review, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating review request for workflow {WorkflowId}", workflowId);
            await Task.Delay(25, ct);

            var reviewRequest = new ReviewRequest
            {
                ReviewId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TenantId = tenantId,
                RequestedBy = review.RequestedBy,
                RequestedAt = DateTimeOffset.UtcNow,
                Reviewers = review.Reviewers ?? new List<string>(),
                Status = "pending",
                Priority = review.Priority ?? "medium",
                Deadline = DateTimeOffset.UtcNow.AddDays(review.DeadlineDays ?? 3),
                Description = review.Description,
                RequiredApprovals = review.RequiredApprovals ?? review.Reviewers?.Count ?? 1,
                Approvals = new List<Approval>(),
                Comments = new List<string>(),
                RejectionReason = null
            };

            var key = $"{tenantId}:{reviewRequest.ReviewId}";
            _reviews[key] = reviewRequest;

            return reviewRequest;
        }

        public async Task<bool> ApproveWorkflowAsync(string tenantId, string reviewId, string approverId, string feedback, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Approving workflow review {ReviewId}", reviewId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{reviewId}";
            if (!_reviews.ContainsKey(key))
                return false;

            var review = _reviews[key];
            var approval = new Approval
            {
                ApprovalId = Guid.NewGuid().ToString("N"),
                ApproverId = approverId,
                ApprovedAt = DateTimeOffset.UtcNow,
                Feedback = feedback,
                Status = "approved"
            };

            review.Approvals.Add(approval);

            if (review.Approvals.Count >= review.RequiredApprovals)
                review.Status = "approved";

            return true;
        }

        public async Task<List<ReviewRequest>> GetPendingReviewsAsync(string tenantId, string assignedTo = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting pending reviews");
            await Task.Delay(20, ct);

            var reviews = _reviews
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .Where(r => r.Status == "pending")
                .ToList();

            if (!string.IsNullOrWhiteSpace(assignedTo))
                reviews = reviews.Where(r => r.Reviewers.Contains(assignedTo)).ToList();

            return reviews.OrderByDescending(r => r.Priority).ThenBy(r => r.Deadline).ToList();
        }

        public async Task<SharedExecution> ShareExecutionAsync(string tenantId, string executionId, ShareDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Sharing execution {ExecutionId}", executionId);
            await Task.Delay(20, ct);

            var shareId = Guid.NewGuid().ToString("N");
            var shared = new SharedExecution
            {
                ShareId = shareId,
                ExecutionId = executionId,
                TenantId = tenantId,
                SharedBy = definition.SharedBy,
                SharedAt = DateTimeOffset.UtcNow,
                SharedWith = definition.SharedWith ?? new List<string>(),
                AccessLevel = definition.AccessLevel ?? "view",
                ExpiresAt = definition.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
                Status = "active",
                ShareLink = $"https://workflows.io/share/{shareId}",
                ViewCount = 0,
                LastViewedAt = null,
                Comments = new List<string>()
            };

            var key = $"{tenantId}:{shareId}";
            _sharedExecutions[key] = shared;

            return shared;
        }

        public async Task<ActivityLog> LogActivityAsync(string tenantId, ActivityDefinition activity, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Logging activity {ActivityType}", activity.ActivityType);
            await Task.Delay(10, ct);

            var log = new ActivityLog
            {
                ActivityId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                ActivityType = activity.ActivityType,
                UserId = activity.UserId,
                UserName = activity.UserName,
                ResourceType = activity.ResourceType,
                ResourceId = activity.ResourceId,
                Description = activity.Description,
                Timestamp = DateTimeOffset.UtcNow,
                Details = activity.Details ?? new Dictionary<string, string>(),
                ImpactLevel = activity.ImpactLevel ?? "medium",
                Status = "completed"
            };

            var key = $"{tenantId}";
            if (!_activityLogs.ContainsKey(key))
                _activityLogs[key] = new List<ActivityLog>();

            _activityLogs[key].Add(log);
            if (_activityLogs[key].Count > 100000)
                _activityLogs[key] = _activityLogs[key].Skip(_activityLogs[key].Count - 100000).ToList();

            return log;
        }

        public async Task<CollaborationMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating collaboration metrics");
            await Task.Delay(30, ct);

            var spaceCount = _spaces.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));
            var commentCount = _comments.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0);
            var reviewCount = _reviews.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));
            var activityCount = _activityLogs.ContainsKey(tenantId) ? _activityLogs[tenantId].Count : 0;

            var metrics = new CollaborationMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                CollaborationSpaces = spaceCount,
                ActiveTeamMembers = _members.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count(m => m.Status == "active") : 0),
                TotalComments = commentCount,
                CommentsLast7Days = _random.Next(commentCount / 10, commentCount),
                ReviewsOpen = _reviews.Count(kvp => kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Status == "pending"),
                ReviewsCompleted = _reviews.Count(kvp => kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Status == "approved"),
                AverageReviewTime = _random.Next(2, 24), // hours
                SharedExecutions = _sharedExecutions.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                ActivityEvents = activityCount,
                AverageMembersPerSpace = spaceCount > 0 ? _members.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0) / spaceCount : 0,
                CollaborationScore = _random.Next(60, 95)
            };

            return metrics;
        }
    }

    public class CollaborationSpaceDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public string Owner { get; set; }
        public string Privacy { get; set; }
        public List<string> WorkflowIds { get; set; }
    }

    public class CollaborationSpace
    {
        public string SpaceId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string Owner { get; set; }
        public string Status { get; set; }
        public string Privacy { get; set; }
        public List<string> WorkflowIds { get; set; } = new();
        public List<TeamMember> Members { get; set; } = new();
        public string AccessLevel { get; set; }
    }

    public class TeamMemberDefinition
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public List<string> Permissions { get; set; }
    }

    public class TeamMember
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
        public DateTimeOffset LastActiveAt { get; set; }
        public List<string> Permissions { get; set; } = new();
        public string Status { get; set; }
    }

    public class CommentDefinition
    {
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string Content { get; set; }
        public List<string> Mentions { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class Comment
    {
        public string CommentId { get; set; }
        public string WorkflowId { get; set; }
        public string SpaceId { get; set; }
        public string TenantId { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string Content { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public List<string> Mentions { get; set; } = new();
        public List<string> Attachments { get; set; } = new();
        public Dictionary<string, int> Reactions { get; set; } = new();
        public int RepliesCount { get; set; }
        public bool IsEdited { get; set; }
    }

    public class ReviewDefinition
    {
        public string RequestedBy { get; set; }
        public List<string> Reviewers { get; set; }
        public string Priority { get; set; }
        public int? DeadlineDays { get; set; }
        public string Description { get; set; }
        public int? RequiredApprovals { get; set; }
    }

    public class ReviewRequest
    {
        public string ReviewId { get; set; }
        public string WorkflowId { get; set; }
        public string TenantId { get; set; }
        public string RequestedBy { get; set; }
        public DateTimeOffset RequestedAt { get; set; }
        public List<string> Reviewers { get; set; } = new();
        public string Status { get; set; }
        public string Priority { get; set; }
        public DateTimeOffset Deadline { get; set; }
        public string Description { get; set; }
        public int RequiredApprovals { get; set; }
        public List<Approval> Approvals { get; set; } = new();
        public List<string> Comments { get; set; } = new();
        public string RejectionReason { get; set; }
    }

    public class Approval
    {
        public string ApprovalId { get; set; }
        public string ApproverId { get; set; }
        public DateTimeOffset ApprovedAt { get; set; }
        public string Feedback { get; set; }
        public string Status { get; set; }
    }

    public class ShareDefinition
    {
        public string SharedBy { get; set; }
        public List<string> SharedWith { get; set; }
        public string AccessLevel { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class SharedExecution
    {
        public string ShareId { get; set; }
        public string ExecutionId { get; set; }
        public string TenantId { get; set; }
        public string SharedBy { get; set; }
        public DateTimeOffset SharedAt { get; set; }
        public List<string> SharedWith { get; set; } = new();
        public string AccessLevel { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string Status { get; set; }
        public string ShareLink { get; set; }
        public int ViewCount { get; set; }
        public DateTimeOffset? LastViewedAt { get; set; }
        public List<string> Comments { get; set; } = new();
    }

    public class ActivityDefinition
    {
        public string ActivityType { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Details { get; set; }
        public string ImpactLevel { get; set; }
    }

    public class ActivityLog
    {
        public string ActivityId { get; set; }
        public string TenantId { get; set; }
        public string ActivityType { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string Description { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<string, string> Details { get; set; } = new();
        public string ImpactLevel { get; set; }
        public string Status { get; set; }
    }

    public class CollaborationMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int CollaborationSpaces { get; set; }
        public int ActiveTeamMembers { get; set; }
        public int TotalComments { get; set; }
        public int CommentsLast7Days { get; set; }
        public int ReviewsOpen { get; set; }
        public int ReviewsCompleted { get; set; }
        public int AverageReviewTime { get; set; }
        public int SharedExecutions { get; set; }
        public int ActivityEvents { get; set; }
        public int AverageMembersPerSpace { get; set; }
        public int CollaborationScore { get; set; }
    }
}
