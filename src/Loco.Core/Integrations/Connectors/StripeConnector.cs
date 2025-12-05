// John Carmack: "Focus on making things work, then optimize"
// Rob Pike: "A little copying is better than a little dependency"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Stripe payments connector for charges, customers, subscriptions, and webhooks
/// Supports Stripe API version 2023-10-16
/// </summary>
public sealed class StripeConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private const string ApiVersion = "2023-10-16";

    public override string Id => "stripe";
    public override string Name => "Stripe";
    public override string Description => "Process payments, manage customers, subscriptions, and handle payment webhooks";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Payment;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 100
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials =
        [
            new() { Name = "secretKey", Label = "Secret Key", Type = ParameterType.Password, Required = true,
                Description = "Stripe secret key (sk_live_... or sk_test_...)" },
            new() { Name = "webhookSecret", Label = "Webhook Secret", Type = ParameterType.Password, Required = false,
                Description = "Webhook signing secret (whsec_...)" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "defaultCurrency", Label = "Default Currency", Type = ParameterType.String, DefaultValue = "usd" },
        new() { Name = "testMode", Label = "Test Mode", Type = ParameterType.Boolean, DefaultValue = false }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Charges
        new()
        {
            Id = "createCharge",
            Name = "Create Charge",
            Description = "Create a new charge (one-time payment)",
            Parameters =
            [
                new() { Name = "amount", Type = ParameterType.Number, Required = true, Description = "Amount in cents" },
                new() { Name = "currency", Type = ParameterType.String, DefaultValue = "usd" },
                new() { Name = "source", Type = ParameterType.String, Description = "Card token or source ID" },
                new() { Name = "customer", Type = ParameterType.String, Description = "Customer ID" },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "metadata", Type = ParameterType.Json }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "getCharge",
            Name = "Get Charge",
            Description = "Retrieve a charge by ID",
            Parameters = [new() { Name = "chargeId", Type = ParameterType.String, Required = true }]
        },
        new()
        {
            Id = "refundCharge",
            Name = "Refund Charge",
            Description = "Refund a charge fully or partially",
            Parameters =
            [
                new() { Name = "chargeId", Type = ParameterType.String, Required = true },
                new() { Name = "amount", Type = ParameterType.Number, Description = "Amount to refund in cents (full if not specified)" },
                new() { Name = "reason", Type = ParameterType.Select,
                    Options =
                    [
                        new() { Label = "Duplicate", Value = "duplicate" },
                        new() { Label = "Fraudulent", Value = "fraudulent" },
                        new() { Label = "Requested by customer", Value = "requested_by_customer" }
                    ]}
            ],
            RequiresConfirmation = true
        },
        // Payment Intents
        new()
        {
            Id = "createPaymentIntent",
            Name = "Create Payment Intent",
            Description = "Create a payment intent for secure payments",
            Parameters =
            [
                new() { Name = "amount", Type = ParameterType.Number, Required = true },
                new() { Name = "currency", Type = ParameterType.String, DefaultValue = "usd" },
                new() { Name = "customer", Type = ParameterType.String },
                new() { Name = "paymentMethodTypes", Type = ParameterType.Json, DefaultValue = "[\"card\"]" },
                new() { Name = "metadata", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "confirmPaymentIntent",
            Name = "Confirm Payment Intent",
            Description = "Confirm a payment intent",
            Parameters =
            [
                new() { Name = "paymentIntentId", Type = ParameterType.String, Required = true },
                new() { Name = "paymentMethod", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        // Customers
        new()
        {
            Id = "createCustomer",
            Name = "Create Customer",
            Description = "Create a new customer",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "metadata", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "getCustomer",
            Name = "Get Customer",
            Description = "Retrieve a customer by ID",
            Parameters = [new() { Name = "customerId", Type = ParameterType.String, Required = true }]
        },
        new()
        {
            Id = "updateCustomer",
            Name = "Update Customer",
            Description = "Update customer information",
            Parameters =
            [
                new() { Name = "customerId", Type = ParameterType.String, Required = true },
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "metadata", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "listCustomers",
            Name = "List Customers",
            Description = "List customers with optional filters",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 10 }
            ]
        },
        // Subscriptions
        new()
        {
            Id = "createSubscription",
            Name = "Create Subscription",
            Description = "Create a new subscription for a customer",
            Parameters =
            [
                new() { Name = "customer", Type = ParameterType.String, Required = true },
                new() { Name = "priceId", Type = ParameterType.String, Required = true },
                new() { Name = "quantity", Type = ParameterType.Number, DefaultValue = 1 },
                new() { Name = "trialPeriodDays", Type = ParameterType.Number },
                new() { Name = "metadata", Type = ParameterType.Json }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "cancelSubscription",
            Name = "Cancel Subscription",
            Description = "Cancel a subscription",
            Parameters =
            [
                new() { Name = "subscriptionId", Type = ParameterType.String, Required = true },
                new() { Name = "cancelAtPeriodEnd", Type = ParameterType.Boolean, DefaultValue = true }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "getSubscription",
            Name = "Get Subscription",
            Description = "Retrieve a subscription",
            Parameters = [new() { Name = "subscriptionId", Type = ParameterType.String, Required = true }]
        },
        // Invoices
        new()
        {
            Id = "createInvoice",
            Name = "Create Invoice",
            Description = "Create an invoice for a customer",
            Parameters =
            [
                new() { Name = "customer", Type = ParameterType.String, Required = true },
                new() { Name = "autoAdvance", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "collectionMethod", Type = ParameterType.Select, DefaultValue = "charge_automatically",
                    Options =
                    [
                        new() { Label = "Charge Automatically", Value = "charge_automatically" },
                        new() { Label = "Send Invoice", Value = "send_invoice" }
                    ]}
            ]
        },
        new()
        {
            Id = "payInvoice",
            Name = "Pay Invoice",
            Description = "Pay an invoice",
            Parameters = [new() { Name = "invoiceId", Type = ParameterType.String, Required = true }],
            RequiresConfirmation = true
        },
        // Products & Prices
        new()
        {
            Id = "createProduct",
            Name = "Create Product",
            Description = "Create a new product",
            Parameters =
            [
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "metadata", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "createPrice",
            Name = "Create Price",
            Description = "Create a price for a product",
            Parameters =
            [
                new() { Name = "product", Type = ParameterType.String, Required = true },
                new() { Name = "unitAmount", Type = ParameterType.Number, Required = true, Description = "Price in cents" },
                new() { Name = "currency", Type = ParameterType.String, DefaultValue = "usd" },
                new() { Name = "recurring", Type = ParameterType.Json, Description = "{interval: 'month', intervalCount: 1}" }
            ]
        },
        // Checkout
        new()
        {
            Id = "createCheckoutSession",
            Name = "Create Checkout Session",
            Description = "Create a Stripe Checkout session",
            Parameters =
            [
                new() { Name = "lineItems", Type = ParameterType.Json, Required = true,
                    Description = "[{price: 'price_xxx', quantity: 1}]" },
                new() { Name = "mode", Type = ParameterType.Select, Required = true,
                    Options =
                    [
                        new() { Label = "Payment", Value = "payment" },
                        new() { Label = "Subscription", Value = "subscription" },
                        new() { Label = "Setup", Value = "setup" }
                    ]},
                new() { Name = "successUrl", Type = ParameterType.String, Required = true },
                new() { Name = "cancelUrl", Type = ParameterType.String, Required = true },
                new() { Name = "customer", Type = ParameterType.String },
                new() { Name = "customerEmail", Type = ParameterType.String }
            ]
        },
        // Balance
        new()
        {
            Id = "getBalance",
            Name = "Get Balance",
            Description = "Retrieve account balance",
            Parameters = []
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onPaymentSucceeded",
            Name = "On Payment Succeeded",
            Description = "Triggered when a payment succeeds",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onPaymentFailed",
            Name = "On Payment Failed",
            Description = "Triggered when a payment fails",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onSubscriptionCreated",
            Name = "On Subscription Created",
            Description = "Triggered when a subscription is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onSubscriptionCanceled",
            Name = "On Subscription Canceled",
            Description = "Triggered when a subscription is canceled",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onInvoicePaid",
            Name = "On Invoice Paid",
            Description = "Triggered when an invoice is paid",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onCustomerCreated",
            Name = "On Customer Created",
            Description = "Triggered when a customer is created",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var secretKey = config.GetCredentialString("secretKey")!;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            client.DefaultRequestHeaders.Add("Stripe-Version", ApiVersion);

            var response = await client.GetAsync("https://api.stripe.com/v1/balance", ct);

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionTestResult.Fail($"Authentication failed: {response.StatusCode}");
            }

            var isTestKey = secretKey.StartsWith("sk_test_");
            return ConnectionTestResult.Ok($"Connected to Stripe ({(isTestKey ? "Test Mode" : "Live Mode")})");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var secretKey = config.GetCredentialString("secretKey")!;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.stripe.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        _httpClient.DefaultRequestHeaders.Add("Stripe-Version", ApiVersion);

        await base.InitializeAsync(config, ct);
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "createCharge" => await CreateChargeAsync(parameters, ct),
            "getCharge" => await GetAsync("charges", parameters.GetString("chargeId")!, ct),
            "refundCharge" => await RefundChargeAsync(parameters, ct),
            "createPaymentIntent" => await CreatePaymentIntentAsync(parameters, ct),
            "confirmPaymentIntent" => await ConfirmPaymentIntentAsync(parameters, ct),
            "createCustomer" => await CreateCustomerAsync(parameters, ct),
            "getCustomer" => await GetAsync("customers", parameters.GetString("customerId")!, ct),
            "updateCustomer" => await UpdateCustomerAsync(parameters, ct),
            "listCustomers" => await ListCustomersAsync(parameters, ct),
            "createSubscription" => await CreateSubscriptionAsync(parameters, ct),
            "cancelSubscription" => await CancelSubscriptionAsync(parameters, ct),
            "getSubscription" => await GetAsync("subscriptions", parameters.GetString("subscriptionId")!, ct),
            "createInvoice" => await CreateInvoiceAsync(parameters, ct),
            "payInvoice" => await PayInvoiceAsync(parameters, ct),
            "createProduct" => await CreateProductAsync(parameters, ct),
            "createPrice" => await CreatePriceAsync(parameters, ct),
            "createCheckoutSession" => await CreateCheckoutSessionAsync(parameters, ct),
            "getBalance" => await GetBalanceAsync(ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateChargeAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["amount"] = parameters.GetInt("amount").ToString(),
            ["currency"] = parameters.GetString("currency") ?? Configuration?.GetSettingString("defaultCurrency") ?? "usd"
        };

        AddIfNotNull(formData, "source", parameters.GetString("source"));
        AddIfNotNull(formData, "customer", parameters.GetString("customer"));
        AddIfNotNull(formData, "description", parameters.GetString("description"));
        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync("charges", formData, ct);
    }

    private async Task<ActionResult> RefundChargeAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["charge"] = parameters.GetString("chargeId")!
        };

        var amount = parameters.GetInt("amount", 0);
        if (amount > 0)
        {
            formData["amount"] = amount.ToString();
        }

        AddIfNotNull(formData, "reason", parameters.GetString("reason"));

        return await PostFormAsync("refunds", formData, ct);
    }

    private async Task<ActionResult> CreatePaymentIntentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["amount"] = parameters.GetInt("amount").ToString(),
            ["currency"] = parameters.GetString("currency") ?? "usd"
        };

        AddIfNotNull(formData, "customer", parameters.GetString("customer"));

        var paymentMethodTypes = parameters.Get<JsonElement?>("paymentMethodTypes");
        if (paymentMethodTypes.HasValue && paymentMethodTypes.Value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var type in paymentMethodTypes.Value.EnumerateArray())
            {
                formData[$"payment_method_types[{index++}]"] = type.GetString()!;
            }
        }
        else
        {
            formData["payment_method_types[0]"] = "card";
        }

        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync("payment_intents", formData, ct);
    }

    private async Task<ActionResult> ConfirmPaymentIntentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var paymentIntentId = parameters.GetString("paymentIntentId")!;
        var formData = new Dictionary<string, string>();

        AddIfNotNull(formData, "payment_method", parameters.GetString("paymentMethod"));

        return await PostFormAsync($"payment_intents/{paymentIntentId}/confirm", formData, ct);
    }

    private async Task<ActionResult> CreateCustomerAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["email"] = parameters.GetString("email")!
        };

        AddIfNotNull(formData, "name", parameters.GetString("name"));
        AddIfNotNull(formData, "phone", parameters.GetString("phone"));
        AddIfNotNull(formData, "description", parameters.GetString("description"));
        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync("customers", formData, ct);
    }

    private async Task<ActionResult> UpdateCustomerAsync(ActionParameters parameters, CancellationToken ct)
    {
        var customerId = parameters.GetString("customerId")!;
        var formData = new Dictionary<string, string>();

        AddIfNotNull(formData, "email", parameters.GetString("email"));
        AddIfNotNull(formData, "name", parameters.GetString("name"));
        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync($"customers/{customerId}", formData, ct);
    }

    private async Task<ActionResult> ListCustomersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = new List<string>();

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email))
        {
            query.Add($"email={Uri.EscapeDataString(email)}");
        }

        query.Add($"limit={parameters.GetInt("limit", 10)}");

        var queryString = string.Join("&", query);
        var response = await _httpClient!.GetAsync($"customers?{queryString}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to list customers: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> CreateSubscriptionAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["customer"] = parameters.GetString("customer")!,
            ["items[0][price]"] = parameters.GetString("priceId")!,
            ["items[0][quantity]"] = parameters.GetInt("quantity", 1).ToString()
        };

        var trialDays = parameters.GetInt("trialPeriodDays", 0);
        if (trialDays > 0)
        {
            formData["trial_period_days"] = trialDays.ToString();
        }

        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync("subscriptions", formData, ct);
    }

    private async Task<ActionResult> CancelSubscriptionAsync(ActionParameters parameters, CancellationToken ct)
    {
        var subscriptionId = parameters.GetString("subscriptionId")!;
        var cancelAtPeriodEnd = parameters.GetBool("cancelAtPeriodEnd", true);

        if (cancelAtPeriodEnd)
        {
            var formData = new Dictionary<string, string>
            {
                ["cancel_at_period_end"] = "true"
            };
            return await PostFormAsync($"subscriptions/{subscriptionId}", formData, ct);
        }

        var response = await _httpClient!.DeleteAsync($"subscriptions/{subscriptionId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to cancel subscription: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> CreateInvoiceAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["customer"] = parameters.GetString("customer")!,
            ["auto_advance"] = parameters.GetBool("autoAdvance", true).ToString().ToLower(),
            ["collection_method"] = parameters.GetString("collectionMethod") ?? "charge_automatically"
        };

        return await PostFormAsync("invoices", formData, ct);
    }

    private async Task<ActionResult> PayInvoiceAsync(ActionParameters parameters, CancellationToken ct)
    {
        var invoiceId = parameters.GetString("invoiceId")!;
        return await PostFormAsync($"invoices/{invoiceId}/pay", new Dictionary<string, string>(), ct);
    }

    private async Task<ActionResult> CreateProductAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["name"] = parameters.GetString("name")!
        };

        AddIfNotNull(formData, "description", parameters.GetString("description"));
        AddMetadata(formData, parameters.Get<JsonElement?>("metadata"));

        return await PostFormAsync("products", formData, ct);
    }

    private async Task<ActionResult> CreatePriceAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["product"] = parameters.GetString("product")!,
            ["unit_amount"] = parameters.GetInt("unitAmount").ToString(),
            ["currency"] = parameters.GetString("currency") ?? "usd"
        };

        var recurring = parameters.Get<JsonElement?>("recurring");
        if (recurring.HasValue && recurring.Value.ValueKind == JsonValueKind.Object)
        {
            if (recurring.Value.TryGetProperty("interval", out var interval))
            {
                formData["recurring[interval]"] = interval.GetString()!;
            }
            if (recurring.Value.TryGetProperty("intervalCount", out var count))
            {
                formData["recurring[interval_count]"] = count.GetInt32().ToString();
            }
        }

        return await PostFormAsync("prices", formData, ct);
    }

    private async Task<ActionResult> CreateCheckoutSessionAsync(ActionParameters parameters, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            ["mode"] = parameters.GetString("mode")!,
            ["success_url"] = parameters.GetString("successUrl")!,
            ["cancel_url"] = parameters.GetString("cancelUrl")!
        };

        AddIfNotNull(formData, "customer", parameters.GetString("customer"));
        AddIfNotNull(formData, "customer_email", parameters.GetString("customerEmail"));

        var lineItems = parameters.Get<JsonElement>("lineItems");
        if (lineItems.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in lineItems.EnumerateArray())
            {
                if (item.TryGetProperty("price", out var price))
                {
                    formData[$"line_items[{index}][price]"] = price.GetString()!;
                }
                if (item.TryGetProperty("quantity", out var qty))
                {
                    formData[$"line_items[{index}][quantity]"] = qty.GetInt32().ToString();
                }
                index++;
            }
        }

        return await PostFormAsync("checkout/sessions", formData, ct);
    }

    private async Task<ActionResult> GetBalanceAsync(CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync("balance", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get balance", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> GetAsync(string resource, string id, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync($"{resource}/{id}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"{resource} not found", "NOT_FOUND");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> PostFormAsync(string endpoint, Dictionary<string, string> formData, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(formData);
        var response = await _httpClient!.PostAsync(endpoint, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            var errorJson = JsonSerializer.Deserialize<JsonElement>(error);
            var errorMessage = errorJson.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : error;
            return ActionResult.Fail($"Stripe API error: {errorMessage}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private static void AddIfNotNull(Dictionary<string, string> formData, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            formData[key] = value;
        }
    }

    private static void AddMetadata(Dictionary<string, string> formData, JsonElement? metadata)
    {
        if (!metadata.HasValue || metadata.Value.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in metadata.Value.EnumerateObject())
        {
            formData[$"metadata[{prop.Name}]"] = prop.Value.ToString();
        }
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
