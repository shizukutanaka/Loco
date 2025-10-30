using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// 言語アクセシビリティサービス
/// 2025年トレンド: 言語アクセシビリティ法対応、インクルーシブデザイン
/// </summary>
public class LanguageAccessibilityService
{
    private readonly ILlmService _llmService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<LanguageAccessibilityService> _logger;

    public LanguageAccessibilityService(
        ILlmService llmService,
        ITranslationService translationService,
        ILogger<LanguageAccessibilityService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
/// アクセシビリティ対応翻訳を実行
/// </summary>
    public async Task<AccessibilityTranslation> TranslateForAccessibilityAsync(
        string text,
        string targetLanguage,
        AccessibilityRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var baseTranslation = await _translationService.TranslateWithCulturalAdaptationAsync(text, targetLanguage, "auto", cancellationToken);

        var accessibilityTranslation = new AccessibilityTranslation
        {
            OriginalText = text,
            BaseTranslation = baseTranslation,
            TargetLanguage = targetLanguage,
            Requirements = requirements
        };

        // アクセシビリティ要件に基づいて翻訳を調整
        if (requirements.UseSimpleLanguage)
        {
            accessibilityTranslation.SimplifiedTranslation = await SimplifyLanguageAsync(baseTranslation, targetLanguage, cancellationToken);
        }

        if (requirements.IncludeAlternativeText)
        {
            accessibilityTranslation.AlternativeDescriptions = await GenerateAlternativeDescriptionsAsync(text, targetLanguage, cancellationToken);
        }

        if (requirements.SupportScreenReader)
        {
            accessibilityTranslation.ScreenReaderOptimized = await OptimizeForScreenReaderAsync(baseTranslation, targetLanguage, cancellationToken);
        }

        if (requirements.IncludeCulturalContext)
        {
            accessibilityTranslation.CulturalContext = await AddCulturalContextAsync(baseTranslation, targetLanguage, cancellationToken);
        }

        accessibilityTranslation.IsCompliant = await ValidateComplianceAsync(accessibilityTranslation, requirements, cancellationToken);
        accessibilityTranslation.ValidationReport = await GenerateValidationReportAsync(accessibilityTranslation, requirements, cancellationToken);

        _logger.LogInformation("Generated accessibility translation for language: {TargetLanguage}", targetLanguage);
        return accessibilityTranslation;
    }

    /// <summary>
/// 言語アクセシビリティ評価を実行
/// </summary>
    public async Task<AccessibilityScore> EvaluateLanguageAccessibilityAsync(
        string text,
        string language,
        CancellationToken cancellationToken = default)
    {
        var languageInfo = await _translationService.GetLanguageInfoAsync(language, cancellationToken);
        if (languageInfo == null)
        {
            return new AccessibilityScore { OverallScore = 5.0, IsAccessible = true };
        }

        var prompt = $"Evaluate the language accessibility of this text for {languageInfo.NativeName} speakers:\n\n" +
                    $"Text: {text}\n\n" +
                    $"Evaluate the following aspects (1-5 scale):\n" +
                    $"- Readability and clarity\n" +
                    $"- Cultural appropriateness\n" +
                    $"- Inclusivity and sensitivity\n" +
                    $"- Technical accessibility (screen readers, etc.)\n" +
                    $"- Compliance with accessibility standards\n\n" +
                    $"Provide scores in JSON format: {{\"readability\": 0, \"culturalAppropriateness\": 0, \"inclusivity\": 0, \"technicalAccessibility\": 0, \"compliance\": 0}}";

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
                var readability = scores?.GetValueOrDefault("readability", 3.0) ?? 3.0;
                var culturalAppropriateness = scores?.GetValueOrDefault("culturalAppropriateness", 3.0) ?? 3.0;
                var inclusivity = scores?.GetValueOrDefault("inclusivity", 3.0) ?? 3.0;
                var technicalAccessibility = scores?.GetValueOrDefault("technicalAccessibility", 3.0) ?? 3.0;
                var compliance = scores?.GetValueOrDefault("compliance", 3.0) ?? 3.0;

                var overallScore = (readability + culturalAppropriateness + inclusivity + technicalAccessibility + compliance) / 5.0;

                return new AccessibilityScore
                {
                    OverallScore = overallScore,
                    Readability = readability,
                    CulturalAppropriateness = culturalAppropriateness,
                    Inclusivity = inclusivity,
                    TechnicalAccessibility = technicalAccessibility,
                    Compliance = compliance,
                    IsAccessible = overallScore >= 3.5,
                    Recommendations = await GenerateAccessibilityRecommendationsAsync(text, languageInfo, overallScore < 3.5, cancellationToken)
                };
            }
            catch
            {
                return new AccessibilityScore { OverallScore = 3.0, IsAccessible = true };
            }
        }

        return new AccessibilityScore { OverallScore = 3.0, IsAccessible = true };
    }

    /// <summary>
