using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Billing;

/// <summary>
/// Comprehensive Stripe Billing Management System
/// Based on 2025 best practices for SaaS usage-based pricing
///
/// Features:
/// - Tiered subscription plans (Free, Pro, Enterprise)
/// - Usage-based metered billing (workflow executions, API calls)
/// - Hybrid pricing model (base + overages)
/// - Prepaid credits system
/// - Real-time usage tracking with Stripe Meters
/// - Automated invoicing
/// - Customer portal integration
///
/// Research Sources (2025):
/// - Stripe Usage-Based Billing documentation
/// - SaaS pricing model trends: 27% → 46% adoption (2018-2022)
/// - 100M usage events/month capacity
/// - Freemium inflection point: 35-50 employees
/// - Execution-based pricing replacing workflow-based
/// </summary>
public class StripeBillingManager
{
    private readonly string _stripeSecretKey;
    private readonly string _stripePublishableKey;

    public StripeBillingManager(string stripeSecretKey, string stripePublishableKey)
    {
        _stripeSecretKey = stripeSecretKey;
        _stripePublishableKey = stripePublishableKey;
    }

    /// <summary>
    /// Subscription pricing tiers
    /// Based on 2025 research: Tier-based with consumption hybrid model
    /// </summary>
    public enum PricingTier
    {
        Free,           // 0 USD/month, 100 executions/month
        Starter,        // 19 USD/month, 1,000 executions/month
        Pro,            // 79 USD/month, 10,000 executions/month
        Business,       // 299 USD/month, 50,000 executions/month
        Enterprise      // Custom pricing, unlimited executions
    }

    /// <summary>
    /// Subscription plan configuration
    /// </summary>
    public class SubscriptionPlan
    {
        public PricingTier Tier { get; set; }
        public decimal MonthlyFee { get; set; }
        public int IncludedExecutions { get; set; }
        public decimal OverageRatePer1000 { get; set; } // Cost per 1,000 extra executions
        public List<Feature> Features { get; set; } = new();
        public string StripePriceId { get; set; } = string.Empty;
        public string StripeProductId { get; set; } = string.Empty;
    }

