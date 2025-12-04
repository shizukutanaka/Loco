// Rob Pike: "Data dominates. If you've chosen the right data structures, the algorithms will be self-evident"
// John Carmack: "Keep it simple and fast"

using System.Text;
using System.Text.RegularExpressions;

namespace Loco.Core.Practical;

/// <summary>
/// Simple template engine - Replace variables, conditionals, loops
/// Fast, no compilation, easy to debug
/// </summary>
public class SimpleTemplate
{
    private readonly string _template;
    private readonly Dictionary<string, object?> _context = new();
    private readonly SimpleLogger _logger;

    public SimpleTemplate(string template, SimpleLogger? logger = null)
    {
        _template = template;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleTemplate));
    }

    // Set variable
    public SimpleTemplate Set(string key, object? value)
    {
        _context[key] = value;
        return this;
    }

    // Set multiple variables
    public SimpleTemplate SetMany(Dictionary<string, object?> values)
    {
        foreach (var kvp in values)
        {
            _context[kvp.Key] = kvp.Value;
        }
        return this;
    }

    // Render template
    public string Render()
    {
        var result = _template;

        // Replace variables: {{name}}
        result = Regex.Replace(result, @"\{\{(\w+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (_context.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? "";
            }
            _logger.Warning($"Variable not found: {key}");
            return match.Value;
        });

        // Handle conditionals: {{#if condition}}...{{/if}}
        result = RenderConditionals(result);

        // Handle loops: {{#each items}}...{{/each}}
        result = RenderLoops(result);

        return result;
    }

    private string RenderConditionals(string template)
    {
        var pattern = @"\{\{#if\s+(\w+)\}\}(.*?)\{\{/if\}\}";
        return Regex.Replace(template, pattern, match =>
        {
            var condition = match.Groups[1].Value;
            var content = match.Groups[2].Value;

            if (_context.TryGetValue(condition, out var value))
            {
                var isTrue = value switch
                {
                    bool b => b,
                    int i => i != 0,
                    string s => !string.IsNullOrEmpty(s),
                    null => false,
                    _ => true
                };

                return isTrue ? content : "";
            }

            return "";
        }, RegexOptions.Singleline);
    }

    private string RenderLoops(string template)
    {
        var pattern = @"\{\{#each\s+(\w+)\}\}(.*?)\{\{/each\}\}";
        return Regex.Replace(template, pattern, match =>
        {
            var collectionName = match.Groups[1].Value;
            var itemTemplate = match.Groups[2].Value;

            if (_context.TryGetValue(collectionName, out var value) && value is System.Collections.IEnumerable enumerable)
            {
                var sb = new StringBuilder();
                foreach (var item in enumerable)
                {
                    var itemResult = itemTemplate;

                    // Replace {{this}} with item
                    itemResult = itemResult.Replace("{{this}}", item?.ToString() ?? "");

                    // Replace {{property}} with item properties
                    if (item != null)
                    {
                        var props = item.GetType().GetProperties();
                        foreach (var prop in props)
                        {
                            var propValue = prop.GetValue(item);
                            itemResult = itemResult.Replace($"{{{{{prop.Name}}}}}", propValue?.ToString() ?? "");
                        }
                    }

                    sb.Append(itemResult);
                }
                return sb.ToString();
            }

            return "";
        }, RegexOptions.Singleline);
    }

    // Load from file
    public static SimpleTemplate FromFile(string path)
    {
        var content = File.ReadAllText(path);
        return new SimpleTemplate(content);
    }

    // Load async
    public static async Task<SimpleTemplate> FromFileAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        return new SimpleTemplate(content);
    }
}

/// <summary>
/// Template with helpers
/// </summary>
public class TemplateWithHelpers : SimpleTemplate
{
    private readonly Dictionary<string, Func<object?, string>> _helpers = new();

    public TemplateWithHelpers(string template) : base(template)
    {
        RegisterDefaultHelpers();
    }

    public TemplateWithHelpers RegisterHelper(string name, Func<object?, string> helper)
    {
        _helpers[name] = helper;
        return this;
    }

    private void RegisterDefaultHelpers()
    {
        // {{upper name}} - uppercase
        _helpers["upper"] = value => value?.ToString()?.ToUpperInvariant() ?? "";

        // {{lower name}} - lowercase
        _helpers["lower"] = value => value?.ToString()?.ToLowerInvariant() ?? "";

        // {{date value}} - format date
        _helpers["date"] = value => value is DateTime dt ? dt.ToString("yyyy-MM-dd") : "";

        // {{json value}} - to JSON
        _helpers["json"] = value => SimpleSerializer.ToJson(value);
    }

    public new string Render()
    {
        var result = base.Render();

        // Apply helpers: {{helper value}}
        foreach (var helper in _helpers)
        {
            var pattern = $"\\{{\\{{{helper.Key}\\s+(\\w+)\\}}\\}}";
            result = Regex.Replace(result, pattern, match =>
            {
                var key = match.Groups[1].Value;
                // Get value from context
                return "";
            });
        }

        return result;
    }
}

/// <summary>
/// Template cache
/// </summary>
public class TemplateCache
{
    private readonly SimpleCache<SimpleTemplate> _cache;
    private readonly string _basePath;

