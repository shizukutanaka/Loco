// John Carmack: "Do the simple thing first"
// Rob Pike: "Simple is not easy, but it's worth it"

using System.Net;
using System.Net.Mail;
using System.Text;

namespace Loco.Core.Practical;

/// <summary>
/// Simple email sender - Send emails without complexity
/// SMTP support, templates, attachments
/// </summary>
public class SimpleEmail
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly SimpleLogger _logger;

    public SimpleEmail(
        string smtpHost,
        int smtpPort,
        string username,
        string password,
        string fromEmail,
        string fromName = "",
        SimpleLogger? logger = null)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _username = username;
        _password = password;
        _fromEmail = fromEmail;
        _fromName = string.IsNullOrEmpty(fromName) ? fromEmail : fromName;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleEmail));
    }

    // Send simple text email
    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        return await SendAsync(new EmailMessage
        {
            To = new[] { to },
            Subject = subject,
            Body = body,
            IsHtml = false
        });
    }

    // Send HTML email
    public async Task<bool> SendHtmlAsync(string to, string subject, string htmlBody)
    {
        return await SendAsync(new EmailMessage
        {
            To = new[] { to },
            Subject = subject,
            Body = htmlBody,
            IsHtml = true
        });
    }

    // Send email with full options
    public async Task<bool> SendAsync(EmailMessage message)
    {
        try
        {
            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_username, _password),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = message.IsHtml
            };

            // Add recipients
            foreach (var to in message.To)
            {
                mailMessage.To.Add(to);
            }

            if (message.Cc != null)
            {
                foreach (var cc in message.Cc)
                {
                    mailMessage.CC.Add(cc);
                }
            }

            if (message.Bcc != null)
            {
                foreach (var bcc in message.Bcc)
                {
                    mailMessage.Bcc.Add(bcc);
                }
            }

            // Add attachments
            if (message.Attachments != null)
            {
                foreach (var attachment in message.Attachments)
                {
                    mailMessage.Attachments.Add(new Attachment(attachment));
                }
            }

            // Add custom headers
            if (message.Headers != null)
            {
                foreach (var header in message.Headers)
                {
                    mailMessage.Headers.Add(header.Key, header.Value);
                }
            }

            await smtpClient.SendMailAsync(mailMessage);
            _logger.Info($"Email sent to {string.Join(", ", message.To)}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to send email to {string.Join(", ", message.To)}", ex);
            return false;
        }
    }
}

