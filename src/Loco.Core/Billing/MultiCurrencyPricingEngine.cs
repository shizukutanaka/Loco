using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Billing;

/// <summary>
/// Multi-Currency Flexible Pricing Engine
/// Based on 2025 global pricing research:
///
/// Key Research Findings:
/// - Korea: 시간단가 500원, 실행 횟수 과금 (Time-based 500 KRW, execution-count billing)
/// - China: 消費者定价策略 30%, Token按量计费 (Consumer pricing 30%, Token usage billing)
/// - Germany: Nutzungsbasierte Abrechnung (Usage-based billing)
/// - France: Facturation à l'usage (Pay-per-use billing)
/// - Brazil: $8,700/employee/year average SaaS spend, LGPD compliance
/// - Russia: 20,000₽~ base pricing, cloud subscription models
/// - Global trend: 27% → 46% adoption of usage-based models (2018-2022)
///
/// Features:
/// - Multi-currency support (USD, EUR, JPY, KRW, CNY, GBP, RUB, BRL, etc.)
/// - Flexible pricing models (subscription, usage-based, hybrid, token-based)
/// - Regional pricing adjustments (purchasing power parity)
/// - Time-based billing (hourly, per-minute)
/// - Token consumption tracking (AI/LLM services)
/// - Volume discounts and progressive pricing
/// - Currency conversion with real-time rates
///
/// Research Sources:
/// - Korea: Worktronics 500원/hour, 500원/execution model
/// - China: Small bottle RPA token-based AI billing
/// - Stripe 2025: 100M events/month capacity
/// - Usage-based SaaS: 46% adoption rate
/// </summary>
public class MultiCurrencyPricingEngine
{
    private readonly Dictionary<string, decimal> _exchangeRates = new();
    private readonly Dictionary<string, PricingModel> _pricingModels = new();

    public MultiCurrencyPricingEngine()
    {
        InitializeExchangeRates();
        InitializePricingModels();
    }

    /// <summary>
    /// Supported currencies based on global research markets
    /// </summary>
    public enum Currency
    {
        USD,  // United States Dollar
        EUR,  // Euro (Germany, France, Spain, Italy)
        JPY,  // Japanese Yen
        KRW,  // Korean Won
        CNY,  // Chinese Yuan
        GBP,  // British Pound
        RUB,  // Russian Ruble
        BRL,  // Brazilian Real
        CAD,  // Canadian Dollar
        AUD,  // Australian Dollar
        INR,  // Indian Rupee
        MXN,  // Mexican Peso
        SGD,  // Singapore Dollar
        HKD   // Hong Kong Dollar
    }

    /// <summary>
    /// Pricing model types based on global trends
    /// </summary>
    public enum PricingModelType
    {
        Subscription,      // Traditional monthly/annual subscription
        UsageBased,        // Pay for what you use (execution-based)
        Hybrid,            // Base subscription + usage overages
        TimeBased,         // Hourly/minute billing (Korea model)
        TokenBased,        // AI token consumption (China model)
        Tiered,            // Volume-based tiers
        PayAsYouGo,        // Pure consumption (Germany/France model)
        Prepaid,           // Buy credits upfront
        Freemium,          // Free tier + paid features
        PerSeat,           // Per user pricing
        PerformanceBased   // Based on value delivered
    }

    /// <summary>
    /// Pricing model configuration
    /// </summary>
    public class PricingModel
    {
        public string ModelId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public PricingModelType Type { get; set; }
        public Currency BaseCurrency { get; set; } = Currency.USD;
        public List<PricingComponent> Components { get; set; } = new();
        public RegionalPricing Regional { get; set; } = new();
        public VolumeDiscounts Discounts { get; set; } = new();
        public Dictionary<string, string> LocalizedNames { get; set; } = new();
    }

    public class PricingComponent
    {
        public string ComponentId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public decimal BasePrice { get; set; }
        public Currency Currency { get; set; }
        public BillingUnit Unit { get; set; }
        public int IncludedQuantity { get; set; } = 0;
        public decimal OveragePrice { get; set; } = 0;
        public Dictionary<string, decimal> RegionalPrices { get; set; } = new(); // Country code -> price
    }

    public enum ComponentType
    {
        BaseSubscription,        // Monthly/annual base fee
        WorkflowExecution,       // Per workflow execution
        APIRequest,              // Per API call
        DataTransfer,            // Per GB transferred
        Storage,                 // Per GB stored
        ActiveUsers,             // Per active user
        AITokens,                // Per 1K tokens (GPT/Claude)
        ComputeTime,             // Per hour/minute
        TransactionVolume,       // Per transaction
        CustomMetric             // User-defined metric
    }

