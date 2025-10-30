using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// クロスカルチャーコミュニケーション支援サービス
/// 2025年トレンド: 文化知能、倫理的考慮、グローバルスタンダード対応
/// </summary>
public class CrossCulturalCommunicationService
{
    private readonly ILlmService _llmService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<CrossCulturalCommunicationService> _logger;

    public CrossCulturalCommunicationService(
        ILlmService llmService,
        ITranslationService translationService,
        ILogger<CrossCulturalCommunicationService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
/// クロスカルチャーコミュニケーションガイドを生成
/// </summary>
    public async Task<CulturalCommunicationGuide> GenerateCommunicationGuideAsync(
        string primaryCulture,
        string targetCulture,
        CommunicationContext context,
        CancellationToken cancellationToken = default)
    {
        var primaryLanguageInfo = await _translationService.GetLanguageInfoAsync(primaryCulture, cancellationToken);
        var targetLanguageInfo = await _translationService.GetLanguageInfoAsync(targetCulture, cancellationToken);

        if (primaryLanguageInfo == null || targetLanguageInfo == null)
        {
            throw new ArgumentException("Unsupported culture codes");
        }

        var guide = new CulturalCommunicationGuide
        {
            PrimaryCulture = primaryLanguageInfo,
            TargetCulture = targetLanguageInfo,
            Context = context,
            GeneratedAt = DateTime.UtcNow
        };

        // コミュニケーションスタイルの違いを分析
        guide.CommunicationDifferences = await AnalyzeCommunicationDifferencesAsync(primaryLanguageInfo, targetLanguageInfo, context, cancellationToken);

        // ベストプラクティスを生成
        guide.BestPractices = await GenerateBestPracticesAsync(primaryLanguageInfo, targetLanguageInfo, context, cancellationToken);

        // 潜在的な問題点を特定
        guide.PotentialIssues = await IdentifyPotentialIssuesAsync(primaryLanguageInfo, targetLanguageInfo, context, cancellationToken);

        // 推奨コミュニケーション戦略
        guide.RecommendedStrategies = await GenerateRecommendedStrategiesAsync(primaryLanguageInfo, targetLanguageInfo, context, cancellationToken);

        _logger.LogInformation("Generated cultural communication guide: {PrimaryCulture} -> {TargetCulture} for {Context}",
            primaryCulture, targetCulture, context);

        return guide;
    }

    /// <summary>
/// メッセージの文化的適合性を評価
/// </summary>
    public async Task<CulturalSensitivityScore> EvaluateCulturalSensitivityAsync(
        string message,
        string targetCulture,
        CommunicationContext context,
        CancellationToken cancellationToken = default)
    {
        var languageInfo = await _translationService.GetLanguageInfoAsync(targetCulture, cancellationToken);
        if (languageInfo == null)
        {
            return new CulturalSensitivityScore { OverallScore = 5.0, IsAppropriate = true }; // デフォルトで適切とみなす
        }

        var prompt = $"Evaluate the cultural sensitivity of this message for {languageInfo.NativeName} culture in a {context} context:\n\n" +
                    $"Message: {message}\n\n" +
                    $"Cultural context: {string.Join(", ", languageInfo.CulturalNuances)}\n" +
                    $"Business etiquette: {string.Join(", ", languageInfo.BusinessEtiquette)}\n" +
                    $"Communication style: {string.Join(", ", languageInfo.CulturalNuances)}\n\n" +
                    $"Rate the following aspects (1-5 scale):\n" +
                    $"- Cultural appropriateness\n" +
                    $"- Communication style match\n" +
                    $"- Business etiquette compliance\n" +
                    $"- Potential for misunderstanding\n\n" +
                    $"Provide scores in JSON format: {{\"culturalAppropriateness\": 0, \"communicationMatch\": 0, \"etiquetteCompliance\": 0, \"misunderstandingRisk\": 0}}";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.1f,
            MaxTokens = 100
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            try
            {
                var scores = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(response.Text);
                var culturalAppropriateness = scores?.GetValueOrDefault("culturalAppropriateness", 3.0) ?? 3.0;
                var communicationMatch = scores?.GetValueOrDefault("communicationMatch", 3.0) ?? 3.0;
                var etiquetteCompliance = scores?.GetValueOrDefault("etiquetteCompliance", 3.0) ?? 3.0;
                var misunderstandingRisk = scores?.GetValueOrDefault("misunderstandingRisk", 3.0) ?? 3.0;

                var overallScore = (culturalAppropriateness + communicationMatch + etiquetteCompliance + (6 - misunderstandingRisk)) / 4.0;

                return new CulturalSensitivityScore
                {
                    OverallScore = overallScore,
                    CulturalAppropriateness = culturalAppropriateness,
                    CommunicationMatch = communicationMatch,
                    EtiquetteCompliance = etiquetteCompliance,
                    MisunderstandingRisk = misunderstandingRisk,
                    IsAppropriate = overallScore >= 3.5,
                    Recommendations = await GenerateRecommendationsAsync(message, languageInfo, overallScore < 3.5, cancellationToken)
                };
            }
            catch
            {
                // デフォルト値
                return new CulturalSensitivityScore { OverallScore = 3.0, IsAppropriate = true };
            }
        }

        return new CulturalSensitivityScore { OverallScore = 3.0, IsAppropriate = true };
    }