    public class Feature
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Get predefined pricing plans
    /// Based on competitive analysis: IFTTT ($2.50), Power Automate ($15), FlowForma (€2,067)
    /// </summary>
    public static List<SubscriptionPlan> GetPricingPlans()
    {
        return new List<SubscriptionPlan>
        {
            new SubscriptionPlan
            {
                Tier = PricingTier.Free,
                MonthlyFee = 0,
                IncludedExecutions = 100,
                OverageRatePer1000 = 0, // No overages, hard limit
                Features = new List<Feature>
                {
                    new Feature { Name = "Basic Workflows", Description = "Up to 5 workflows" },
                    new Feature { Name = "Community Support", Description = "Forum access" },
                    new Feature { Name = "Single User", Description = "1 user account" },
                    new Feature { Name = "100 Executions/month", Description = "Workflow executions" }
                },
                StripePriceId = "price_free_tier",
                StripeProductId = "prod_free"
            },
            new SubscriptionPlan
            {
                Tier = PricingTier.Starter,
                MonthlyFee = 19,
                IncludedExecutions = 1000,
                OverageRatePer1000 = 5, // $5 per 1,000 extra executions
                Features = new List<Feature>
                {
                    new Feature { Name = "Unlimited Workflows", Description = "No workflow limits" },
                    new Feature { Name = "Email Support", Description = "24-hour response time" },
                    new Feature { Name = "Up to 3 Users", Description = "Team collaboration" },
                    new Feature { Name = "1,000 Executions/month", Description = "+ $5/1K overages" },
                    new Feature { Name = "Basic Integrations", Description = "100+ apps" }
                },
                StripePriceId = "price_starter_tier",
                StripeProductId = "prod_starter"
            },
            new SubscriptionPlan
            {
                Tier = PricingTier.Pro,
                MonthlyFee = 79,
                IncludedExecutions = 10000,
                OverageRatePer1000 = 3, // $3 per 1,000 extra executions (volume discount)
                Features = new List<Feature>
                {
                    new Feature { Name = "Unlimited Workflows", Description = "No limits" },
                    new Feature { Name = "Priority Support", Description = "4-hour response time" },
                    new Feature { Name = "Up to 10 Users", Description = "Team collaboration" },
                    new Feature { Name = "10,000 Executions/month", Description = "+ $3/1K overages" },
                    new Feature { Name = "Advanced Integrations", Description = "500+ apps" },
                    new Feature { Name = "API Access", Description = "REST API" },
                    new Feature { Name = "Custom Actions", Description = "Build custom integrations" }
                },
                StripePriceId = "price_pro_tier",
                StripeProductId = "prod_pro"
            },
            new SubscriptionPlan
            {
                Tier = PricingTier.Business,
                MonthlyFee = 299,
                IncludedExecutions = 50000,
                OverageRatePer1000 = 2, // $2 per 1,000 extra executions
                Features = new List<Feature>
                {
                    new Feature { Name = "Unlimited Workflows", Description = "No limits" },
                    new Feature { Name = "Premium Support", Description = "1-hour response time" },
                    new Feature { Name = "Unlimited Users", Description = "Entire organization" },
                    new Feature { Name = "50,000 Executions/month", Description = "+ $2/1K overages" },
                    new Feature { Name = "All Integrations", Description = "1000+ apps" },
                    new Feature { Name = "API Access", Description = "REST + GraphQL" },
                    new Feature { Name = "SSO & SAML", Description = "Enterprise authentication" },
                    new Feature { Name = "Advanced Security", Description = "Audit logs, compliance" },
                    new Feature { Name = "Dedicated Support", Description = "Slack channel" }
                },
                StripePriceId = "price_business_tier",
                StripeProductId = "prod_business"
            },
            new SubscriptionPlan
            {
                Tier = PricingTier.Enterprise,
                MonthlyFee = 0, // Custom pricing
                IncludedExecutions = int.MaxValue,
                OverageRatePer1000 = 0,
                Features = new List<Feature>
                {
                    new Feature { Name = "Everything in Business", Description = "All features" },
                    new Feature { Name = "Custom SLA", Description = "Guaranteed uptime" },
                    new Feature { Name = "Dedicated Infrastructure", Description = "Private cloud option" },
                    new Feature { Name = "Unlimited Executions", Description = "No limits" },
                    new Feature { Name = "White-label", Description = "Custom branding" },
                    new Feature { Name = "24/7 Phone Support", Description = "Direct engineer access" },
                    new Feature { Name = "Custom Development", Description = "Feature prioritization" },
                    new Feature { Name = "On-premise Deployment", Description = "Self-hosted option" }
                },
                StripePriceId = "price_enterprise_custom",
                StripeProductId = "prod_enterprise"
            }
        };
    }

    /// <summary>
    /// Customer subscription
    /// </summary>
    public class CustomerSubscription
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public PricingTier CurrentTier { get; set; }
        public SubscriptionStatus Status { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public int ExecutionsThisPeriod { get; set; }
        public int IncludedExecutions { get; set; }
        public decimal CurrentMonthlyFee { get; set; }
        public decimal EstimatedOverageCharges { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
    }

    public enum SubscriptionStatus
    {
        Trialing,
        Active,
        PastDue,
        Canceled,
        Incomplete,
        IncompleteExpired,
        Paused
    }

    /// <summary>
    /// Usage meter for tracking workflow executions
    /// Based on Stripe Meters (2025 feature)
    /// </summary>
    public class UsageMeter
    {
        public string MeterId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public MeterType Type { get; set; }
        public AggregationMethod Aggregation { get; set; }
        public Dictionary<string, string> Dimensions { get; set; } = new();
    }

    public enum MeterType
    {
        WorkflowExecutions,
        APIRequests,
        DataTransfer,
        StorageUsed,
        ActiveUsers,
        AITokens
    }

    public enum AggregationMethod
    {
        Sum,            // Sum of usage values during period
        MostRecent,     // Most recent usage value during period
        Maximum,        // Maximum usage value during period
        UniqueCount     // Count unique values
    }

    /// <summary>
    /// Usage event to be reported to Stripe
    /// </summary>
    public class UsageEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string CustomerId { get; set; } = string.Empty;
        public string MeterId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int Quantity { get; set; } = 1;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public UsageDimensions Dimensions { get; set; } = new();
    }

