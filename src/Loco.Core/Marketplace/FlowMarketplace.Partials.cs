using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Marketplace;

public partial class FlowMarketplace
{
    /// <summary>
    /// Get featured flows (online first, fallback to local)
    /// </summary>
    public async Task<List<MarketplaceFlow>> GetFeaturedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try online first
            if (await IsOnlineAsync())
            {
                var response = await _httpClient.GetAsync($"{_marketplaceUrl}/flows/featured", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    return JsonSerializer.Deserialize<List<MarketplaceFlow>>(json) ?? new List<MarketplaceFlow>();
                }
            }

            // Fallback to local featured
            return GetLocalFeatured();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting featured flows");
            return new List<MarketplaceFlow>();
        }
    }

    /// <summary>
    /// Get flow packs (bundles)
    /// </summary>
    public async Task<List<FlowPack>> GetPacksAsync(CancellationToken cancellationToken = default)
    {
        var packs = new List<FlowPack>();

        // Built-in packs
        packs.Add(new FlowPack
        {
            Id = "starter-pack",
            Name = "スターターパック",
            Description = "初心者向けの基本的な自動化セット",
            Icon = "📦",
            Flows = new List<string> { "morning-routine", "file-organizer", "backup-daily" },
            Category = MarketplaceCategory.Productivity
        });

        packs.Add(new FlowPack
        {
            Id = "developer-pack",
            Name = "開発者パック",
            Description = "開発作業を効率化する自動化セット",
            Icon = "💻",
            Flows = new List<string> { "git-auto-commit", "build-notify", "test-runner" },
            Category = MarketplaceCategory.Development
        });

        packs.Add(new FlowPack
        {
            Id = "home-automation",
            Name = "ホームオートメーション",
            Description = "スマートホーム向け自動化セット",
            Icon = "🏠",
            Flows = new List<string> { "lights-schedule", "temperature-control", "security-monitor" },
            Category = MarketplaceCategory.SmartHome
        });

        // Get online packs if available
        if (await IsOnlineAsync())
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_marketplaceUrl}/packs", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var onlinePacks = JsonSerializer.Deserialize<List<FlowPack>>(json);
                    if (onlinePacks != null)
                        packs.AddRange(onlinePacks);
                }
            }
            catch
            {
                // ignore network errors and return built-ins
            }
        }

        return packs;
    }

    /// <summary>
    /// Install flow pack
    /// </summary>
    public async Task<PackInstallResult> InstallPackAsync(string packId, CancellationToken cancellationToken = default)
    {
        var result = new PackInstallResult
        {
            PackId = packId,
            InstalledFlows = new List<string>(),
            FailedFlows = new List<string>()
        };

        try
        {
            var packs = await GetPacksAsync(cancellationToken);
            var pack = packs.FirstOrDefault(p => p.Id == packId);

            if (pack == null)
            {
                result.Success = false;
                result.Message = "Pack not found";
                return result;
            }

            foreach (var flowId in pack.Flows)
            {
                var installResult = await InstallAsync(flowId, cancellationToken);
                if (installResult.Success)
                {
                    result.InstalledFlows.Add(flowId);
                }
                else
                {
                    result.FailedFlows.Add(flowId);
                }
            }

            result.Success = result.FailedFlows.Count == 0;
            result.Message = result.Success
                ? $"Pack installed: {result.InstalledFlows.Count} flows"
                : $"Pack partially installed: {result.InstalledFlows.Count} succeeded, {result.FailedFlows.Count} failed";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing pack");
            result.Success = false;
            result.Message = $"Pack installation failed: {ex.Message}";
            return result;
        }
    }

    private List<MarketplaceFlow> SearchLocal(string query, MarketplaceCategory? category)
    {
        var results = new List<MarketplaceFlow>();
        var flowsDir = Path.Combine(_localRepository, "flows");

        if (!Directory.Exists(flowsDir))
            return results;

        var files = Directory.GetFiles(flowsDir, "*.json");
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var flow = JsonSerializer.Deserialize<MarketplaceFlow>(json);

                if (flow != null && MatchesSearch(flow, query, category))
                {
                    results.Add(flow);
                }
            }
            catch { }
        }

        return results;
    }

    private async Task<List<MarketplaceFlow>> SearchOnlineAsync(string query, MarketplaceCategory? category, CancellationToken cancellationToken)
    {
        var url = $"{_marketplaceUrl}/flows/search?q={Uri.EscapeDataString(query)}";
        if (category.HasValue)
            url += $"&category={category.Value}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new List<MarketplaceFlow>();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<MarketplaceFlow>>(json) ?? new List<MarketplaceFlow>();
    }

    private bool MatchesSearch(MarketplaceFlow flow, string query, MarketplaceCategory? category)
    {
        if (category.HasValue && flow.Metadata.Category != category.Value)
            return false;

        if (string.IsNullOrWhiteSpace(query))
            return true;

        var searchTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchText = $"{flow.FlowDefinition.Name} {flow.FlowDefinition.Description} {string.Join(" ", flow.Metadata.Tags)}".ToLower();

        return searchTerms.All(term => searchText.Contains(term));
    }

    private bool ValidateFlow(MarketplaceFlow flow)
    {
        // Basic validation
        if (flow?.FlowDefinition == null)
            return false;

        if (string.IsNullOrWhiteSpace(flow.FlowDefinition.Name))
            return false;

        if (flow.FlowDefinition.Triggers == null || !flow.FlowDefinition.Triggers.Any())
            return false;

        if (flow.FlowDefinition.Actions == null || !flow.FlowDefinition.Actions.Any())
            return false;

        // Security validation - no dangerous operations
        foreach (var action in flow.FlowDefinition.Actions)
        {
            if (action.Type == "shell" || action.Type == "script")
            {
                // Check for dangerous commands
                var command = action.Parameters?.GetValueOrDefault("command")?.ToString()?.ToLower();
                if (command != null)
                {
                    var dangerous = new[] { "format", "del ", "rm ", "shutdown", "reboot", "reg ", "regedit" };
                    if (dangerous.Any(d => command.Contains(d)))
                        return false;
                }
            }
        }

        return true;
    }

    private async Task<FlowDefinition> LoadFlowAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var marketplaceFlow = JsonSerializer.Deserialize<MarketplaceFlow>(json);
        return marketplaceFlow?.FlowDefinition;
    }

    private async Task UpdateMetadataAsync(MarketplaceFlow flow)
    {
        flow.Metadata.InstallCount++;
        flow.Metadata.LastInstalled = DateTime.UtcNow;

        var metadataPath = Path.Combine(_localRepository, "metadata.json");
        var metadata = new Dictionary<string, MarketplaceMetadata>();

        if (File.Exists(metadataPath))
        {
            var json = await File.ReadAllTextAsync(metadataPath);
            metadata = JsonSerializer.Deserialize<Dictionary<string, MarketplaceMetadata>>(json) ?? new();
        }

        metadata[flow.Id] = flow.Metadata;

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata));
    }

    private async Task<bool> IsOnlineAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_marketplaceUrl}/health",
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private List<MarketplaceFlow> GetLocalFeatured()
    {
        // Return pre-defined featured flows for offline mode
        return new List<MarketplaceFlow>
        {
            new MarketplaceFlow
            {
                Id = "morning-routine",
                FlowDefinition = new FlowDefinition
                {
                    Name = "朝のルーティン",
                    Description = "毎朝の自動化タスク"
                },
                Metadata = new MarketplaceMetadata
                {
                    Author = "Loco Team",
                    Category = MarketplaceCategory.Productivity,
                    Tags = new[] { "morning", "routine", "daily" },
                    Rating = 4.8,
                    Downloads = 10000
                }
            }
        };
    }

    private string GenerateQrCode(string url)
    {
        // Simple text representation for now
        // In production, use a QR code library
        return $"QR:{url}";
    }
}