/// 多言語アクセシビリティレポートを生成
/// </summary>
    public async Task<MultilingualAccessibilityReport> GenerateMultilingualAccessibilityReportAsync(
        Dictionary<string, string> texts,
        List<string> targetLanguages,
        AccessibilityRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var report = new MultilingualAccessibilityReport
        {
            GeneratedAt = DateTime.UtcNow,
            TargetLanguages = targetLanguages,
            Requirements = requirements,
            LanguageReports = new Dictionary<string, AccessibilityReport>()
        };

        foreach (var language in targetLanguages)
        {
            var languageReport = new AccessibilityReport
            {
                Language = language,
                IndividualTranslations = new List<AccessibilityTranslation>(),
                AccessibilityScore = new AccessibilityScore()
            };

            foreach (var (key, text) in texts)
            {
                var translation = await TranslateForAccessibilityAsync(text, language, requirements, cancellationToken);
                languageReport.IndividualTranslations.Add(translation);

                var accessibilityScore = await EvaluateLanguageAccessibilityAsync(translation.FinalTranslation, language, cancellationToken);
                languageReport.AccessibilityScore = accessibilityScore;
            }

            report.LanguageReports[language] = languageReport;
        }

        report.OverallCompliance = report.LanguageReports.All(lr => lr.Value.AccessibilityScore.IsAccessible);
        report.Recommendations = await GenerateOverallRecommendationsAsync(report, cancellationToken);

        return report;
    }

    private async Task<string> SimplifyLanguageAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var prompt = $"Simplify this text for better accessibility in {targetLanguage}. Use shorter sentences, common words, and clear structure:\n\n" +
                    $"Original: {text}\n\n" +
                    $"Provide only the simplified version:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.2f,
            MaxTokens = text.Length + 50
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : text;
    }

    private async Task<List<string>> GenerateAlternativeDescriptionsAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var prompt = $"Generate alternative descriptions and explanations for this text in {targetLanguage} to improve accessibility:\n\n" +
                    $"Text: {text}\n\n" +
                    $"Provide 2-3 alternative ways to express the same meaning:";

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

        return new List<string> { text };
    }

    private async Task<string> OptimizeForScreenReaderAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var prompt = $"Optimize this text for screen reader accessibility in {targetLanguage}. " +
                    $"Add proper punctuation, avoid abbreviations, and ensure clear pronunciation:\n\n" +
                    $"Original: {text}\n\n" +
                    $"Provide only the screen reader optimized version:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.2f,
            MaxTokens = text.Length + 30
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : text;
    }

    private async Task<string> AddCulturalContextAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var languageInfo = await _translationService.GetLanguageInfoAsync(targetLanguage, cancellationToken);
        if (languageInfo == null) return text;

        var prompt = $"Add appropriate cultural context to this text for {languageInfo.NativeName} audience. " +
                    $"Consider local customs, business practices, and cultural sensitivities:\n\n" +
                    $"Text: {text}\n" +
                    $"Cultural context: {string.Join(", ", languageInfo.CulturalNuances)}\n" +
                    $"Business etiquette: {string.Join(", ", languageInfo.BusinessEtiquette)}\n\n" +
                    $"Provide the culturally contextualized version:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = text.Length + 100
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : text;
    }

    private async Task<bool> ValidateComplianceAsync(
        AccessibilityTranslation translation,
        AccessibilityRequirements requirements,
        CancellationToken cancellationToken)
    {
        var prompt = $"Validate if this translation meets accessibility requirements:\n\n" +
                    $"Original: {translation.OriginalText}\n" +
                    $"Translation: {translation.FinalTranslation}\n" +
                    $"Language: {translation.TargetLanguage}\n" +
                    $"Requirements: Simple language: {requirements.UseSimpleLanguage}, " +
                    $"Alternative text: {requirements.IncludeAlternativeText}, " +
                    $"Screen reader support: {requirements.SupportScreenReader}, " +
                    $"Cultural context: {requirements.IncludeCulturalContext}\n\n" +
                    $"Is this translation compliant? (yes/no)";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.1f,
            MaxTokens = 10
        }, cancellationToken);

        return response.Success && response.Text.Trim().ToLower().StartsWith("yes");
    }

    private async Task<string> GenerateValidationReportAsync(
        AccessibilityTranslation translation,
        AccessibilityRequirements requirements,
        CancellationToken cancellationToken)
    {
        var prompt = $"Generate a validation report for this accessibility translation:\n\n" +
                    $"Requirements met: {string.Join(", ", GetMetRequirements(translation, requirements))}\n" +
                    $"Areas for improvement: {string.Join(", ", GetImprovementAreas(translation, requirements))}\n\n" +
                    $"Provide a brief report:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.2f,
            MaxTokens = 150
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : "Translation validated for accessibility compliance.";
    }

    private async Task<List<string>> GenerateOverallRecommendationsAsync(
        MultilingualAccessibilityReport report,
        CancellationToken cancellationToken)
    {
        var nonCompliantLanguages = report.LanguageReports
            .Where(lr => !lr.Value.AccessibilityScore.IsAccessible)
            .Select(lr => lr.Key)
            .ToList();

        if (nonCompliantLanguages.Count == 0)
        {
            return new List<string> { "All translations meet accessibility requirements." };
        }

        var prompt = $"Generate recommendations for improving accessibility in these languages: {string.Join(", ", nonCompliantLanguages)}\n\n" +
                    $"Common issues found: {string.Join(", ", report.LanguageReports.SelectMany(lr => lr.Value.AccessibilityScore.Recommendations))}\n\n" +
                    $"Provide 3-5 specific recommendations:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = 200
        }, cancellationToken);

        if (response.Success && !string.IsNullOrEmpty(response.Text))
        {
            return response.Text.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }

        return new List<string> { "Review accessibility requirements for non-compliant languages." };
    }

    private List<string> GetMetRequirements(AccessibilityTranslation translation, AccessibilityRequirements requirements)
    {
        var met = new List<string>();
        if (requirements.UseSimpleLanguage && !string.IsNullOrEmpty(translation.SimplifiedTranslation)) met.Add("Simple language");
        if (requirements.IncludeAlternativeText && translation.AlternativeDescriptions.Any()) met.Add("Alternative descriptions");
        if (requirements.SupportScreenReader && !string.IsNullOrEmpty(translation.ScreenReaderOptimized)) met.Add("Screen reader optimization");
        if (requirements.IncludeCulturalContext && !string.IsNullOrEmpty(translation.CulturalContext)) met.Add("Cultural context");
        return met;
    }

    private List<string> GetImprovementAreas(AccessibilityTranslation translation, AccessibilityRequirements requirements)
    {
        var areas = new List<string>();
        if (requirements.UseSimpleLanguage && string.IsNullOrEmpty(translation.SimplifiedTranslation)) areas.Add("Simplify language");
        if (requirements.IncludeAlternativeText && !translation.AlternativeDescriptions.Any()) areas.Add("Add alternative descriptions");
        if (requirements.SupportScreenReader && string.IsNullOrEmpty(translation.ScreenReaderOptimized)) areas.Add("Optimize for screen readers");
        if (requirements.IncludeCulturalContext && string.IsNullOrEmpty(translation.CulturalContext)) areas.Add("Add cultural context");
        return areas;
    }

    private async Task<List<string>> GenerateAccessibilityRecommendationsAsync(
        string text,
        LanguageInfo languageInfo,
        bool needsImprovement,
        CancellationToken cancellationToken)
    {
        if (!needsImprovement)
            return new List<string> { "Text meets accessibility standards." };

        var prompt = $"Suggest accessibility improvements for this text in {languageInfo.NativeName}:\n\n" +
                    $"Text: {text}\n" +
                    $"Cultural context: {string.Join(", ", languageInfo.CulturalNuances)}\n\n" +
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

        return new List<string> { "Consider simplifying language and adding cultural context." };
    }
}

