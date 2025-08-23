using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Sharing;

/// <summary>
/// Simple share service for easy flow distribution
/// Following Rob Pike's simplicity principle - no external dependencies
/// </summary>
public class SimpleShareService
{
    private readonly ILogger<SimpleShareService> _logger;
    private readonly string _baseUrl;
    
    public SimpleShareService(ILogger<SimpleShareService> logger, string baseUrl = "https://github.com/shizukutanaka/Loco")
    {
        _logger = logger;
        _baseUrl = baseUrl;
    }
    
    /// <summary>
    /// Generate share code for flow
    /// </summary>
    public ShareCode GenerateShareCode(FlowDefinition flow)
    {
        try
        {
            var shareId = GenerateShareId();
            var shortCode = GenerateShortCode();
            
            return new ShareCode
            {
                FlowId = flow.Id,
                FlowName = flow.Name,
                ShareId = shareId,
                ShareUrl = $"{_baseUrl}/install/{shareId}",
                LocoUrl = $"loco://install/{shareId}",
                ShortCode = shortCode,
                QrCodeAscii = GenerateSimpleAsciiQr($"{_baseUrl}/i/{shortCode}"),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating share code");
            throw;
        }
    }
    
    /// <summary>
    /// Generate simple ASCII QR code (simplified representation)
    /// </summary>
    public string GenerateSimpleAsciiQr(string text)
    {
        // Simplified QR code representation for console
        var sb = new StringBuilder();
        sb.AppendLine("┌─────────────────────┐");
        sb.AppendLine("│ ███ █ ███ █ ███ █ │");
        sb.AppendLine("│ █ █ █ █ █ █ █ █ █ │");
        sb.AppendLine("│ ███ █ ███ █ ███ █ │");
        sb.AppendLine("│                   │");
        sb.AppendLine($"│  {text.Substring(0, Math.Min(15, text.Length)).PadRight(15)}  │");
        sb.AppendLine("│                   │");
        sb.AppendLine("│ ███ █ ███ █ ███ █ │");
        sb.AppendLine("│ █ █ █ █ █ █ █ █ █ │");
        sb.AppendLine("│ ███ █ ███ █ ███ █ │");
        sb.AppendLine("└─────────────────────┘");
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate shareable link
    /// </summary>
    public ShareLink GenerateShareLink(FlowDefinition flow, ShareOptions options = null)
    {
        options ??= new ShareOptions();
        
        var shareId = GenerateShareId();
        var baseUrl = options.CustomBaseUrl ?? _baseUrl;
        
        var urlBuilder = new StringBuilder($"{baseUrl}/flow/{shareId}");
        var queryParams = new List<string>();
        
        if (options.IncludeMetadata)
        {
            queryParams.Add($"name={Uri.EscapeDataString(flow.Name)}");
            if (!string.IsNullOrEmpty(flow.Description))
                queryParams.Add($"desc={Uri.EscapeDataString(flow.Description.Substring(0, Math.Min(100, flow.Description.Length)))}");
        }
        
        if (queryParams.Any())
        {
            urlBuilder.Append("?");
            urlBuilder.Append(string.Join("&", queryParams));
        }
        
        var shareUrl = urlBuilder.ToString();
        
        return new ShareLink
        {
            FlowId = flow.Id,
            ShareId = shareId,
            FullUrl = shareUrl,
            ShortUrl = $"{baseUrl}/s/{shareId.Substring(0, 6)}",
            LocoProtocolUrl = $"loco://install/{shareId}",
            MarkdownLink = $"[{flow.Name}]({shareUrl})",
            HtmlLink = $"<a href=\"{shareUrl}\">{flow.Name}</a>",
            ExpiresAt = options.ExpirationDays > 0 
                ? DateTime.UtcNow.AddDays(options.ExpirationDays) 
                : null
        };
    }
    
    /// <summary>
    /// Generate one-click install button HTML
    /// </summary>
    public string GenerateInstallButton(FlowDefinition flow, ButtonStyle style = ButtonStyle.Default)
    {
        var shareId = GenerateShareId();
        var installUrl = $"loco://install/{shareId}";
        
        var buttonStyle = style switch
        {
            ButtonStyle.Primary => "background: #007bff; color: white;",
            ButtonStyle.Success => "background: #28a745; color: white;",
            ButtonStyle.Large => "padding: 12px 24px; font-size: 18px;",
            _ => "background: #6c757d; color: white;"
        };
        
        return $@"
<a href=""{installUrl}"" style=""{buttonStyle} padding: 8px 16px; text-decoration: none; border-radius: 4px; display: inline-block;"">
    Install {flow.Name} with Loco
</a>";
    }
    
    /// <summary>
    /// Export flow as shareable file
    /// </summary>
    public async Task<string> ExportFlowAsync(FlowDefinition flow, string outputPath = null)
    {
        try
        {
            outputPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{flow.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.loco");
            
            var exportData = new FlowExport
            {
                Version = "1.0",
                ExportedAt = DateTime.UtcNow,
                Flow = flow,
                Metadata = new Dictionary<string, object>
                {
                    ["author"] = Environment.UserName,
                    ["machine"] = Environment.MachineName,
                    ["locoVersion"] = "0.0.1"
                }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(outputPath, json);
            
            _logger.LogInformation("Flow exported to {Path}", outputPath);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting flow");
            throw;
        }
    }
    
    /// <summary>
    /// Import flow from file
    /// </summary>
    public async Task<FlowDefinition> ImportFlowAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Flow file not found", filePath);
            
            var json = await File.ReadAllTextAsync(filePath);
            
            // Try to parse as FlowExport first
            try
            {
                var export = System.Text.Json.JsonSerializer.Deserialize<FlowExport>(json);
                if (export?.Flow != null)
                    return export.Flow;
            }
            catch
            {
                // Try as raw FlowDefinition
                var flow = System.Text.Json.JsonSerializer.Deserialize<FlowDefinition>(json);
                if (flow != null)
                    return flow;
            }
            
            throw new InvalidOperationException("Invalid flow file format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing flow from {Path}", filePath);
            throw;
        }
    }
    
    private string GenerateShareId()
    {
        var bytes = new byte[12];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }
    
    private string GenerateShortCode()
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[6];
        var random = new Random();
        
        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }
        
        return $"{new string(code, 0, 3)}-{new string(code, 3, 3)}";
    }
}

/// <summary>
/// Share code information
/// </summary>
public class ShareCode
{
    public string FlowId { get; set; }
    public string FlowName { get; set; }
    public string ShareId { get; set; }
    public string ShareUrl { get; set; }
    public string LocoUrl { get; set; }
    public string ShortCode { get; set; }
    public string QrCodeAscii { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Share link information
/// </summary>
public class ShareLink
{
    public string FlowId { get; set; }
    public string ShareId { get; set; }
    public string FullUrl { get; set; }
    public string ShortUrl { get; set; }
    public string LocoProtocolUrl { get; set; }
    public string MarkdownLink { get; set; }
    public string HtmlLink { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Share options
/// </summary>
public class ShareOptions
{
    public string CustomBaseUrl { get; set; }
    public bool IncludeMetadata { get; set; } = true;
    public int ExpirationDays { get; set; } = 7;
    public bool RequireAuth { get; set; }
}

/// <summary>
/// Button styles
/// </summary>
public enum ButtonStyle
{
    Default,
    Primary,
    Success,
    Large
}

/// <summary>
/// Flow export format
/// </summary>
public class FlowExport
{
    public string Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public FlowDefinition Flow { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
