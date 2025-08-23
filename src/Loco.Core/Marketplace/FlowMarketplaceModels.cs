using System;
using System.Collections.Generic;
using Loco.Core.Models;

namespace Loco.Core.Marketplace;

/// <summary>
/// Marketplace flow with metadata
/// </summary>
public class MarketplaceFlow
{
    public string Id { get; set; }
    public FlowDefinition FlowDefinition { get; set; }
    public MarketplaceMetadata Metadata { get; set; }
    public string Version { get; set; }
    public DateTime UploadedAt { get; set; }
    public int Downloads { get; set; }
}

/// <summary>
/// Marketplace metadata
/// </summary>
public class MarketplaceMetadata
{
    public string Author { get; set; }
    public string AuthorUrl { get; set; }
    public MarketplaceCategory Category { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string License { get; set; } = "MIT";
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public int Downloads { get; set; }
    public int InstallCount { get; set; }
    public DateTime? LastInstalled { get; set; }
    public Dictionary<string, object> Extra { get; set; } = new();
}

/// <summary>
/// Marketplace categories
/// </summary>
public enum MarketplaceCategory
{
    Productivity,
    Development,
    SmartHome,
    Gaming,
    Media,
    System,
    Network,
    Security,
    Business,
    Education,
    Entertainment,
    Other
}

/// <summary>
/// Flow pack (bundle)
/// </summary>
public class FlowPack
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public List<string> Flows { get; set; } = new();
    public MarketplaceCategory Category { get; set; }
    public string Author { get; set; }
    public double Price { get; set; } // 0 for free
}

/// <summary>
/// Install result
/// </summary>
public class InstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string LocalPath { get; set; }
    public FlowDefinition Flow { get; set; }
}

/// <summary>
/// Share result
/// </summary>
public class ShareResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string ShareUrl { get; set; }
    public string FlowId { get; set; }
    public string QrCode { get; set; }
}

/// <summary>
/// Share info from server
/// </summary>
public class ShareInfo
{
    public string FlowId { get; set; }
    public string ShareUrl { get; set; }
    public string ShortUrl { get; set; }
}

/// <summary>
/// Pack install result
/// </summary>
public class PackInstallResult
{
    public string PackId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<string> InstalledFlows { get; set; } = new();
    public List<string> FailedFlows { get; set; } = new();
}
