using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Shopify e-commerce connector for product, order, and customer management.
/// Uses Shopify Admin REST API.
/// </summary>
public sealed class ShopifyConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _shopDomain;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "shopify";
    public override string Name => "Shopify";
    public override string Description => "E-commerce platform for online stores and retail point-of-sale systems";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Other;
    public override string IconUrl => "https://cdn.shopify.com/shopifycloud/web/assets/v1/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForApi();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "shopDomain", Label = "Shop Domain", Type = ParameterType.String, Description = "Your myshopify.com domain (e.g., mystore.myshopify.com)" },
            new() { Name = "accessToken", Label = "Admin API Access Token", Type = ParameterType.Password }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Products
        new()
        {
            Id = "listProducts",
            Name = "List Products",
            Description = "Get all products from your store",
            Parameters = new ActionParameter[]
            {
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50, Description = "Number of products to return (max 250)" },
                new() { Name = "productType", Type = ParameterType.String },
                new() { Name = "vendor", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String, Description = "active, archived, or draft" }
            }
        },
        new()
        {
            Id = "getProduct",
            Name = "Get Product",
            Description = "Get a specific product by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "productId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createProduct",
            Name = "Create Product",
            Description = "Create a new product",
            Parameters = new ActionParameter[]
            {
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "bodyHtml", Type = ParameterType.String, Description = "Description (HTML)" },
                new() { Name = "vendor", Type = ParameterType.String },
                new() { Name = "productType", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String, Description = "Comma-separated tags" },
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "draft" },
                new() { Name = "price", Type = ParameterType.Number },
                new() { Name = "sku", Type = ParameterType.String },
                new() { Name = "inventoryQuantity", Type = ParameterType.Number }
            }
        },
        new()
        {
            Id = "updateProduct",
            Name = "Update Product",
            Description = "Update an existing product",
            Parameters = new ActionParameter[]
            {
                new() { Name = "productId", Type = ParameterType.String, Required = true },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "bodyHtml", Type = ParameterType.String },
                new() { Name = "vendor", Type = ParameterType.String },
                new() { Name = "productType", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "deleteProduct",
            Name = "Delete Product",
            Description = "Delete a product",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "productId", Type = ParameterType.String, Required = true }
            }
        },

        // Orders
        new()
        {
            Id = "listOrders",
            Name = "List Orders",
            Description = "Get all orders from your store",
            Parameters = new ActionParameter[]
            {
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50 },
                new() { Name = "status", Type = ParameterType.String, Description = "open, closed, cancelled, or any" },
                new() { Name = "financialStatus", Type = ParameterType.String, Description = "pending, paid, refunded, etc." },
                new() { Name = "fulfillmentStatus", Type = ParameterType.String, Description = "shipped, unshipped, partial, etc." },
                new() { Name = "createdAtMin", Type = ParameterType.DateTime },
                new() { Name = "createdAtMax", Type = ParameterType.DateTime }
            }
        },
        new()
        {
            Id = "getOrder",
            Name = "Get Order",
            Description = "Get a specific order by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "orderId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createOrder",
            Name = "Create Order",
            Description = "Create a new order",
            Parameters = new ActionParameter[]
            {
                new() { Name = "email", Type = ParameterType.String, Required = true, Description = "Customer email" },
                new() { Name = "lineItems", Type = ParameterType.Json, Required = true, Description = "Array of {variant_id, quantity}" },
                new() { Name = "shippingAddress", Type = ParameterType.Json },
                new() { Name = "billingAddress", Type = ParameterType.Json },
                new() { Name = "financialStatus", Type = ParameterType.String, DefaultValue = "pending" },
                new() { Name = "note", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "cancelOrder",
            Name = "Cancel Order",
            Description = "Cancel an order",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "orderId", Type = ParameterType.String, Required = true },
                new() { Name = "reason", Type = ParameterType.String, Description = "customer, inventory, fraud, declined, or other" },
                new() { Name = "email", Type = ParameterType.Boolean, DefaultValue = true, Description = "Send email notification" },
                new() { Name = "restock", Type = ParameterType.Boolean, DefaultValue = true }
            }
        },

        // Customers
        new()
        {
            Id = "listCustomers",
            Name = "List Customers",
            Description = "Get all customers",
            Parameters = new ActionParameter[]
            {
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50 },
                new() { Name = "createdAtMin", Type = ParameterType.DateTime },
                new() { Name = "updatedAtMin", Type = ParameterType.DateTime }
            }
        },
        new()
        {
            Id = "getCustomer",
            Name = "Get Customer",
            Description = "Get a specific customer by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "customerId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createCustomer",
            Name = "Create Customer",
            Description = "Create a new customer",
            Parameters = new ActionParameter[]
            {
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String },
                new() { Name = "note", Type = ParameterType.String },
                new() { Name = "acceptsMarketing", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "address", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "searchCustomers",
            Name = "Search Customers",
            Description = "Search customers by query",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true, Description = "Search by email, name, or other fields" }
            }
        },

        // Inventory
        new()
        {
            Id = "getInventoryLevels",
            Name = "Get Inventory Levels",
            Description = "Get inventory levels for a location",
            Parameters = new ActionParameter[]
            {
                new() { Name = "locationId", Type = ParameterType.String, Required = true },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "adjustInventory",
            Name = "Adjust Inventory",
            Description = "Adjust inventory quantity for an item",
            Parameters = new ActionParameter[]
            {
                new() { Name = "inventoryItemId", Type = ParameterType.String, Required = true },
                new() { Name = "locationId", Type = ParameterType.String, Required = true },
                new() { Name = "adjustment", Type = ParameterType.Number, Required = true, Description = "Positive or negative number" }
            }
        },
        new()
        {
            Id = "setInventory",
            Name = "Set Inventory",
            Description = "Set inventory quantity for an item",
            Parameters = new ActionParameter[]
            {
                new() { Name = "inventoryItemId", Type = ParameterType.String, Required = true },
                new() { Name = "locationId", Type = ParameterType.String, Required = true },
                new() { Name = "quantity", Type = ParameterType.Number, Required = true }
            }
        },

        // Fulfillment
        new()
        {
            Id = "createFulfillment",
            Name = "Create Fulfillment",
            Description = "Create a fulfillment for an order",
            Parameters = new ActionParameter[]
            {
                new() { Name = "orderId", Type = ParameterType.String, Required = true },
                new() { Name = "trackingNumber", Type = ParameterType.String },
                new() { Name = "trackingCompany", Type = ParameterType.String },
                new() { Name = "trackingUrl", Type = ParameterType.String },
                new() { Name = "notifyCustomer", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "lineItems", Type = ParameterType.Json, Description = "Specific items to fulfill" }
            }
        },

        // Collections
        new()
        {
            Id = "listCollections",
            Name = "List Collections",
            Description = "Get all custom collections",
            Parameters = new ActionParameter[]
            {
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },

        // Locations
        new()
        {
            Id = "listLocations",
            Name = "List Locations",
            Description = "Get all locations",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Refunds
        new()
        {
            Id = "createRefund",
            Name = "Create Refund",
            Description = "Create a refund for an order",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "orderId", Type = ParameterType.String, Required = true },
                new() { Name = "amount", Type = ParameterType.Number, Description = "Leave empty for full refund" },
                new() { Name = "note", Type = ParameterType.String },
                new() { Name = "notify", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "restock", Type = ParameterType.Boolean, DefaultValue = true }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "orderCreated",
            Name = "Order Created",
            Description = "Triggered when a new order is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "orderPaid",
            Name = "Order Paid",
            Description = "Triggered when an order is paid",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "orderFulfilled",
            Name = "Order Fulfilled",
            Description = "Triggered when an order is fulfilled",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "productCreated",
            Name = "Product Created",
            Description = "Triggered when a new product is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "customerCreated",
            Name = "Customer Created",
            Description = "Triggered when a new customer is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "inventoryLevelUpdated",
            Name = "Inventory Level Updated",
            Description = "Triggered when inventory levels change",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        _shopDomain = config.GetCredentialString("shopDomain")!.TrimEnd('/');
        if (!_shopDomain.Contains('.'))
        {
            _shopDomain = $"{_shopDomain}.myshopify.com";
        }

        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://{_shopDomain}/admin/api/2024-01/")
        };
        _httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "listProducts" => await ListProductsAsync(parameters, ct),
            "getProduct" => await GetResourceAsync("products", parameters.GetString("productId")!, ct),
            "createProduct" => await CreateProductAsync(parameters, ct),
            "updateProduct" => await UpdateProductAsync(parameters, ct),
            "deleteProduct" => await DeleteResourceAsync("products", parameters.GetString("productId")!, ct),

            "listOrders" => await ListOrdersAsync(parameters, ct),
            "getOrder" => await GetResourceAsync("orders", parameters.GetString("orderId")!, ct),
            "createOrder" => await CreateOrderAsync(parameters, ct),
            "cancelOrder" => await CancelOrderAsync(parameters, ct),

            "listCustomers" => await ListCustomersAsync(parameters, ct),
            "getCustomer" => await GetResourceAsync("customers", parameters.GetString("customerId")!, ct),
            "createCustomer" => await CreateCustomerAsync(parameters, ct),
            "searchCustomers" => await SearchCustomersAsync(parameters, ct),

            "getInventoryLevels" => await GetInventoryLevelsAsync(parameters, ct),
            "adjustInventory" => await AdjustInventoryAsync(parameters, ct),
            "setInventory" => await SetInventoryAsync(parameters, ct),

            "createFulfillment" => await CreateFulfillmentAsync(parameters, ct),
            "listCollections" => await ListCollectionsAsync(parameters, ct),
            "listLocations" => await GetAsync("locations.json", ct),
            "createRefund" => await CreateRefundAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> ListProductsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var limit = parameters.GetInt("limit");
        if (limit > 0)
            queryParams.Add($"limit={limit}");

        var productType = parameters.GetString("productType");
        if (!string.IsNullOrEmpty(productType))
            queryParams.Add($"product_type={Uri.EscapeDataString(productType)}");

        var vendor = parameters.GetString("vendor");
        if (!string.IsNullOrEmpty(vendor))
            queryParams.Add($"vendor={Uri.EscapeDataString(vendor)}");

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={status}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"products.json{query}", ct);
    }

    private async Task<ActionResult> CreateProductAsync(ActionParameters parameters, CancellationToken ct)
    {
        var product = new Dictionary<string, object>
        {
            ["title"] = parameters.GetString("title")!
        };

        var bodyHtml = parameters.GetString("bodyHtml");
        if (!string.IsNullOrEmpty(bodyHtml))
            product["body_html"] = bodyHtml;

        var vendor = parameters.GetString("vendor");
        if (!string.IsNullOrEmpty(vendor))
            product["vendor"] = vendor;

        var productType = parameters.GetString("productType");
        if (!string.IsNullOrEmpty(productType))
            product["product_type"] = productType;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            product["tags"] = tags;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status))
            product["status"] = status;

        var price = parameters.GetInt("price");
        var sku = parameters.GetString("sku");
        var qty = parameters.GetInt("inventoryQuantity");

        if (price > 0 || !string.IsNullOrEmpty(sku) || qty > 0)
        {
            var variant = new Dictionary<string, object>();
            if (price > 0) variant["price"] = price;
            if (!string.IsNullOrEmpty(sku)) variant["sku"] = sku;
            if (qty > 0) variant["inventory_quantity"] = qty;
            product["variants"] = new[] { variant };
        }

        return await PostAsync("products.json", new { product }, ct);
    }

    private async Task<ActionResult> UpdateProductAsync(ActionParameters parameters, CancellationToken ct)
    {
        var productId = parameters.GetString("productId")!;
        var product = new Dictionary<string, object> { ["id"] = productId };

        var title = parameters.GetString("title");
        if (!string.IsNullOrEmpty(title))
            product["title"] = title;

        var bodyHtml = parameters.GetString("bodyHtml");
        if (!string.IsNullOrEmpty(bodyHtml))
            product["body_html"] = bodyHtml;

        var vendor = parameters.GetString("vendor");
        if (!string.IsNullOrEmpty(vendor))
            product["vendor"] = vendor;

        var productType = parameters.GetString("productType");
        if (!string.IsNullOrEmpty(productType))
            product["product_type"] = productType;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            product["tags"] = tags;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status))
            product["status"] = status;

        return await PutAsync($"products/{productId}.json", new { product }, ct);
    }

    private async Task<ActionResult> ListOrdersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var limit = parameters.GetInt("limit");
        if (limit > 0)
            queryParams.Add($"limit={limit}");

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={status}");

        var financialStatus = parameters.GetString("financialStatus");
        if (!string.IsNullOrEmpty(financialStatus))
            queryParams.Add($"financial_status={financialStatus}");

        var fulfillmentStatus = parameters.GetString("fulfillmentStatus");
        if (!string.IsNullOrEmpty(fulfillmentStatus))
            queryParams.Add($"fulfillment_status={fulfillmentStatus}");

        var createdAtMin = parameters.GetString("createdAtMin");
        if (!string.IsNullOrEmpty(createdAtMin))
            queryParams.Add($"created_at_min={Uri.EscapeDataString(createdAtMin)}");

        var createdAtMax = parameters.GetString("createdAtMax");
        if (!string.IsNullOrEmpty(createdAtMax))
            queryParams.Add($"created_at_max={Uri.EscapeDataString(createdAtMax)}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"orders.json{query}", ct);
    }

    private async Task<ActionResult> CreateOrderAsync(ActionParameters parameters, CancellationToken ct)
    {
        var order = new Dictionary<string, object>
        {
            ["email"] = parameters.GetString("email")!,
            ["line_items"] = parameters.Get<JsonElement>("lineItems")
        };

        var shippingAddress = parameters.Get<JsonElement?>("shippingAddress");
        if (shippingAddress.HasValue && shippingAddress.Value.ValueKind != JsonValueKind.Undefined)
            order["shipping_address"] = shippingAddress.Value;

        var billingAddress = parameters.Get<JsonElement?>("billingAddress");
        if (billingAddress.HasValue && billingAddress.Value.ValueKind != JsonValueKind.Undefined)
            order["billing_address"] = billingAddress.Value;

        var financialStatus = parameters.GetString("financialStatus");
        if (!string.IsNullOrEmpty(financialStatus))
            order["financial_status"] = financialStatus;

        var note = parameters.GetString("note");
        if (!string.IsNullOrEmpty(note))
            order["note"] = note;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            order["tags"] = tags;

        return await PostAsync("orders.json", new { order }, ct);
    }

    private async Task<ActionResult> CancelOrderAsync(ActionParameters parameters, CancellationToken ct)
    {
        var orderId = parameters.GetString("orderId")!;
        var payload = new Dictionary<string, object>();

        var reason = parameters.GetString("reason");
        if (!string.IsNullOrEmpty(reason))
            payload["reason"] = reason;

        if (parameters.GetBool("email", true))
            payload["email"] = true;

        if (parameters.GetBool("restock", true))
            payload["restock"] = true;

        return await PostAsync($"orders/{orderId}/cancel.json", payload, ct);
    }

    private async Task<ActionResult> ListCustomersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var limit = parameters.GetInt("limit");
        if (limit > 0)
            queryParams.Add($"limit={limit}");

        var createdAtMin = parameters.GetString("createdAtMin");
        if (!string.IsNullOrEmpty(createdAtMin))
            queryParams.Add($"created_at_min={Uri.EscapeDataString(createdAtMin)}");

        var updatedAtMin = parameters.GetString("updatedAtMin");
        if (!string.IsNullOrEmpty(updatedAtMin))
            queryParams.Add($"updated_at_min={Uri.EscapeDataString(updatedAtMin)}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"customers.json{query}", ct);
    }

    private async Task<ActionResult> CreateCustomerAsync(ActionParameters parameters, CancellationToken ct)
    {
        var customer = new Dictionary<string, object>
        {
            ["email"] = parameters.GetString("email")!
        };

        var firstName = parameters.GetString("firstName");
        if (!string.IsNullOrEmpty(firstName))
            customer["first_name"] = firstName;

        var lastName = parameters.GetString("lastName");
        if (!string.IsNullOrEmpty(lastName))
            customer["last_name"] = lastName;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone))
            customer["phone"] = phone;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            customer["tags"] = tags;

        var note = parameters.GetString("note");
        if (!string.IsNullOrEmpty(note))
            customer["note"] = note;

        if (parameters.GetBool("acceptsMarketing"))
            customer["accepts_marketing"] = true;

        var address = parameters.Get<JsonElement?>("address");
        if (address.HasValue && address.Value.ValueKind != JsonValueKind.Undefined)
            customer["addresses"] = new[] { address.Value };

        return await PostAsync("customers.json", new { customer }, ct);
    }

    private async Task<ActionResult> SearchCustomersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = Uri.EscapeDataString(parameters.GetString("query")!);
        return await GetAsync($"customers/search.json?query={query}", ct);
    }

    private async Task<ActionResult> GetInventoryLevelsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var locationId = parameters.GetString("locationId")!;
        var limit = parameters.GetInt("limit", 50);
        return await GetAsync($"inventory_levels.json?location_ids={locationId}&limit={limit}", ct);
    }

    private async Task<ActionResult> AdjustInventoryAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new
        {
            location_id = long.Parse(parameters.GetString("locationId")!),
            inventory_item_id = long.Parse(parameters.GetString("inventoryItemId")!),
            available_adjustment = parameters.GetInt("adjustment", 0)
        };
        return await PostAsync("inventory_levels/adjust.json", payload, ct);
    }

    private async Task<ActionResult> SetInventoryAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new
        {
            location_id = long.Parse(parameters.GetString("locationId")!),
            inventory_item_id = long.Parse(parameters.GetString("inventoryItemId")!),
            available = parameters.GetInt("quantity", 0)
        };
        return await PostAsync("inventory_levels/set.json", payload, ct);
    }

    private async Task<ActionResult> CreateFulfillmentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var orderId = parameters.GetString("orderId")!;
        var fulfillment = new Dictionary<string, object>
        {
            ["notify_customer"] = parameters.GetBool("notifyCustomer", true)
        };

        var trackingInfo = new Dictionary<string, object>();

        var trackingNumber = parameters.GetString("trackingNumber");
        if (!string.IsNullOrEmpty(trackingNumber))
            trackingInfo["number"] = trackingNumber;

        var trackingCompany = parameters.GetString("trackingCompany");
        if (!string.IsNullOrEmpty(trackingCompany))
            trackingInfo["company"] = trackingCompany;

        var trackingUrl = parameters.GetString("trackingUrl");
        if (!string.IsNullOrEmpty(trackingUrl))
            trackingInfo["url"] = trackingUrl;

        if (trackingInfo.Count > 0)
            fulfillment["tracking_info"] = trackingInfo;

        var lineItems = parameters.Get<JsonElement?>("lineItems");
        if (lineItems.HasValue && lineItems.Value.ValueKind != JsonValueKind.Undefined)
            fulfillment["line_items_by_fulfillment_order"] = lineItems.Value;

        return await PostAsync("fulfillments.json", new { fulfillment }, ct);
    }

    private async Task<ActionResult> ListCollectionsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var limit = parameters.GetInt("limit", 50);
        return await GetAsync($"custom_collections.json?limit={limit}", ct);
    }

    private async Task<ActionResult> CreateRefundAsync(ActionParameters parameters, CancellationToken ct)
    {
        var orderId = parameters.GetString("orderId")!;
        var refund = new Dictionary<string, object>
        {
            ["notify"] = parameters.GetBool("notify", true),
            ["restock"] = parameters.GetBool("restock", true)
        };

        var note = parameters.GetString("note");
        if (!string.IsNullOrEmpty(note))
            refund["note"] = note;

        var amount = parameters.GetInt("amount");
        if (amount > 0)
        {
            var transactions = new[]
            {
                new Dictionary<string, object>
                {
                    ["amount"] = amount,
                    ["kind"] = "refund",
                    ["gateway"] = "manual"
                }
            };
            refund["transactions"] = transactions;
        }

        return await PostAsync($"orders/{orderId}/refunds.json", new { refund }, ct);
    }

    private async Task<ActionResult> GetAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync(endpoint, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> GetResourceAsync(string resource, string id, CancellationToken ct)
    {
        return await GetAsync($"{resource}/{id}.json", ct);
    }

    private async Task<ActionResult> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        var response = await _httpClient!.PostAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PutAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        var response = await _httpClient!.PutAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> DeleteResourceAsync(string resource, string id, CancellationToken ct)
    {
        var response = await _httpClient!.DeleteAsync($"{resource}/{id}.json", ct);
        return await ProcessResponseAsync(response, ct);
    }

    private static async Task<ActionResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrEmpty(content))
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["status"] = "success" });
            }

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                return ActionResult.Ok(data ?? new Dictionary<string, object>());
            }
            catch
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["response"] = content });
            }
        }

        return ActionResult.Fail($"Shopify API error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
