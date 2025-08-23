using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Loco.Core.Caching;
using Loco.Core.Models;

namespace Loco.Core.NaturalLanguage;

/// <summary>
/// Optimized natural language processor - pragmatic approach
/// Fast pattern matching without heavy ML dependencies
/// </summary>
public sealed class FastNaturalLanguageProcessor
{
    private readonly FastCache<string, ProcessedIntent> _intentCache;
    private readonly List<PatternMatcher> _patterns;
    private static readonly char[] _separators = { ' ', ',', '.', '!', '?', ';', ':', '\t', '\n', '\r' };
    
    public FastNaturalLanguageProcessor()
    {
        _intentCache = new FastCache<string, ProcessedIntent>(1000, TimeSpan.FromHours(1));
        _patterns = InitializePatterns();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<ProcessedIntent> ProcessAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ProcessedIntent.Empty;
        
        // Normalize input
        var normalized = NormalizeInput(input);
        
        // Check cache
        if (_intentCache.TryGet(normalized, out var cached))
            return cached;
        
        // Process intent
        var result = await Task.Run(() => ProcessInternal(normalized)).ConfigureAwait(false);
        
        // Cache result
        _intentCache.Set(normalized, result);
        
        return result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NormalizeInput(string input)
    {
        return input.ToLowerInvariant().Trim();
    }
    
    private ProcessedIntent ProcessInternal(string input)
    {
        var tokens = Tokenize(input);
        var intent = new ProcessedIntent { OriginalText = input };
        
        // Match patterns
        foreach (var pattern in _patterns)
        {
            if (pattern.TryMatch(input, tokens, out var action))
            {
                intent.IntentType = pattern.IntentType;
                intent.Action = action;
                intent.Confidence = pattern.GetConfidence(input, tokens);
                intent.Parameters = ExtractParameters(input, tokens, pattern);
                break;
            }
        }
        
        if (intent.IntentType == IntentType.Unknown)
        {
            intent.Confidence = 0.0;
        }
        
        return intent;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[] Tokenize(string input)
    {
        return input.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
    }
    
    private static Dictionary<string, object> ExtractParameters(
        string input,
        string[] tokens,
        PatternMatcher pattern)
    {
        var parameters = new Dictionary<string, object>();
        
        // Extract time
        var timeMatch = Regex.Match(input, @"\b(\d{1,2}):(\d{2})(?:\s*(am|pm))?\b", RegexOptions.IgnoreCase);
        if (timeMatch.Success)
        {
            parameters["time"] = timeMatch.Value;
        }
        
        // Extract date
        var datePatterns = new[]
        {
            @"\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
            @"\b(tomorrow|today|yesterday)\b",
            @"\b(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})\b"
        };
        
        foreach (var datePattern in datePatterns)
        {
            var dateMatch = Regex.Match(input, datePattern, RegexOptions.IgnoreCase);
            if (dateMatch.Success)
            {
                parameters["date"] = dateMatch.Value;
                break;
            }
        }
        
        // Extract file paths
        var pathMatch = Regex.Match(input, @"[a-zA-Z]:[\\\/][\w\s\\\/.-]+");
        if (pathMatch.Success)
        {
            parameters["path"] = pathMatch.Value;
        }
        
        // Extract URLs
        var urlMatch = Regex.Match(input, @"https?://[\w\-._~:/?#[\]@!$&'()*+,;=]+");
        if (urlMatch.Success)
        {
            parameters["url"] = urlMatch.Value;
        }
        
        // Extract numbers
        var numbers = Regex.Matches(input, @"\b\d+\b")
            .Cast<Match>()
            .Select(m => int.Parse(m.Value))
            .ToList();
        if (numbers.Any())
        {
            parameters["numbers"] = numbers;
        }
        
        return parameters;
    }
    
    private static List<PatternMatcher> InitializePatterns()
    {
        return new List<PatternMatcher>
        {
            // Timer patterns
            new PatternMatcher(
                IntentType.Timer,
                new[] { "every", "at", "remind", "alarm", "timer" },
                new[] { "morning", "evening", "daily", "hourly" },
                "timer"
            ),
            
            // File operations
            new PatternMatcher(
                IntentType.FileOperation,
                new[] { "copy", "move", "delete", "backup", "sync" },
                new[] { "file", "folder", "directory", "document" },
                "file"
            ),
            
            // Application control
            new PatternMatcher(
                IntentType.ApplicationControl,
                new[] { "open", "close", "launch", "start", "stop", "run" },
                new[] { "app", "application", "program", "chrome", "notepad" },
                "app"
            ),
            
            // System monitoring
            new PatternMatcher(
                IntentType.SystemMonitor,
                new[] { "monitor", "watch", "check", "track" },
                new[] { "cpu", "memory", "disk", "network", "system" },
                "monitor"
            ),
            
            // Notification
            new PatternMatcher(
                IntentType.Notification,
                new[] { "notify", "alert", "tell", "message", "email" },
                new[] { "when", "if", "after" },
                "notify"
            ),
            
            // HTTP/Web
            new PatternMatcher(
                IntentType.HttpRequest,
                new[] { "http", "get", "post", "api", "webhook", "fetch" },
                new[] { "url", "endpoint", "request" },
                "http"
            )
        };
    }
    
    private class PatternMatcher
    {
        public IntentType IntentType { get; }
        private readonly HashSet<string> _primaryKeywords;
        private readonly HashSet<string> _secondaryKeywords;
        private readonly string _actionName;
        
        public PatternMatcher(
            IntentType intentType,
            IEnumerable<string> primaryKeywords,
            IEnumerable<string> secondaryKeywords,
            string actionName)
        {
            IntentType = intentType;
            _primaryKeywords = new HashSet<string>(primaryKeywords, StringComparer.OrdinalIgnoreCase);
            _secondaryKeywords = new HashSet<string>(secondaryKeywords, StringComparer.OrdinalIgnoreCase);
            _actionName = actionName;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMatch(string input, string[] tokens, out string action)
        {
            action = null;
            
            var primaryMatch = tokens.Any(t => _primaryKeywords.Contains(t));
            if (!primaryMatch) return false;
            
            action = _actionName;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetConfidence(string input, string[] tokens)
        {
            var primaryMatches = tokens.Count(t => _primaryKeywords.Contains(t));
            var secondaryMatches = tokens.Count(t => _secondaryKeywords.Contains(t));
            
            var totalKeywords = _primaryKeywords.Count + _secondaryKeywords.Count;
            var totalMatches = primaryMatches * 2 + secondaryMatches; // Primary worth more
            
            return Math.Min(1.0, (double)totalMatches / Math.Max(3, totalKeywords));
        }
    }
}

public sealed class ProcessedIntent
{
    public string OriginalText { get; set; }
    public IntentType IntentType { get; set; } = IntentType.Unknown;
    public string Action { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    public static ProcessedIntent Empty => new();
}

public enum IntentType
{
    Unknown,
    Timer,
    FileOperation,
    ApplicationControl,
    SystemMonitor,
    Notification,
    HttpRequest
}
