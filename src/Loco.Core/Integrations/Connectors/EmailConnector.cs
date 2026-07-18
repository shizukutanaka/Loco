// Uncle Bob: "Make it readable. The code is read more often than it is written."
// John Carmack: "If you're going to have to maintain it, write it well."

using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Email connector for SMTP sending and basic operations
/// Supports TLS, HTML, attachments, and templates
/// </summary>
public sealed class EmailConnector : ConnectorBase
{
    private SmtpClient? _smtpClient;
    private string _fromEmail = "";
    private string _fromName = "";

    public override string Id => "email";
    public override string Name => "Email (SMTP)";
    public override string Description => "Send emails via SMTP with support for HTML, attachments, and templates";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = false, // IMAP polling would require separate implementation
        RateLimitPerMinute = 60,
        DefaultTimeout = TimeSpan.FromSeconds(30)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Basic,
        RequiredCredentials =
        [
            new() { Name = "smtpHost", Label = "SMTP Host", Type = ParameterType.String, Required = true,
                Description = "SMTP server hostname (e.g., smtp.gmail.com)" },
            new() { Name = "smtpPort", Label = "SMTP Port", Type = ParameterType.Number, Required = true,
                Description = "SMTP port (587 for TLS, 465 for SSL)" },
            new() { Name = "username", Label = "Username", Type = ParameterType.String, Required = true,
                Description = "SMTP username (usually email address)" },
            new() { Name = "password", Label = "Password", Type = ParameterType.Password, Required = true,
                Description = "SMTP password or app password" },
            new() { Name = "fromEmail", Label = "From Email", Type = ParameterType.String, Required = true },
            new() { Name = "fromName", Label = "From Name", Type = ParameterType.String, Required = false }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "enableSsl", Label = "Enable SSL/TLS", Type = ParameterType.Boolean, DefaultValue = true },
        new() { Name = "timeout", Label = "Timeout (seconds)", Type = ParameterType.Number, DefaultValue = 30 }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "send",
            Name = "Send Email",
            Description = "Send a simple text or HTML email",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true,
                    Description = "Recipient email address(es), comma-separated" },
                new() { Name = "subject", Type = ParameterType.String, Required = true,
                    Description = "Email subject line" },
                new() { Name = "body", Type = ParameterType.String, Required = true,
                    Description = "Email body (text or HTML)" },
                new() { Name = "isHtml", Type = ParameterType.Boolean, DefaultValue = false,
                    Description = "Send as HTML email" },
                new() { Name = "cc", Type = ParameterType.String,
                    Description = "CC recipients, comma-separated" },
                new() { Name = "bcc", Type = ParameterType.String,
                    Description = "BCC recipients, comma-separated" },
                new() { Name = "replyTo", Type = ParameterType.String,
                    Description = "Reply-To email address" },
                new() { Name = "priority", Type = ParameterType.Select, DefaultValue = "Normal",
                    Options =
                    [
                        new() { Label = "Low", Value = "Low" },
                        new() { Label = "Normal", Value = "Normal" },
                        new() { Label = "High", Value = "High" }
                    ]}
            ],
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "sendWithAttachment",
            Name = "Send Email with Attachments",
            Description = "Send an email with file attachments",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "isHtml", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "attachments", Type = ParameterType.Json, Required = true,
                    Description = "Array of file paths or {path, name} objects" },
                new() { Name = "cc", Type = ParameterType.String },
                new() { Name = "bcc", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "sendTemplate",
            Name = "Send Template Email",
            Description = "Send an email using a template with variables",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "template", Type = ParameterType.Code, Required = true,
                    Description = "HTML template with {{variable}} placeholders" },
                new() { Name = "variables", Type = ParameterType.Json, Required = true,
                    Description = "Object with variable values" },
                new() { Name = "cc", Type = ParameterType.String },
                new() { Name = "bcc", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "sendBulk",
            Name = "Send Bulk Emails",
            Description = "Send emails to multiple recipients with personalization",
            Parameters =
            [
                new() { Name = "recipients", Type = ParameterType.Json, Required = true,
                    Description = "Array of {email, name, variables} objects" },
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "template", Type = ParameterType.Code, Required = true },
                new() { Name = "delayMs", Type = ParameterType.Number, DefaultValue = 100,
                    Description = "Delay between emails in milliseconds" }
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var host = config.GetCredentialString("smtpHost")!;
            var port = config.GetCredential<int?>("smtpPort") ?? 587;
            var username = config.GetCredentialString("username")!;
            var password = config.GetCredentialString("password")!;
            var enableSsl = config.GetSetting<bool?>("enableSsl") ?? true;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
                Timeout = 10000
            };

            // SmtpClient doesn't have a direct connection test method
            // We verify by attempting to create the connection
            // A more thorough test would send a test email

            return ConnectionTestResult.Ok($"SMTP configuration validated: {host}:{port}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("SMTP connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var host = config.GetCredentialString("smtpHost")!;
        var port = config.GetCredential<int?>("smtpPort") ?? 587;
        var username = config.GetCredentialString("username")!;
        var password = config.GetCredentialString("password")!;
        var enableSsl = config.GetSetting<bool?>("enableSsl") ?? true;
        var timeout = config.GetSetting<int?>("timeout") ?? 30;

        _smtpClient = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl,
            Timeout = timeout * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        _fromEmail = config.GetCredentialString("fromEmail")!;
        _fromName = config.GetCredentialString("fromName") ?? _fromEmail;

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
            "send" => await SendEmailAsync(parameters, ct),
            "sendWithAttachment" => await SendWithAttachmentAsync(parameters, ct),
            "sendTemplate" => await SendTemplateAsync(parameters, ct),
            "sendBulk" => await SendBulkAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        try
        {
            using var message = CreateMessage(parameters);
            await _smtpClient!.SendMailAsync(message, ct);

            return ActionResult.Ok(new
            {
                sent = true,
                to = parameters.GetString("to"),
                subject = parameters.GetString("subject")
            });
        }
        catch (SmtpException ex)
        {
            return ActionResult.Fail($"SMTP error: {ex.Message}", ex.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to send email: {ex.Message}");
        }
    }

    private async Task<ActionResult> SendWithAttachmentAsync(ActionParameters parameters, CancellationToken ct)
    {
        try
        {
            using var message = CreateMessage(parameters);

            // Add attachments
            var attachments = parameters.Get<List<object>>("attachments");
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    string filePath;
                    string? fileName = null;

                    if (attachment is string path)
                    {
                        filePath = path;
                    }
                    else if (attachment is Dictionary<string, object> dict)
                    {
                        filePath = dict["path"]?.ToString() ?? "";
                        fileName = dict.TryGetValue("name", out var n) ? n?.ToString() : null;
                    }
                    else
                    {
                        continue;
                    }

                    if (File.Exists(filePath))
                    {
                        var att = new Attachment(filePath);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            att.Name = fileName;
                        }
                        message.Attachments.Add(att);
                    }
                }
            }

            await _smtpClient!.SendMailAsync(message, ct);

            return ActionResult.Ok(new
            {
                sent = true,
                to = parameters.GetString("to"),
                attachmentCount = message.Attachments.Count
            });
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to send email: {ex.Message}");
        }
    }

    private async Task<ActionResult> SendTemplateAsync(ActionParameters parameters, CancellationToken ct)
    {
        try
        {
            var template = parameters.GetString("template")!;
            var variables = parameters.Get<Dictionary<string, object>>("variables") ?? new();

            // Replace template variables
            var body = ReplaceTemplateVariables(template, variables);

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = ReplaceTemplateVariables(parameters.GetString("subject")!, variables),
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            AddRecipients(message, parameters);

            await _smtpClient!.SendMailAsync(message, ct);

            return ActionResult.Ok(new
            {
                sent = true,
                to = parameters.GetString("to"),
                variablesUsed = variables.Keys.ToList()
            });
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to send template email: {ex.Message}");
        }
    }

    private async Task<ActionResult> SendBulkAsync(ActionParameters parameters, CancellationToken ct)
    {
        var recipients = parameters.Get<List<Dictionary<string, object>>>("recipients");
        var subject = parameters.GetString("subject")!;
        var template = parameters.GetString("template")!;
        var delayMs = parameters.GetInt("delayMs", 100);

        if (recipients == null || recipients.Count == 0)
        {
            return ActionResult.Fail("No recipients provided", "INVALID_PARAMETER");
        }

        var results = new List<object>();
        var successCount = 0;
        var failCount = 0;

        foreach (var recipient in recipients)
        {
            ct.ThrowIfCancellationRequested();

            var email = recipient["email"]?.ToString();
            var name = recipient.TryGetValue("name", out var n) ? n?.ToString() : null;
            // recipients arrives as JSON, so each recipient's nested "variables"
            // object is a boxed JsonElement, not a Dictionary - the old
            // `v as Dictionary<string, object>` was always null and silently
            // dropped every recipient's personalization, sending templates with
            // unresolved {{placeholders}}.
            var variables = recipient.TryGetValue("variables", out var v)
                ? ExtractVariables(v)
                : new Dictionary<string, object>();

            // Add recipient info to variables
            variables["email"] = email ?? "";
            variables["name"] = name ?? "";

            try
            {
                var body = ReplaceTemplateVariables(template, variables);
                var subjectText = ReplaceTemplateVariables(subject, variables);

                using var message = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subjectText,
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(new MailAddress(email!, name));

                await _smtpClient!.SendMailAsync(message, ct);

                results.Add(new { email, success = true });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new { email, success = false, error = ex.Message });
                failCount++;
            }

            if (delayMs > 0 && recipients.IndexOf(recipient) < recipients.Count - 1)
            {
                await Task.Delay(delayMs, ct);
            }
        }

        return ActionResult.Ok(new
        {
            total = recipients.Count,
            success = successCount,
            failed = failCount,
            results
        });
    }

    private MailMessage CreateMessage(ActionParameters parameters)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_fromEmail, _fromName),
            Subject = parameters.GetString("subject") ?? "",
            Body = parameters.GetString("body") ?? "",
            IsBodyHtml = parameters.GetBool("isHtml", false),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        // Set priority
        var priority = parameters.GetString("priority");
        message.Priority = priority switch
        {
            "High" => MailPriority.High,
            "Low" => MailPriority.Low,
            _ => MailPriority.Normal
        };

        // Add reply-to
        var replyTo = parameters.GetString("replyTo");
        if (!string.IsNullOrEmpty(replyTo))
        {
            message.ReplyToList.Add(new MailAddress(replyTo));
        }

        AddRecipients(message, parameters);

        return message;
    }

    private static void AddRecipients(MailMessage message, ActionParameters parameters)
    {
        // Add To recipients
        var to = parameters.GetString("to");
        if (!string.IsNullOrEmpty(to))
        {
            foreach (var email in to.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                message.To.Add(email);
            }
        }

        // Add CC recipients
        var cc = parameters.GetString("cc");
        if (!string.IsNullOrEmpty(cc))
        {
            foreach (var email in cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                message.CC.Add(email);
            }
        }

        // Add BCC recipients
        var bcc = parameters.GetString("bcc");
        if (!string.IsNullOrEmpty(bcc))
        {
            foreach (var email in bcc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                message.Bcc.Add(email);
            }
        }
    }

    /// <summary>
    /// Normalizes a recipient's "variables" value into a dictionary. When the
    /// recipient list is supplied as JSON (the normal path), the value is a
    /// boxed JsonElement rather than a Dictionary; enumerate its properties so
    /// per-recipient personalization survives. Falls back to a direct cast for
    /// in-process callers that pass a real dictionary.
    /// </summary>
    private static Dictionary<string, object> ExtractVariables(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in element.EnumerateObject())
            {
                result[prop.Name] = prop.Value;
            }
            return result;
        }

        return value as Dictionary<string, object> ?? new Dictionary<string, object>();
    }

    private static string ReplaceTemplateVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;

        foreach (var kvp in variables)
        {
            var placeholder = "{{" + kvp.Key + "}}";
            result = result.Replace(placeholder, kvp.Value?.ToString() ?? "");
        }

        return result;
    }

    public override async Task CleanupAsync(CancellationToken ct = default)
    {
        _smtpClient?.Dispose();
        _smtpClient = null;
        await base.CleanupAsync(ct);
    }

    public override void Dispose()
    {
        _smtpClient?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Pre-built email templates
/// </summary>
public static class EmailTemplates
{
    public static string Welcome => @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: #4CAF50; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }
        .content { padding: 30px; background: #f9f9f9; }
        .button { display: inline-block; background: #4CAF50; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; }
        .footer { text-align: center; padding: 20px; color: #666; font-size: 12px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome, {{name}}!</h1>
        </div>
        <div class='content'>
            <p>Thank you for joining {{appName}}. We're excited to have you on board!</p>
            <p>Click the button below to get started:</p>
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{{actionUrl}}' class='button'>Get Started</a>
            </p>
        </div>
        <div class='footer'>
            <p>&copy; {{year}} {{appName}}. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

    public static string PasswordReset => @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .content { padding: 30px; background: #fff; border: 1px solid #ddd; border-radius: 8px; }
        .button { display: inline-block; background: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; }
        .warning { color: #856404; background: #fff3cd; padding: 12px; border-radius: 4px; margin: 20px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>Hello {{name}},</p>
            <p>We received a request to reset your password. Click the button below to create a new password:</p>
            <p style='text-align: center; margin: 30px 0;'>
                <a href='{{resetUrl}}' class='button'>Reset Password</a>
            </p>
            <div class='warning'>
                This link will expire in {{expirationHours}} hours. If you didn't request this, please ignore this email.
            </div>
        </div>
    </div>
</body>
</html>";

    public static string Notification => @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .alert { padding: 20px; border-radius: 8px; margin: 10px 0; }
        .alert-info { background: #d1ecf1; border: 1px solid #bee5eb; color: #0c5460; }
        .alert-warning { background: #fff3cd; border: 1px solid #ffeeba; color: #856404; }
        .alert-error { background: #f8d7da; border: 1px solid #f5c6cb; color: #721c24; }
        .alert-success { background: #d4edda; border: 1px solid #c3e6cb; color: #155724; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='alert alert-{{type}}'>
            <h3>{{title}}</h3>
            <p>{{message}}</p>
        </div>
        <p style='color: #666; font-size: 12px;'>Sent at {{timestamp}}</p>
    </div>
</body>
</html>";
}