    public class UsageDimensions
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty; // production, staging, dev
        public string ExecutionType { get; set; } = string.Empty; // scheduled, manual, webhook
    }

    /// <summary>
    /// Create Stripe customer
    /// </summary>
    public async Task<string> CreateCustomerAsync(
        string email,
        string name,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate Stripe API call

        // In production: use Stripe.CustomerService
        var customerId = $"cus_{Guid.NewGuid().ToString("N").Substring(0, 14)}";

        return customerId;
    }

    /// <summary>
    /// Create subscription for customer
    /// </summary>
    public async Task<CustomerSubscription> CreateSubscriptionAsync(
        string customerId,
        PricingTier tier,
        bool trialPeriod = false,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate Stripe API call

        var plan = GetPricingPlans().First(p => p.Tier == tier);

        var subscription = new CustomerSubscription
        {
            SubscriptionId = $"sub_{Guid.NewGuid().ToString("N").Substring(0, 14)}",
            CustomerId = customerId,
            CurrentTier = tier,
            Status = trialPeriod ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            ExecutionsThisPeriod = 0,
            IncludedExecutions = plan.IncludedExecutions,
            CurrentMonthlyFee = plan.MonthlyFee,
            EstimatedOverageCharges = 0,
            CancelAtPeriodEnd = false
        };

        return subscription;
    }

    /// <summary>
    /// Report usage event to Stripe
    /// Based on Stripe Meters best practice (2025)
    /// </summary>
    public async Task<bool> ReportUsageAsync(
        UsageEvent usageEvent,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken); // Simulate Stripe API call

        // In production: use Stripe.BillingPortal.MeterEventService
        // await meterEventService.CreateAsync(new MeterEventCreateOptions { ... });

        return true;
    }

    /// <summary>
    /// Get current usage for customer this billing period
    /// </summary>
    public async Task<UsageSummary> GetUsageSummaryAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        // Simulated data - in production, query from Stripe
        return new UsageSummary
        {
            CustomerId = customerId,
            BillingPeriodStart = DateTime.UtcNow.AddDays(-15),
            BillingPeriodEnd = DateTime.UtcNow.AddDays(15),
            TotalExecutions = 7500,
            IncludedExecutions = 10000,
            RemainingExecutions = 2500,
            OverageExecutions = 0,
            EstimatedOverageCharges = 0,
            EstimatedTotalBill = 79.00m
        };
    }

    public class UsageSummary
    {
        public string CustomerId { get; set; } = string.Empty;
        public DateTime BillingPeriodStart { get; set; }
        public DateTime BillingPeriodEnd { get; set; }
        public int TotalExecutions { get; set; }
        public int IncludedExecutions { get; set; }
        public int RemainingExecutions { get; set; }
        public int OverageExecutions { get; set; }
        public decimal EstimatedOverageCharges { get; set; }
        public decimal EstimatedTotalBill { get; set; }
    }

    /// <summary>
    /// Calculate estimated bill based on current usage
    /// Real-time billing preview (Stripe 2025 feature)
    /// </summary>
    public async Task<BillingPreview> GetBillingPreviewAsync(
        string customerId,
        int currentExecutions,
        PricingTier tier,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        var plan = GetPricingPlans().First(p => p.Tier == tier);
        var baseFee = plan.MonthlyFee;
        var overageExecutions = Math.Max(0, currentExecutions - plan.IncludedExecutions);
        var overageCharges = (overageExecutions / 1000.0m) * plan.OverageRatePer1000;

        return new BillingPreview
        {
            CustomerId = customerId,
            Tier = tier,
            BaseFee = baseFee,
            IncludedExecutions = plan.IncludedExecutions,
            CurrentExecutions = currentExecutions,
            OverageExecutions = overageExecutions,
            OverageCharges = overageCharges,
            TotalEstimatedBill = baseFee + overageCharges,
            NextBillingDate = DateTime.UtcNow.AddDays(15) // Simulated
        };
    }

    public class BillingPreview
    {
        public string CustomerId { get; set; } = string.Empty;
        public PricingTier Tier { get; set; }
        public decimal BaseFee { get; set; }
        public int IncludedExecutions { get; set; }
        public int CurrentExecutions { get; set; }
        public int OverageExecutions { get; set; }
        public decimal OverageCharges { get; set; }
        public decimal TotalEstimatedBill { get; set; }
        public DateTime NextBillingDate { get; set; }
    }

    /// <summary>
    /// Upgrade/downgrade subscription
    /// Prorated billing handled by Stripe
    /// </summary>
    public async Task<CustomerSubscription> ChangeSubscriptionTierAsync(
        string subscriptionId,
        PricingTier newTier,
        bool prorate = true,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // In production: use Stripe.SubscriptionService.UpdateAsync
        var plan = GetPricingPlans().First(p => p.Tier == newTier);

        return new CustomerSubscription
        {
            SubscriptionId = subscriptionId,
            CurrentTier = newTier,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            IncludedExecutions = plan.IncludedExecutions,
            CurrentMonthlyFee = plan.MonthlyFee
        };
    }

    /// <summary>
    /// Cancel subscription
    /// </summary>
    public async Task<bool> CancelSubscriptionAsync(
        string subscriptionId,
        bool cancelAtPeriodEnd = true,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // In production: use Stripe.SubscriptionService.CancelAsync or UpdateAsync
        return true;
    }

    /// <summary>
    /// Create Stripe customer portal session
    /// Allows customers to manage their own billing
    /// </summary>
    public async Task<string> CreateCustomerPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // In production: use Stripe.BillingPortal.SessionService.CreateAsync
        return $"https://billing.stripe.com/session/{Guid.NewGuid()}";
    }

    /// <summary>
    /// Set up usage alert threshold
    /// Stripe 2025 feature: alerts when customer exceeds usage threshold
    /// </summary>
    public async Task<UsageAlert> SetUsageAlertAsync(
        string customerId,
        int thresholdExecutions,
        AlertType alertType = AlertType.Email,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        return new UsageAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            ThresholdExecutions = thresholdExecutions,
            AlertType = alertType,
            Triggered = false
        };
    }

    public class UsageAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public int ThresholdExecutions { get; set; }
        public AlertType AlertType { get; set; }
        public bool Triggered { get; set; }
        public DateTime? TriggeredAt { get; set; }
    }

    public enum AlertType
    {
        Email,
        Webhook,
        SMS,
        InApp
    }

    /// <summary>
    /// Prepaid credits system
    /// Based on Stripe 2025 recommendation for AI/API businesses
    /// </summary>
    public class PrepaidCredits
    {
        public string CustomerId { get; set; } = string.Empty;
        public decimal TotalCredits { get; set; }
        public decimal RemainingCredits { get; set; }
        public decimal CreditBalance => RemainingCredits;
        public DateTime PurchasedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<CreditTransaction> Transactions { get; set; } = new();
    }

    public class CreditTransaction
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum TransactionType
    {
        Purchase,
        Deduction,
        Refund,
        Adjustment
    }

    /// <summary>
    /// Purchase prepaid credits
    /// </summary>
    public async Task<PrepaidCredits> PurchaseCreditsAsync(
        string customerId,
        decimal creditAmount,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // In production: create Stripe PaymentIntent for one-time payment
        return new PrepaidCredits
        {
            CustomerId = customerId,
            TotalCredits = creditAmount,
            RemainingCredits = creditAmount,
            PurchasedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            Transactions = new List<CreditTransaction>
            {
                new CreditTransaction
                {
                    Type = TransactionType.Purchase,
                    Amount = creditAmount,
                    BalanceAfter = creditAmount,
                    Description = $"Purchased {creditAmount} credits"
                }
            }
        };
    }

    /// <summary>
    /// Market insights from 2025 research
    /// </summary>
    public static class BillingMarketInsights
    {
        public static readonly Dictionary<string, object> Data = new()
        {
            { "UsageBasedAdoption", "27% (2018) → 46% (2022)" },
            { "StripeCapacity", "100M usage events/month" },
            { "FreemiumInflectionPoint", "35-50 employees" },
            { "TrendShift", "Execution-based replacing workflow-based pricing" },
            { "KeyPrinciples", new[] {
                "Real-time usage visibility for customers",
                "Transparent automated invoicing",
                "Flexible hybrid models (base + consumption)",
                "Prepaid credits for AI/API services",
                "Granular dimension-based pricing",
                "Usage alerts at thresholds"
            }},
            { "CompetitorPricing", new Dictionary<string, string> {
                { "IFTTT", "$2.50/month" },
                { "Power Automate", "$15/user/month" },
                { "FlowForma Essential", "€2,067/month" },
                { "n8n", "Build without limits pricing model" },
                { "HighLevel", "Volume-based workflow tiers" }
            }}
        };
    }
}