    private async Task<List<CommunicationDifference>> AnalyzeCommunicationDifferencesAsync(
        LanguageInfo primary,
        LanguageInfo target,
        CommunicationContext context,
        CancellationToken cancellationToken)
    {
        var differences = new List<CommunicationDifference>();

        // コミュニケーションスタイルの違い
        var styleDiff = await CompareCommunicationStylesAsync(primary, target, cancellationToken);
        differences.Add(styleDiff);

        // ビジネスエチケットの違い
        var etiquetteDiff = await CompareBusinessEtiquetteAsync(primary, target, context, cancellationToken);
        differences.Add(etiquetteDiff);

        // 非言語コミュニケーションの違い
        var nonVerbalDiff = await CompareNonVerbalCommunicationAsync(primary, target, cancellationToken);
        differences.Add(nonVerbalDiff);

        return differences;
    }

    private async Task<List<string>> GenerateBestPracticesAsync(
        LanguageInfo primary,
        LanguageInfo target,
        CommunicationContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $"Generate best practices for communication between {primary.NativeName} and {target.NativeName} cultures in a {context} context:\n\n" +
                    $"Primary culture: {string.Join(", ", primary.CulturalNuances)}\n" +
                    $"Target culture: {string.Join(", ", target.CulturalNuances)}\n" +
                    $"Business etiquette: Primary - {string.Join(", ", primary.BusinessEtiquette)}, Target - {string.Join(", ", target.BusinessEtiquette)}\n\n" +
                    $"Provide 5-7 specific, actionable best practices:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = 300
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            return response.Text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.Length > 10)
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }

        return GetDefaultBestPractices(primary, target);
    }

    private async Task<List<string>> IdentifyPotentialIssuesAsync(
        LanguageInfo primary,
        LanguageInfo target,
        CommunicationContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $"Identify potential communication issues when {primary.NativeName} professionals interact with {target.NativeName} colleagues in {context} context:\n\n" +
                    $"Consider differences in: communication styles, business etiquette, cultural values, and non-verbal cues.";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.4f,
            MaxTokens = 200
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            return response.Text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }

        return GetDefaultPotentialIssues(primary, target);
    }

    private async Task<List<string>> GenerateRecommendedStrategiesAsync(
        LanguageInfo primary,
        LanguageInfo target,
        CommunicationContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $"Recommend communication strategies for effective cross-cultural collaboration between {primary.NativeName} and {target.NativeName} teams in {context} context:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = 250
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            return response.Text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }

        return GetDefaultStrategies(primary, target);
    }

    private async Task<List<string>> GenerateRecommendationsAsync(
        string message,
        LanguageInfo languageInfo,
        bool needsImprovement,
        CancellationToken cancellationToken)
    {
        if (!needsImprovement)
            return new List<string> { "Communication appears culturally appropriate." };

        var prompt = $"Suggest improvements for this message to make it more culturally appropriate for {languageInfo.NativeName} culture:\n\n" +
                    $"Message: {message}\n\n" +
                    $"Provide 2-3 specific recommendations:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = 150
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            return response.Text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }

        return new List<string> { "Consider adapting communication style to local cultural norms." };
    }

    private List<string> GetDefaultBestPractices(LanguageInfo primary, LanguageInfo target)
    {
        return new List<string>
        {
            "Use clear and simple language",
            "Be patient and allow time for responses",
            "Show respect for cultural differences",
            "Use appropriate formality levels",
            "Confirm understanding frequently"
        };
    }

    private List<string> GetDefaultPotentialIssues(LanguageInfo primary, LanguageInfo target)
    {
        return new List<string>
        {
            "Differences in communication directness",
            "Varying expectations of formality",
            "Different approaches to time management",
            "Non-verbal communication misunderstandings"
        };
    }

    private List<string> GetDefaultStrategies(LanguageInfo primary, LanguageInfo target)
    {
        return new List<string>
        {
            "Establish clear communication protocols",
            "Provide cultural awareness training",
            "Use visual aids and demonstrations",
            "Schedule regular check-ins",
            "Encourage open feedback"
        };
    }

    private async Task<CommunicationDifference> CompareCommunicationStylesAsync(LanguageInfo primary, LanguageInfo target, CancellationToken cancellationToken)
    {
        return new CommunicationDifference
        {
            Aspect = "Communication Style",
            PrimaryCulture = string.Join(", ", primary.CulturalNuances),
            TargetCulture = string.Join(", ", target.CulturalNuances),
            Impact = await CalculateImpactAsync(primary.CulturalNuances, target.CulturalNuances, cancellationToken)
        };
    }

    private async Task<CommunicationDifference> CompareBusinessEtiquetteAsync(LanguageInfo primary, LanguageInfo target, CommunicationContext context, CancellationToken cancellationToken)
    {
        return new CommunicationDifference
        {
            Aspect = "Business Etiquette",
            PrimaryCulture = string.Join(", ", primary.BusinessEtiquette),
            TargetCulture = string.Join(", ", target.BusinessEtiquette),
            Impact = await CalculateImpactAsync(primary.BusinessEtiquette, target.BusinessEtiquette, cancellationToken)
        };
    }

    private async Task<CommunicationDifference> CompareNonVerbalCommunicationAsync(LanguageInfo primary, LanguageInfo target, CancellationToken cancellationToken)
    {
        var primaryNonVerbal = primary.IsRTL ? "Right-to-left writing, expressive gestures" : "Left-to-right writing, moderate gestures";
        var targetNonVerbal = target.IsRTL ? "Right-to-left writing, expressive gestures" : "Left-to-right writing, moderate gestures";

        return new CommunicationDifference
        {
            Aspect = "Non-verbal Communication",
            PrimaryCulture = primaryNonVerbal,
            TargetCulture = targetNonVerbal,
            Impact = primary.IsRTL == target.IsRTL ? "Low" : "High"
        };
    }

    private async Task<string> CalculateImpactAsync(string[] primaryTraits, string[] targetTraits, CancellationToken cancellationToken)
    {
        var commonTraits = primaryTraits.Intersect(targetTraits).Count();
        var totalTraits = primaryTraits.Length + targetTraits.Length;

        if (commonTraits >= totalTraits / 2) return "Low";
        if (commonTraits >= totalTraits / 4) return "Medium";
        return "High";
    }
}