    public TemplateCache(string basePath, int maxSize = 100)
    {
        _basePath = basePath;
        _cache = new SimpleCache<SimpleTemplate>(TimeSpan.FromMinutes(maxSize));
    }

    public async Task<SimpleTemplate> GetAsync(string name)
    {
        var cached = _cache.Get(name);
        if (cached != null) return cached;

        var path = Path.Combine(_basePath, name);
        var template = await SimpleTemplate.FromFileAsync(path);

        _cache.Set(name, template, TimeSpan.FromMinutes(10));
        return template;
    }

    public void Clear() => _cache.Clear();
}

/// <summary>
/// View model for templates
/// </summary>
public class ViewModel
{
    private readonly Dictionary<string, object?> _data = new();

    public ViewModel Set(string key, object? value)
    {
        _data[key] = value;
        return this;
    }

    public Dictionary<string, object?> ToDictionary() => _data;
}

/// <summary>
/// HTML template builder
/// </summary>
public class HtmlTemplate
{
    private readonly StringBuilder _html = new();

    public HtmlTemplate Element(string tag, string content, Dictionary<string, string>? attributes = null)
    {
        _html.Append($"<{tag}");

        if (attributes != null)
        {
            foreach (var attr in attributes)
            {
                _html.Append($" {attr.Key}=\"{EscapeHtml(attr.Value)}\"");
            }
        }

        _html.Append($">{EscapeHtml(content)}</{tag}>");
        return this;
    }

    public HtmlTemplate Div(string content, string? className = null)
    {
        var attrs = className != null ? new Dictionary<string, string> { ["class"] = className } : null;
        return Element("div", content, attrs);
    }

    public HtmlTemplate H1(string content) => Element("h1", content);
    public HtmlTemplate H2(string content) => Element("h2", content);
    public HtmlTemplate P(string content) => Element("p", content);

    public HtmlTemplate Link(string text, string href)
    {
        return Element("a", text, new Dictionary<string, string> { ["href"] = href });
    }

    public HtmlTemplate Image(string src, string alt)
    {
        _html.Append($"<img src=\"{EscapeHtml(src)}\" alt=\"{EscapeHtml(alt)}\" />");
        return this;
    }

    public HtmlTemplate Raw(string html)
    {
        _html.Append(html);
        return this;
    }

    public override string ToString() => _html.ToString();

    private string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}

/// <summary>
/// Example templates
/// </summary>
public class TemplateExamples
{
    public const string UserProfileTemplate = @"
<!DOCTYPE html>
<html>
<head>
    <title>{{name}}'s Profile</title>
</head>
<body>
    <h1>Welcome, {{name}}!</h1>
    <p>Email: {{email}}</p>

    {{#if isPremium}}
    <div class=""premium-badge"">Premium Member</div>
    {{/if}}

    <h2>Recent Posts</h2>
    <ul>
    {{#each posts}}
        <li>{{title}} - {{date}}</li>
    {{/each}}
    </ul>
</body>
</html>";

    public const string EmailTemplate = @"
Hello {{name}},

Thank you for your order #{{orderId}}.

Items:
{{#each items}}
- {{name}}: ${{price}}
{{/each}}

Total: ${{total}}

Best regards,
{{companyName}}";

    public static void Examples()
    {
        // Simple variable replacement
        var template = new SimpleTemplate("Hello {{name}}, you have {{count}} messages");
        template.Set("name", "John");
        template.Set("count", 5);
        var result = template.Render();
        Console.WriteLine(result);

        // Conditionals
        var template2 = new SimpleTemplate(@"
            {{#if isPremium}}
            Welcome Premium Member!
            {{/if}}
        ");
        template2.Set("isPremium", true);
        var result2 = template2.Render();

        // Loops
        var template3 = new SimpleTemplate(@"
            {{#each users}}
            - {{name}} ({{email}})
            {{/each}}
        ");
        var users = new[]
        {
            new { name = "Alice", email = "alice@example.com" },
            new { name = "Bob", email = "bob@example.com" }
        };
        template3.Set("users", users);
        var result3 = template3.Render();

        // HTML builder
        var html = new HtmlTemplate()
            .H1("My Page")
            .P("Welcome to my website")
            .Div("This is a div", "container")
            .Link("Click here", "https://example.com")
            .Image("/logo.png", "Logo");

        Console.WriteLine(html.ToString());

        // From file
        // var fileTemplate = SimpleTemplate.FromFile("templates/email.txt");
        // fileTemplate.Set("name", "John");
        // var email = fileTemplate.Render();
    }
}

/// <summary>
/// Markdown to HTML converter (simple)
/// </summary>
public class SimpleMarkdown
{
    public static string ToHtml(string markdown)
    {
        var html = markdown;

        // Headers
        html = Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);

        // Bold
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        // Italic
        html = Regex.Replace(html, @"\*(.+?)\*", "<em>$1</em>");

        // Links
        html = Regex.Replace(html, @"\[(.+?)\]\((.+?)\)", "<a href=\"$2\">$1</a>");

        // Code
        html = Regex.Replace(html, @"`(.+?)`", "<code>$1</code>");

        // Line breaks
        html = Regex.Replace(html, @"\n\n", "</p><p>");
        html = "<p>" + html + "</p>";

        return html;
    }
}