/// <summary>
/// Email message
/// </summary>
public class EmailMessage
{
    public string[] To { get; set; } = Array.Empty<string>();
    public string[]? Cc { get; set; }
    public string[]? Bcc { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsHtml { get; set; }
    public string[]? Attachments { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Email template engine
/// </summary>
public class EmailTemplate
{
    private string _template;
    private readonly Dictionary<string, string> _variables = new();

    public EmailTemplate(string template)
    {
        _template = template;
    }

    public EmailTemplate Set(string key, string value)
    {
        _variables[key] = value;
        return this;
    }

    public EmailTemplate SetMany(Dictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
        return this;
    }

    public string Render()
    {
        var result = _template;
        foreach (var kvp in _variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
        return result;
    }

    // Load template from file
    public static async Task<EmailTemplate> LoadAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        return new EmailTemplate(content);
    }
}

/// <summary>
/// Email queue for async sending
/// </summary>
public class EmailQueue
{
    private readonly SimpleEmail _emailSender;
    private readonly SimpleMessageQueue<EmailMessage> _queue;
    private readonly SimpleLogger _logger;

    public EmailQueue(SimpleEmail emailSender, SimpleLogger? logger = null)
    {
        _emailSender = emailSender;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(EmailQueue));
        _queue = new SimpleMessageQueue<EmailMessage>(capacity: 1000, logger: _logger);

        // Start consumers
        _queue.StartConsumer(async message =>
        {
            await _emailSender.SendAsync(message);
        }, consumerCount: 2);
    }

    public async Task EnqueueAsync(EmailMessage message)
    {
        await _queue.EnqueueAsync(message);
        _logger.Debug($"Queued email to {string.Join(", ", message.To)}");
    }

    public async Task StopAsync()
    {
        await _queue.StopAsync();
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}

/// <summary>
/// Common email templates
/// </summary>
public static class EmailTemplates
{
    public static string WelcomeEmail = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: #4CAF50; color: white; padding: 20px; }
        .content { padding: 20px; }
        .button { background: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Welcome {{name}}!</h1>
        </div>
        <div class=""content"">
            <p>Thank you for signing up for {{appName}}.</p>
            <p>Click the button below to get started:</p>
            <a href=""{{actionUrl}}"" class=""button"">Get Started</a>
        </div>
    </div>
</body>
</html>";

    public static string PasswordResetEmail = @"
<!DOCTYPE html>
<html>
<body>
    <h2>Password Reset Request</h2>
    <p>Hello {{name}},</p>
    <p>We received a request to reset your password.</p>
    <p>Click the link below to reset your password:</p>
    <a href=""{{resetUrl}}"">Reset Password</a>
    <p>This link will expire in 24 hours.</p>
    <p>If you didn't request this, please ignore this email.</p>
</body>
</html>";

    public static string NotificationEmail = @"
<!DOCTYPE html>
<html>
<body>
    <h2>{{title}}</h2>
    <p>{{message}}</p>
    <p><small>Sent at {{timestamp}}</small></p>
</body>
</html>";
}

/// <summary>
/// Email service with templates
/// </summary>
public class EmailService
{
    private readonly SimpleEmail _email;
    private readonly EmailQueue _queue;

    public EmailService(SimpleEmail email)
    {
        _email = email;
        _queue = new EmailQueue(email);
    }

    public async Task SendWelcomeEmailAsync(string to, string userName, string appName, string actionUrl)
    {
        var template = new EmailTemplate(EmailTemplates.WelcomeEmail)
            .Set("name", userName)
            .Set("appName", appName)
            .Set("actionUrl", actionUrl);

        await _email.SendHtmlAsync(to, $"Welcome to {appName}!", template.Render());
    }

    public async Task SendPasswordResetEmailAsync(string to, string userName, string resetUrl)
    {
        var template = new EmailTemplate(EmailTemplates.PasswordResetEmail)
            .Set("name", userName)
            .Set("resetUrl", resetUrl);

        await _email.SendHtmlAsync(to, "Password Reset Request", template.Render());
    }

    public async Task SendNotificationAsync(string to, string title, string message)
    {
        var template = new EmailTemplate(EmailTemplates.NotificationEmail)
            .Set("title", title)
            .Set("message", message)
            .Set("timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        await _queue.EnqueueAsync(new EmailMessage
        {
            To = new[] { to },
            Subject = title,
            Body = template.Render(),
            IsHtml = true
        });
    }

    public async Task StopAsync()
    {
        await _queue.StopAsync();
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}

/// <summary>
/// Email builder (fluent API)
/// </summary>
public class EmailBuilder
{
    private readonly EmailMessage _message = new();

    public EmailBuilder To(params string[] addresses)
    {
        _message.To = addresses;
        return this;
    }

    public EmailBuilder Cc(params string[] addresses)
    {
        _message.Cc = addresses;
        return this;
    }

    public EmailBuilder Bcc(params string[] addresses)
    {
        _message.Bcc = addresses;
        return this;
    }

    public EmailBuilder Subject(string subject)
    {
        _message.Subject = subject;
        return this;
    }

    public EmailBuilder Body(string body, bool isHtml = false)
    {
        _message.Body = body;
        _message.IsHtml = isHtml;
        return this;
    }

    public EmailBuilder Attach(params string[] filePaths)
    {
        _message.Attachments = filePaths;
        return this;
    }

    public EmailBuilder Header(string key, string value)
    {
        _message.Headers ??= new Dictionary<string, string>();
        _message.Headers[key] = value;
        return this;
    }

    public EmailMessage Build() => _message;
}

/// <summary>
/// Example usage
/// </summary>
public class EmailExamples
{
    public static async Task Examples()
    {
        // Setup email sender (example with Gmail)
        var email = new SimpleEmail(
            smtpHost: "smtp.gmail.com",
            smtpPort: 587,
            username: "your-email@gmail.com",
            password: "your-app-password",
            fromEmail: "your-email@gmail.com",
            fromName: "Your App"
        );

        // Send simple text email
        await email.SendAsync(
            to: "user@example.com",
            subject: "Hello",
            body: "This is a test email"
        );

        // Send HTML email
        await email.SendHtmlAsync(
            to: "user@example.com",
            subject: "Welcome!",
            htmlBody: "<h1>Welcome!</h1><p>Thanks for signing up.</p>"
        );

        // Send with attachments
        var message = new EmailBuilder()
            .To("user@example.com")
            .Subject("Report")
            .Body("<h1>Monthly Report</h1>", isHtml: true)
            .Attach("report.pdf")
            .Build();

        await email.SendAsync(message);

        // Use template
        var template = new EmailTemplate(EmailTemplates.WelcomeEmail)
            .Set("name", "John")
            .Set("appName", "MyApp")
            .Set("actionUrl", "https://myapp.com/start");

        await email.SendHtmlAsync("user@example.com", "Welcome!", template.Render());

        // Use email service
        var service = new EmailService(email);
        await service.SendWelcomeEmailAsync(
            "user@example.com",
            "John Doe",
            "MyApp",
            "https://myapp.com/start"
        );

        await service.SendNotificationAsync(
            "user@example.com",
            "New Message",
            "You have a new message from Alice"
        );
    }
}