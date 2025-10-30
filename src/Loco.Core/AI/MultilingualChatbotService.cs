using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// 多言語AIチャットボットサービス
/// 2025年トレンド: ポリグロットAIチャットボット、リアルタイム翻訳、文化的ニュアンス対応
/// </summary>
public class MultilingualChatbotService
{
    private readonly ILlmService _llmService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<MultilingualChatbotService> _logger;

    public MultilingualChatbotService(
        ILlmService llmService,
        ITranslationService translationService,
        ILogger<MultilingualChatbotService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 多言語チャットボットセッションを開始
    /// </summary>
    public async Task<ChatbotSession> StartSessionAsync(string userLanguage, CancellationToken cancellationToken = default)
    {
        var languageInfo = await _translationService.GetLanguageInfoAsync(userLanguage, cancellationToken);
        if (languageInfo == null)
        {
            _logger.LogWarning("Unsupported language: {UserLanguage}", userLanguage);
            languageInfo = await _translationService.GetLanguageInfoAsync("en", cancellationToken);
        }

        var session = new ChatbotSession
        {
            SessionId = Guid.NewGuid(),
            UserLanguage = userLanguage,
            LanguageInfo = languageInfo,
            ConversationHistory = new List<ChatMessage>(),
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Started multilingual chatbot session: {SessionId} for language: {UserLanguage}", session.SessionId, userLanguage);
        return session;
    }

    /// <summary>
    /// ユーザーメッセージを処理し、文化的適応を考慮した応答を生成
    /// </summary>
    public async Task<ChatbotResponse> ProcessMessageAsync(
        ChatbotSession session,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ユーザーメッセージを英語に翻訳（内部処理用）
            var translatedMessage = await _translationService.TranslateAsync(userMessage, "en", session.UserLanguage, cancellationToken);

            // コンテキストと文化を考慮した応答生成
            var contextPrompt = BuildContextPrompt(session, translatedMessage);

            var response = await _llmService.CompleteAsync(contextPrompt, new LlmOptions
            {
                Temperature = 0.7f,
                MaxTokens = 500
            }, cancellationToken);

            if (!response.Success || string.IsNullOrEmpty(response.Text))
            {
                return new ChatbotResponse
                {
                    Response = GetLocalizedErrorMessage(session.UserLanguage),
                    IsError = true
                };
            }

            // 応答をユーザーの言語に翻訳
            var localizedResponse = await _translationService.TranslateWithCulturalAdaptationAsync(
                response.Text, session.UserLanguage, "en", cancellationToken);

            // 文化的ニュアンスを追加
            var culturallyAdaptedResponse = await AddCulturalAdaptationAsync(
                localizedResponse, session.LanguageInfo, cancellationToken);

            // セッション履歴を更新
            session.ConversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                Language = session.UserLanguage,
                Timestamp = DateTime.UtcNow
            });

            session.ConversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = culturallyAdaptedResponse,
                Language = session.UserLanguage,
                Timestamp = DateTime.UtcNow
            });

            return new ChatbotResponse
            {
                Response = culturallyAdaptedResponse,
                OriginalResponse = response.Text,
                Confidence = CalculateConfidence(response),
                CulturalAdaptations = ExtractCulturalAdaptations(session.LanguageInfo),
                IsError = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chatbot message for session: {SessionId}", session.SessionId);
            return new ChatbotResponse
            {
                Response = GetLocalizedErrorMessage(session.UserLanguage),
                IsError = true
            };
        }
    }

    private string BuildContextPrompt(ChatbotSession session, string translatedMessage)
    {
        var cultureContext = BuildCultureContext(session.LanguageInfo);
        var conversationContext = BuildConversationContext(session);

        return $"You are a helpful AI assistant for Loco automation platform. Respond to the user's query in a culturally appropriate manner.\n\n" +
               $"User Language: {session.LanguageInfo.NativeName} ({session.LanguageInfo.Region})\n" +
               $"Cultural Context: {cultureContext}\n" +
               $"Business Etiquette: {string.Join(", ", session.LanguageInfo.BusinessEtiquette)}\n" +
               $"Communication Style: {string.Join(", ", session.LanguageInfo.CulturalNuances)}\n\n" +
               $"Recent Conversation:\n{conversationContext}\n\n" +
               $"Current Query: {translatedMessage}\n\n" +
               $"Instructions:\n" +
               $"- Respond naturally and conversationally\n" +
               $"- Consider cultural sensitivities and business practices\n" +
               $"- Use appropriate formality level for {session.LanguageInfo.NativeName} culture\n" +
               $"- If discussing automation or technical topics, relate them to Loco platform features\n" +
               $"- Keep responses helpful, accurate, and culturally adapted";
    }

    private string BuildCultureContext(LanguageInfo? languageInfo)
    {
        if (languageInfo == null) return "General Western business culture";

        return $"Calendar: {languageInfo.CalendarType}, " +
               $"Business style: {string.Join(", ", languageInfo.BusinessEtiquette)}, " +
               $"Cultural values: {string.Join(", ", languageInfo.CulturalNuances)}, " +
               $"Region: {string.Join(", ", languageInfo.BusinessRegions)}";
    }

    private string BuildConversationContext(ChatbotSession session)
    {
        return string.Join("\n",
            session.ConversationHistory
                .TakeLast(4) // 直近4メッセージのみ
                .Select(msg => $"{msg.Role}: {msg.Content}"));
    }

    private async Task<string> AddCulturalAdaptationAsync(
        string response,
        LanguageInfo? languageInfo,
        CancellationToken cancellationToken)
    {
        if (languageInfo == null) return response;

        // 文化的適応のための追加プロンプト
        var adaptationPrompt = $"Adapt this response for {languageInfo.NativeName} culture:\n\n" +
                              $"Original: {response}\n\n" +
                              $"Cultural considerations:\n" +
                              $"- Communication style: {string.Join(", ", languageInfo.CulturalNuances)}\n" +
                              $"- Business etiquette: {string.Join(", ", languageInfo.BusinessEtiquette)}\n" +
                              $"- Formality level: {GetFormalityLevel(languageInfo)}\n\n" +
                              $"Provide only the culturally adapted response:";

        var adaptationResponse = await _llmService.CompleteAsync(adaptationPrompt, new LlmOptions
        {
            Temperature = 0.3f,
            MaxTokens = response.Length + 100
        }, cancellationToken);

        return adaptationResponse.Success && !string.IsNullOrEmpty(adaptationResponse.Text)
            ? adaptationResponse.Text.Trim()
            : response;
    }

    private string GetFormalityLevel(LanguageInfo languageInfo)
    {
        return languageInfo.BusinessEtiquette.Contains("Formal") ? "High formality" : "Moderate formality";
    }

    private double CalculateConfidence(LlmResponse response)
    {
        // 簡易的な信頼度計算（実際にはより洗練された方法を使用）
        if (response.Text.Length < 10) return 0.3;
        if (response.Text.Length > 1000) return 0.8;
        return 0.7; // デフォルト
    }

    private List<string> ExtractCulturalAdaptations(LanguageInfo? languageInfo)
    {
        if (languageInfo == null) return new List<string>();

        var adaptations = new List<string>();
        if (languageInfo.IsRTL) adaptations.Add("Right-to-left text layout");
        if (languageInfo.CalendarType != "Gregorian") adaptations.Add($"Uses {languageInfo.CalendarType} calendar");
        if (languageInfo.BusinessEtiquette.Any()) adaptations.Add($"Business etiquette: {string.Join(", ", languageInfo.BusinessEtiquette)}");
        if (languageInfo.CulturalNuances.Any()) adaptations.Add($"Cultural nuances: {string.Join(", ", languageInfo.CulturalNuances)}");

        return adaptations;
    }

    private string GetLocalizedErrorMessage(string language)
    {
        return language switch
        {
            "ja" => "申し訳ありませんが、応答を生成できませんでした。しばらく経ってから再試行してください。",
            "es" => "Lo siento, no pude generar una respuesta. Por favor, inténtalo de nuevo en unos momentos.",
            "de" => "Entschuldigung, ich konnte keine Antwort generieren. Bitte versuchen Sie es in einigen Augenblicken erneut.",
            "fr" => "Désolé, je n'ai pas pu générer de réponse. Veuillez réessayer dans quelques instants.",
            "zh" => "抱歉，我无法生成回复。请稍后重试。",
            "ko" => "죄송합니다. 응답을 생성할 수 없습니다. 잠시 후 다시 시도해 주세요.",
            "ar" => "عذراً، لم أتمكن من إنشاء رد. يرجى المحاولة مرة أخرى خلال لحظات.",
            _ => "I'm sorry, I couldn't generate a response. Please try again in a few moments."
        };
    }
}

/// <summary>
/// チャットボットセッション
/// </summary>
public class ChatbotSession
{
    public Guid SessionId { get; set; }
    public string UserLanguage { get; set; } = string.Empty;
    public LanguageInfo? LanguageInfo { get; set; }
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// チャットメッセージ
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// チャットボット応答
/// </summary>
public class ChatbotResponse
{
    public string Response { get; set; } = string.Empty;
    public string OriginalResponse { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> CulturalAdaptations { get; set; } = new();
    public bool IsError { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