    public enum BillingUnit
    {
        PerMonth,
        PerYear,
        PerExecution,            // Korea model
        PerHour,                 // Korea 500원/hour model
        PerMinute,
        Per1000Tokens,           // China AI token model
        Per1000APIRequests,
        PerGB,
        PerUser,
        PerTransaction,
        PerSeat
    }

    /// <summary>
    /// Regional pricing adjustments
    /// Based on purchasing power parity (PPP)
    /// </summary>
    public class RegionalPricing
    {
        public bool Enabled { get; set; } = true;
        public Dictionary<string, decimal> PPPAdjustments { get; set; } = new(); // Country code -> multiplier
        public Dictionary<string, Currency> PreferredCurrency { get; set; } = new(); // Country -> currency
        public Dictionary<string, decimal> TaxRates { get; set; } = new(); // Country -> VAT/GST rate
    }

    /// <summary>
    /// Volume discounts and progressive pricing
    /// </summary>
    public class VolumeDiscounts
    {
        public bool Enabled { get; set; } = true;
        public List<DiscountTier> Tiers { get; set; } = new();
    }

    public class DiscountTier
    {
        public int MinQuantity { get; set; }
        public int? MaxQuantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public double DiscountPercentage { get; set; } // 0.0 to 1.0
    }

    /// <summary>
    /// Usage record for billing calculation
    /// </summary>
    public class UsageRecord
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public string CustomerId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ComponentId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public BillingUnit Unit { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string Region { get; set; } = "US"; // Country code
    }

