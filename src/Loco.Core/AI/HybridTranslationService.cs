using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// ハイブリッド翻訳サービス（機械翻訳 + 人間翻訳）
/// 2025年トレンド: 機械+人間翻訳の標準化
/// </summary>
public class HybridTranslationService
{
    private readonly ILlmService _llmService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<HybridTranslationService> _logger;

    public HybridTranslationService(
        ILlmService llmService,
        ITranslationService translationService,
        ILogger<HybridTranslationService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
/// ハイブリッド翻訳を実行
/// </summary>
    public async Task<HybridTranslationResult> TranslateHybridAsync(
        string text,
        string targetLanguage,
        TranslationMode mode = TranslationMode.Auto,
        CancellationToken cancellationToken = default)
    {
        var qualityMetrics = await _translationService.EvaluateTranslationQualityAsync(text, text, targetLanguage, cancellationToken);

        if (mode == TranslationMode.Auto)
        {
            mode = DetermineOptimalMode(text, targetLanguage, qualityMetrics);
        }

        return mode switch
        {
            TranslationMode.MachineOnly => await TranslateMachineOnlyAsync(text, targetLanguage, cancellationToken),
            TranslationMode.HumanPreferred => await TranslateHumanPreferredAsync(text, targetLanguage, cancellationToken),
            TranslationMode.Hybrid => await TranslateHybridAsync(text, targetLanguage, cancellationToken),
            _ => throw new ArgumentException($"Unsupported translation mode: {mode}")
        };
    }

    private TranslationMode DetermineOptimalMode(string text, string targetLanguage, TranslationQualityMetrics metrics)
    {
        // 品質メトリクスに基づいて最適モードを決定
        if (metrics.OverallScore >= 4.0)
            return TranslationMode.MachineOnly;

        if (text.Length < 100 && IsTechnicalContent(text))
            return TranslationMode.MachineOnly;

        if (IsCreativeContent(text) || IsLegalContent(text))
            return TranslationMode.HumanPreferred;

        return TranslationMode.Hybrid;
    }

    private async Task<HybridTranslationResult> TranslateMachineOnlyAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        var machineTranslation = await _translationService.TranslateWithCulturalAdaptationAsync(text, targetLanguage, "auto", cancellationToken);

        return new HybridTranslationResult
        {
            FinalTranslation = machineTranslation,
            MachineTranslation = machineTranslation,
            HumanTranslation = null,
            Mode = TranslationMode.MachineOnly,
            Confidence = 0.9,
            ProcessingTime = DateTime.UtcNow
        };
    }

    private async Task<HybridTranslationResult> TranslateHumanPreferredAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        // 人間翻訳をシミュレート（実際には外部サービス連携）
        var humanTranslation = await SimulateHumanTranslationAsync(text, targetLanguage, cancellationToken);

        return new HybridTranslationResult
        {
            FinalTranslation = humanTranslation,
            MachineTranslation = await _translationService.TranslateAsync(text, targetLanguage, "auto", cancellationToken),
            HumanTranslation = humanTranslation,
            Mode = TranslationMode.HumanPreferred,
            Confidence = 0.95,
            ProcessingTime = DateTime.UtcNow
        };
    }

    private async Task<HybridTranslationResult> TranslateHybridAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        // 機械翻訳を実行
        var machineTranslation = await _translationService.TranslateWithCulturalAdaptationAsync(text, targetLanguage, "auto", cancellationToken);

        // 品質評価
        var qualityMetrics = await _translationService.EvaluateTranslationQualityAsync(text, machineTranslation, targetLanguage, cancellationToken);

        string finalTranslation;
        if (qualityMetrics.IsAcceptable)
        {
            finalTranslation = machineTranslation;
        }
        else
        {
            // 人間による修正が必要な場合
            var humanCorrection = await RequestHumanCorrectionAsync(text, machineTranslation, targetLanguage, cancellationToken);
            finalTranslation = humanCorrection;
        }

        return new HybridTranslationResult
        {
            FinalTranslation = finalTranslation,
            MachineTranslation = machineTranslation,
            HumanTranslation = qualityMetrics.IsAcceptable ? null : finalTranslation,
            Mode = TranslationMode.Hybrid,
            Confidence = qualityMetrics.OverallScore,
            QualityMetrics = qualityMetrics,
            ProcessingTime = DateTime.UtcNow
        };
    }

    private async Task<string> SimulateHumanTranslationAsync(string text, string targetLanguage, CancellationToken cancellationToken)
    {
        // シミュレーション: 高品質な人間翻訳を模倣
        var prompt = $"You are a professional human translator. Translate the following text to {targetLanguage} " +
                    $"with perfect accuracy, cultural adaptation, and natural fluency:\n\n{text}\n\n" +
                    $"Provide only the translation:";

        var response = await _llmService.CompleteAsync(prompt, new LlmOptions
        {
            Temperature = 0.1f, // 低温度で正確性を重視
            MaxTokens = text.Length * 2
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : await _translationService.TranslateAsync(text, targetLanguage, "auto", cancellationToken);
    }

    private async Task<string> RequestHumanCorrectionAsync(string original, string machineTranslation, string targetLanguage, CancellationToken cancellationToken)
    {
        // 人間による修正をシミュレート
        var correctionPrompt = $"As an expert translator, review and improve this machine translation:\n\n" +
                              $"Original ({GetLanguageName("auto")}): {original}\n" +
                              $"Machine Translation ({targetLanguage}): {machineTranslation}\n\n" +
                              $"Provide the corrected translation only:";

        var response = await _llmService.CompleteAsync(correctionPrompt, new LlmOptions
        {
            Temperature = 0.2f,
            MaxTokens = machineTranslation.Length + 100
        }, cancellationToken);

        return response.Success && !string.IsNullOrEmpty(response.Text)
            ? response.Text.Trim()
            : machineTranslation;
    }

    private bool IsTechnicalContent(string text)
    {
        var technicalKeywords = new[] { "API", "SDK", "HTTP", "JSON", "XML", "database", "server", "client" };
        return technicalKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCreativeContent(string text)
    {
        var creativeKeywords = new[] { "story", "poem", "creative", "marketing", "brand", "advertising" };
        return creativeKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLegalContent(string text)
    {
        var legalKeywords = new[] { "legal", "contract", "terms", "agreement", "privacy", "policy", "compliance" };
        return legalKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private string GetLanguageName(string code)
    {
        return code switch
        {
            "auto" => "Auto-detected",
            "en" => "English",
            "ja" => "Japanese",
            "zh" => "Chinese",
            "es" => "Spanish",
            "de" => "German",
            "fr" => "French",
            "ar" => "Arabic",
            "ko" => "Korean",
            _ => code
        };
    }
}

/// <summary>
/// 翻訳モード
/// </summary>
public enum TranslationMode
{
    Auto,        // 自動決定
    MachineOnly, // 機械翻訳のみ
    HumanPreferred, // 人間翻訳優先
    Hybrid       // ハイブリッド
}

/// <summary>
/// ハイブリッド翻訳結果
/// </summary>
public class HybridTranslationResult
{
    public string FinalTranslation { get; set; } = string.Empty;
    public string MachineTranslation { get; set; } = string.Empty;
    public string? HumanTranslation { get; set; }
    public TranslationMode Mode { get; set; }
    public double Confidence { get; set; }
    public TranslationQualityMetrics? QualityMetrics { get; set; }
    public DateTime ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