/// <summary>
/// アクセシビリティ要件
/// </summary>
public class AccessibilityRequirements
{
    public bool UseSimpleLanguage { get; set; }
    public bool IncludeAlternativeText { get; set; }
    public bool SupportScreenReader { get; set; }
    public bool IncludeCulturalContext { get; set; }
    public string[] SupportedRegions { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> CustomRequirements { get; set; } = new();
}

/// <summary>
/// アクセシビリティ対応翻訳
/// </summary>
public class AccessibilityTranslation
{
    public string OriginalText { get; set; } = string.Empty;
    public string BaseTranslation { get; set; } = string.Empty;
    public string? SimplifiedTranslation { get; set; }
    public List<string> AlternativeDescriptions { get; set; } = new();
    public string? ScreenReaderOptimized { get; set; }
    public string? CulturalContext { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public string FinalTranslation => GetFinalTranslation();
    public AccessibilityRequirements Requirements { get; set; } = new();
    public bool IsCompliant { get; set; }
    public string ValidationReport { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();

    private string GetFinalTranslation()
    {
        if (!string.IsNullOrEmpty(SimplifiedTranslation)) return SimplifiedTranslation;
        if (!string.IsNullOrEmpty(ScreenReaderOptimized)) return ScreenReaderOptimized;
        return BaseTranslation;
    }
}

/// <summary>
/// アクセシビリティスコア
/// </summary>
public class AccessibilityScore
{
    public double OverallScore { get; set; }
    public double Readability { get; set; }
    public double CulturalAppropriateness { get; set; }
    public double Inclusivity { get; set; }
    public double TechnicalAccessibility { get; set; }
    public double Compliance { get; set; }
    public bool IsAccessible { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 多言語アクセシビリティレポート
/// </summary>
public class MultilingualAccessibilityReport
{
    public DateTime GeneratedAt { get; set; }
    public List<string> TargetLanguages { get; set; } = new();
    public AccessibilityRequirements Requirements { get; set; } = new();
    public Dictionary<string, AccessibilityReport> LanguageReports { get; set; } = new();
    public bool OverallCompliance { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 言語別アクセシビリティレポート
/// </summary>
public class AccessibilityReport
{
    public string Language { get; set; } = string.Empty;
    public List<AccessibilityTranslation> IndividualTranslations { get; set; } = new();
    public AccessibilityScore AccessibilityScore { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