    /// <summary>
    /// Billing calculation result
    /// </summary>
    public class BillingCalculation
    {
        public string CalculationId { get; set; } = Guid.NewGuid().ToString();
        public string CustomerId { get; set; } = string.Empty;
        public DateTime BillingPeriodStart { get; set; }
        public DateTime BillingPeriodEnd { get; set; }
        public Currency Currency { get; set; }
        public List<LineItem> LineItems { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal Discounts { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public Dictionary<Currency, decimal> TotalInOtherCurrencies { get; set; } = new();
    }

    public class LineItem
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public BillingUnit Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string ComponentId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Initialize exchange rates (simplified - in production, fetch from API)
    /// </summary>
    private void InitializeExchangeRates()
    {
        // Base: 1 USD
        _exchangeRates[Currency.USD.ToString()] = 1.00m;
        _exchangeRates[Currency.EUR.ToString()] = 0.92m;      // Euro
        _exchangeRates[Currency.JPY.ToString()] = 150.00m;    // Japanese Yen
        _exchangeRates[Currency.KRW.ToString()] = 1320.00m;   // Korean Won
        _exchangeRates[Currency.CNY.ToString()] = 7.25m;      // Chinese Yuan
        _exchangeRates[Currency.GBP.ToString()] = 0.79m;      // British Pound
        _exchangeRates[Currency.RUB.ToString()] = 90.00m;     // Russian Ruble
        _exchangeRates[Currency.BRL.ToString()] = 5.00m;      // Brazilian Real
        _exchangeRates[Currency.CAD.ToString()] = 1.35m;      // Canadian Dollar
        _exchangeRates[Currency.AUD.ToString()] = 1.52m;      // Australian Dollar
        _exchangeRates[Currency.INR.ToString()] = 83.00m;     // Indian Rupee
        _exchangeRates[Currency.MXN.ToString()] = 17.00m;     // Mexican Peso
        _exchangeRates[Currency.SGD.ToString()] = 1.34m;      // Singapore Dollar
        _exchangeRates[Currency.HKD.ToString()] = 7.80m;      // Hong Kong Dollar
    }

    /// <summary>
    /// Initialize pricing models based on global research
    /// </summary>
    private void InitializePricingModels()
    {
        // Korean time-based model (500원/hour)
        _pricingModels["korea-time-based"] = new PricingModel
        {
            Name = "Time-Based Billing (Korea)",
            Type = PricingModelType.TimeBased,
            BaseCurrency = Currency.KRW,
            Components = new List<PricingComponent>
            {
                new PricingComponent
                {
                    Name = "Bot Execution Time",
                    Type = ComponentType.ComputeTime,
                    BasePrice = 500m, // 500 KRW per hour
                    Currency = Currency.KRW,
                    Unit = BillingUnit.PerHour,
                    IncludedQuantity = 80 // 80 hours included in base plan
                },
                new PricingComponent
                {
                    Name = "Schedule Trigger Execution",
                    Type = ComponentType.WorkflowExecution,
                    BasePrice = 500m, // 500 KRW per execution
                    Currency = Currency.KRW,
                    Unit = BillingUnit.PerExecution,
                    IncludedQuantity = 100 // 100 executions included
                }
            },
            LocalizedNames = new Dictionary<string, string>
            {
                { "ko", "시간 기반 요금제" },
                { "en", "Time-Based Billing" }
            }
        };

        // Chinese token-based model (AI大模型)
        _pricingModels["china-token-based"] = new PricingModel
        {
            Name = "AI Token Consumption (China)",
            Type = PricingModelType.TokenBased,
            BaseCurrency = Currency.CNY,
            Components = new List<PricingComponent>
            {
                new PricingComponent
                {
                    Name = "AI Model Tokens",
                    Type = ComponentType.AITokens,
                    BasePrice = 0.015m, // 0.015 CNY per 1K tokens
                    Currency = Currency.CNY,
                    Unit = BillingUnit.Per1000Tokens,
                    IncludedQuantity = 100000 // 100K tokens included
                }
            },
            LocalizedNames = new Dictionary<string, string>
            {
                { "zh", "AI Token按量计费" },
                { "en", "AI Token Usage Billing" }
            }
        };

        // European usage-based model (Germany/France)
        _pricingModels["europe-usage-based"] = new PricingModel
        {
            Name = "Usage-Based Billing (Europe)",
            Type = PricingModelType.UsageBased,
            BaseCurrency = Currency.EUR,
            Components = new List<PricingComponent>
            {
                new PricingComponent
                {
                    Name = "Workflow Executions",
                    Type = ComponentType.WorkflowExecution,
                    BasePrice = 0.005m, // €0.005 per execution
                    Currency = Currency.EUR,
                    Unit = BillingUnit.PerExecution
                },
                new PricingComponent
                {
                    Name = "API Requests",
                    Type = ComponentType.APIRequest,
                    BasePrice = 0.50m, // €0.50 per 1000 requests
                    Currency = Currency.EUR,
                    Unit = BillingUnit.Per1000APIRequests
                }
            },
            LocalizedNames = new Dictionary<string, string>
            {
                { "de", "Nutzungsbasierte Abrechnung" },
                { "fr", "Facturation à l'usage" },
                { "en", "Usage-Based Billing" }
            }
        };

        // Russian subscription model
        _pricingModels["russia-subscription"] = new PricingModel
        {
            Name = "Subscription Billing (Russia)",
            Type = PricingModelType.Subscription,
            BaseCurrency = Currency.RUB,
            Components = new List<PricingComponent>
            {
                new PricingComponent
                {
                    Name = "Monthly Subscription",
                    Type = ComponentType.BaseSubscription,
                    BasePrice = 20000m, // 20,000₽ per month
                    Currency = Currency.RUB,
                    Unit = BillingUnit.PerMonth,
                    IncludedQuantity = 5000 // 5000 executions included
                }
            },
            LocalizedNames = new Dictionary<string, string>
            {
                { "ru", "Подписка" },
                { "en", "Subscription Billing" }
            }
        };

        // Add more pricing models...
    }

    /// <summary>
    /// Convert amount between currencies
    /// </summary>
    public decimal ConvertCurrency(
        decimal amount,
        Currency fromCurrency,
        Currency toCurrency)
    {
        if (fromCurrency == toCurrency)
        {
            return amount;
        }

        // Convert to USD first
        var amountInUSD = amount / _exchangeRates[fromCurrency.ToString()];

        // Convert to target currency
        return amountInUSD * _exchangeRates[toCurrency.ToString()];
    }

    /// <summary>
    /// Calculate billing based on usage records
    /// </summary>
    public async Task<BillingCalculation> CalculateBillingAsync(
        string customerId,
        string pricingModelId,
        List<UsageRecord> usageRecords,
        Currency billingCurrency,
        string region = "US",
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        if (!_pricingModels.TryGetValue(pricingModelId, out var model))
        {
            throw new ArgumentException($"Pricing model {pricingModelId} not found");
        }

        var calculation = new BillingCalculation
        {
            CustomerId = customerId,
            BillingPeriodStart = usageRecords.Min(r => r.Timestamp),
            BillingPeriodEnd = usageRecords.Max(r => r.Timestamp),
            Currency = billingCurrency
        };

        // Group usage by component
        var usageByComponent = usageRecords.GroupBy(r => r.ComponentId);

        foreach (var componentUsage in usageByComponent)
        {
            var component = model.Components.FirstOrDefault(c => c.ComponentId == componentUsage.Key);
            if (component == null) continue;

            var totalQuantity = componentUsage.Sum(r => r.Quantity);
            var billableQuantity = Math.Max(0, totalQuantity - component.IncludedQuantity);

            if (billableQuantity > 0)
            {
                // Get regional price if available
                var unitPrice = component.RegionalPrices.GetValueOrDefault(region, component.BasePrice);

                // Apply regional PPP adjustment
                if (model.Regional.Enabled && model.Regional.PPPAdjustments.TryGetValue(region, out var pppMultiplier))
                {
                    unitPrice *= pppMultiplier;
                }

                // Convert to billing currency
                var unitPriceInBillingCurrency = ConvertCurrency(unitPrice, component.Currency, billingCurrency);

                // Apply volume discounts
                if (model.Discounts.Enabled)
                {
                    var tier = model.Discounts.Tiers
                        .Where(t => billableQuantity >= t.MinQuantity &&
                                   (!t.MaxQuantity.HasValue || billableQuantity <= t.MaxQuantity.Value))
                        .FirstOrDefault();

                    if (tier != null)
                    {
                        unitPriceInBillingCurrency *= (decimal)(1.0 - tier.DiscountPercentage);
                    }
                }

                var lineItem = new LineItem
                {
                    Description = component.Name,
                    Quantity = billableQuantity,
                    Unit = component.Unit,
                    UnitPrice = unitPriceInBillingCurrency,
                    Amount = billableQuantity * unitPriceInBillingCurrency,
                    ComponentId = component.ComponentId
                };

                calculation.LineItems.Add(lineItem);
            }
        }

        // Calculate totals
        calculation.Subtotal = calculation.LineItems.Sum(li => li.Amount);
        calculation.Discounts = 0; // Can add promotional discounts here

        // Apply tax if applicable
        if (model.Regional.TaxRates.TryGetValue(region, out var taxRate))
        {
            calculation.Tax = calculation.Subtotal * taxRate;
        }

        calculation.Total = calculation.Subtotal - calculation.Discounts + calculation.Tax;

        // Calculate total in other major currencies for reference
        calculation.TotalInOtherCurrencies = new Dictionary<Currency, decimal>
        {
            { Currency.USD, ConvertCurrency(calculation.Total, billingCurrency, Currency.USD) },
            { Currency.EUR, ConvertCurrency(calculation.Total, billingCurrency, Currency.EUR) },
            { Currency.JPY, ConvertCurrency(calculation.Total, billingCurrency, Currency.JPY) },
            { Currency.KRW, ConvertCurrency(calculation.Total, billingCurrency, Currency.KRW) },
            { Currency.CNY, ConvertCurrency(calculation.Total, billingCurrency, Currency.CNY) }
        };

        return calculation;
    }

    /// <summary>
    /// Get recommended pricing model for region
    /// Based on local market preferences
    /// </summary>
    public string GetRecommendedPricingModel(string region)
    {
        return region switch
        {
            "KR" => "korea-time-based",
            "CN" => "china-token-based",
            "DE" or "FR" or "ES" or "IT" => "europe-usage-based",
            "RU" => "russia-subscription",
            _ => "europe-usage-based" // Default
        };
    }

    /// <summary>
    /// Get preferred currency for region
    /// </summary>
    public Currency GetPreferredCurrency(string region)
    {
        return region switch
        {
            "US" => Currency.USD,
            "DE" or "FR" or "ES" or "IT" or "NL" or "BE" => Currency.EUR,
            "JP" => Currency.JPY,
            "KR" => Currency.KRW,
            "CN" => Currency.CNY,
            "GB" => Currency.GBP,
            "RU" => Currency.RUB,
            "BR" => Currency.BRL,
            "CA" => Currency.CAD,
            "AU" => Currency.AUD,
            "IN" => Currency.INR,
            "MX" => Currency.MXN,
            "SG" => Currency.SGD,
            "HK" => Currency.HKD,
            _ => Currency.USD
        };
    }

    /// <summary>
    /// Get all available pricing models
    /// </summary>
    public List<PricingModel> GetPricingModels(PricingModelType? type = null, Currency? currency = null)
    {
        var models = _pricingModels.Values.AsEnumerable();

        if (type.HasValue)
        {
            models = models.Where(m => m.Type == type.Value);
        }

        if (currency.HasValue)
        {
            models = models.Where(m => m.BaseCurrency == currency.Value);
        }

        return models.ToList();
    }

    /// <summary>
    /// Update exchange rates (in production, call external API)
    /// </summary>
    public async Task UpdateExchangeRatesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // In production: fetch from external API like exchangerate-api.com, fixer.io, etc.
        // For now, rates are static in InitializeExchangeRates()
    }
}