/// <summary>
/// コミュニケーションコンテキスト
/// </summary>
public enum CommunicationContext
{
    Business,
    Technical,
    Sales,
    Support,
    Negotiation,
    TeamCollaboration,
    Training
}

/// <summary>
/// 文化的コミュニケーションガイド
/// </summary>
public class CulturalCommunicationGuide
{
    public LanguageInfo PrimaryCulture { get; set; } = new();
    public LanguageInfo TargetCulture { get; set; } = new();
    public CommunicationContext Context { get; set; }
    public List<CommunicationDifference> CommunicationDifferences { get; set; } = new();
    public List<string> BestPractices { get; set; } = new();
    public List<string> PotentialIssues { get; set; } = new();
    public List<string> RecommendedStrategies { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// コミュニケーションの違い
/// </summary>
public class CommunicationDifference
{
    public string Aspect { get; set; } = string.Empty;
    public string PrimaryCulture { get; set; } = string.Empty;
    public string TargetCulture { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty; // Low, Medium, High
}

/// <summary>
/// 文化的適合性スコア
/// </summary>
public class CulturalSensitivityScore
{
    public double OverallScore { get; set; }
    public double CulturalAppropriateness { get; set; }
    public double CommunicationMatch { get; set; }
    public double EtiquetteCompliance { get; set; }
    public double MisunderstandingRisk { get; set; }
    public bool IsAppropriate { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
