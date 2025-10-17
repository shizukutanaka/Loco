using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// セキュリティスキャナー
/// 問題: 「セキュリティ脆弱性が放置される」（企業の最大懸念）
/// 解決: 自動スキャンで脆弱性を継続的に検出
/// </summary>
public class SecurityScanner
{
    public enum Severity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }

    public class SecurityIssue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public Severity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleJa { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionJa { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string RecommendationJa { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int? LineNumber { get; set; }
        public string? Code { get; set; }
        public List<string> References { get; set; } = new();
    }

    public class ScanResult
    {
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public int FilesScanned { get; set; }
        public List<SecurityIssue> Issues { get; set; } = new();

        public int CriticalCount => Issues.Count(i => i.Severity == Severity.Critical);
        public int HighCount => Issues.Count(i => i.Severity == Severity.High);
        public int MediumCount => Issues.Count(i => i.Severity == Severity.Medium);
        public int LowCount => Issues.Count(i => i.Severity == Severity.Low);
        public int InfoCount => Issues.Count(i => i.Severity == Severity.Info);

        public bool HasCriticalIssues => CriticalCount > 0;
        public bool HasHighIssues => HighCount > 0;
    }

    /// <summary>
    /// ディレクトリをスキャン
    /// </summary>
    public async Task<ScanResult> ScanDirectoryAsync(string directory, string[] patterns = null!)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ScanResult();

        patterns ??= new[] { "*.json", "*.config", "*.cs", "*.bat", "*.ps1", "*.sh" };

        try
        {
            var files = new List<string>();
            foreach (var pattern in patterns)
            {
                files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories));
            }

            result.FilesScanned = files.Count;

            foreach (var file in files)
            {
                var fileIssues = await ScanFileAsync(file).ConfigureAwait(false);
                result.Issues.AddRange(fileIssues);
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add(new SecurityIssue
            {
                Severity = Severity.High,
                Title = "Scan error",
                TitleJa = "スキャンエラー",
                Description = $"Failed to scan directory: {ex.Message}",
                DescriptionJa = $"ディレクトリのスキャンに失敗しました: {ex.Message}"
            });
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    /// <summary>
    /// ファイルをスキャン
    /// </summary>
    public async Task<List<SecurityIssue>> ScanFileAsync(string filePath)
    {
        var issues = new List<SecurityIssue>();

        try
        {
            var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var lines = content.Split('\n');

            // 1. ハードコードされたパスワード検出
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // パスワードパターン
                var passwordPatterns = new[]
                {
                    @"password\s*=\s*[""'](.+?)[""']",
                    @"pwd\s*=\s*[""'](.+?)[""']",
                    @"secret\s*=\s*[""'](.+?)[""']",
                    @"apikey\s*=\s*[""'](.+?)[""']",
                    @"api_key\s*=\s*[""'](.+?)[""']",
                    @"token\s*=\s*[""'](.+?)[""']"
                };

                foreach (var pattern in passwordPatterns)
                {
                    var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                    if (match.Success && !IsPlaceholder(match.Groups[1].Value))
                    {
                        issues.Add(new SecurityIssue
                        {
                            Severity = Severity.Critical,
                            Title = "Hardcoded credentials detected",
                            TitleJa = "ハードコードされた認証情報を検出",
                            Description = "Credentials should never be hardcoded in source files. Use environment variables or secure configuration files.",
                            DescriptionJa = "認証情報をソースファイルにハードコードしないでください。環境変数またはセキュアな設定ファイルを使用してください。",
                            Recommendation = "Move credentials to environment variables or use a secrets management system",
                            RecommendationJa = "認証情報を環境変数に移動するか、シークレット管理システムを使用してください",
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Code = line.Trim(),
                            References = new List<string> { "OWASP A07:2021 - Identification and Authentication Failures" }
                        });
                    }
                }
            }

            // 2. SQLインジェクション脆弱性
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (Regex.IsMatch(line, @"SELECT\s+.*\s+FROM.*\+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"INSERT\s+INTO.*\+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"UPDATE.*SET.*\+", RegexOptions.IgnoreCase))
                {
                    issues.Add(new SecurityIssue
                    {
                        Severity = Severity.High,
                        Title = "Possible SQL injection vulnerability",
                        TitleJa = "SQLインジェクションの可能性",
                        Description = "String concatenation in SQL queries can lead to SQL injection attacks.",
                        DescriptionJa = "SQL クエリでの文字列連結はSQLインジェクション攻撃につながる可能性があります。",
                        Recommendation = "Use parameterized queries or prepared statements",
                        RecommendationJa = "パラメータ化クエリまたはプリペアドステートメントを使用してください",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Code = line.Trim(),
                        References = new List<string> { "OWASP A03:2021 - Injection" }
                    });
                }
            }

            // 3. パストラバーサル脆弱性
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (Regex.IsMatch(line, @"File\.(Read|Write|Delete|Open).*\+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(line, @"Directory\.(Create|Delete|Move).*\+", RegexOptions.IgnoreCase))
                {
                    issues.Add(new SecurityIssue
                    {
                        Severity = Severity.High,
                        Title = "Possible path traversal vulnerability",
                        TitleJa = "パストラバーサルの可能性",
                        Description = "User input in file paths can lead to unauthorized file access.",
                        DescriptionJa = "ファイルパスへのユーザー入力は、不正なファイルアクセスにつながる可能性があります。",
                        Recommendation = "Validate and sanitize all file paths, use Path.GetFullPath() and check against allowed directories",
                        RecommendationJa = "すべてのファイルパスを検証・サニタイズし、Path.GetFullPath()を使用して許可されたディレクトリと照合してください",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Code = line.Trim(),
                        References = new List<string> { "OWASP A01:2021 - Broken Access Control" }
                    });
                }
            }

            // 4. 危険な関数の使用
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                var dangerousFunctions = new Dictionary<string, string>
                {
                    ["Process.Start"] = "Command injection risk",
                    ["Eval"] = "Code injection risk",
                    ["InnerHtml"] = "XSS risk",
                    ["unsafe"] = "Memory safety risk",
                    ["AllowAnonymous"] = "Authentication bypass risk"
                };

                foreach (var (func, risk) in dangerousFunctions)
                {
                    if (line.Contains(func, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new SecurityIssue
                        {
                            Severity = Severity.Medium,
                            Title = $"Potentially dangerous function: {func}",
                            TitleJa = $"潜在的に危険な関数: {func}",
                            Description = $"Using {func} may introduce security risks: {risk}",
                            DescriptionJa = $"{func} の使用はセキュリティリスクをもたらす可能性があります: {risk}",
                            Recommendation = "Review usage and ensure proper validation/sanitization",
                            RecommendationJa = "使用方法を確認し、適切な検証・サニタイズを行ってください",
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Code = line.Trim()
                        });
                    }
                }
            }

            // 5. 弱い暗号化アルゴリズム
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                var weakCrypto = new[] { "MD5", "SHA1", "DES", "RC4" };

                foreach (var algo in weakCrypto)
                {
                    if (Regex.IsMatch(line, $@"\b{algo}\b", RegexOptions.IgnoreCase))
                    {
                        issues.Add(new SecurityIssue
                        {
                            Severity = Severity.High,
                            Title = $"Weak cryptographic algorithm: {algo}",
                            TitleJa = $"弱い暗号化アルゴリズム: {algo}",
                            Description = $"{algo} is considered cryptographically weak and should not be used.",
                            DescriptionJa = $"{algo} は暗号学的に弱いとされており、使用すべきではありません。",
                            Recommendation = "Use modern algorithms like SHA-256, SHA-3, or AES-256",
                            RecommendationJa = "SHA-256、SHA-3、AES-256などの最新アルゴリズムを使用してください",
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Code = line.Trim(),
                            References = new List<string> { "OWASP A02:2021 - Cryptographic Failures" }
                        });
                    }
                }
            }

            // 6. HTTPSなしの通信
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (Regex.IsMatch(line, @"http://(?!localhost|127\.0\.0\.1)", RegexOptions.IgnoreCase))
                {
                    issues.Add(new SecurityIssue
                    {
                        Severity = Severity.Medium,
                        Title = "Insecure HTTP connection",
                        TitleJa = "安全でないHTTP接続",
                        Description = "Using HTTP instead of HTTPS exposes data to interception.",
                        DescriptionJa = "HTTPSではなくHTTPを使用すると、データが傍受される可能性があります。",
                        Recommendation = "Use HTTPS for all external connections",
                        RecommendationJa = "すべての外部接続にHTTPSを使用してください",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Code = line.Trim()
                    });
                }
            }

        }
        catch
        {
            // ファイル読み取りエラーは無視
        }

        return issues;
    }

    /// <summary>
    /// プレースホルダーかどうかを判定
    /// </summary>
    private bool IsPlaceholder(string value)
    {
        var placeholders = new[]
        {
            "your_password", "your_api_key", "your_token", "your_secret",
            "changeme", "change_me", "placeholder", "example", "***", "xxx",
            "<password>", "{password}", "[password]", "password123", "test"
        };

        return placeholders.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
               value.Length < 4;
    }

    /// <summary>
    /// スキャン結果を表示
    /// </summary>
    public string FormatScanResult(ScanResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║  Security Scan Report / セキュリティスキャンレポート          ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine($"Scan time: {result.ScanTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {result.Duration.TotalSeconds:F2}s");
        sb.AppendLine($"Files scanned: {result.FilesScanned}");
        sb.AppendLine();

        // サマリー
        sb.AppendLine("Summary / 概要:");
        sb.AppendLine($"  🔴 Critical: {result.CriticalCount}");
        sb.AppendLine($"  🟠 High: {result.HighCount}");
        sb.AppendLine($"  🟡 Medium: {result.MediumCount}");
        sb.AppendLine($"  🟢 Low: {result.LowCount}");
        sb.AppendLine($"  ℹ️  Info: {result.InfoCount}");
        sb.AppendLine();

        if (!result.Issues.Any())
        {
            sb.AppendLine("✅ No security issues found!");
            sb.AppendLine("✅ セキュリティ問題は見つかりませんでした！");
            return sb.ToString();
        }

        // 重要度順に問題を表示
        var orderedIssues = result.Issues
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.FilePath)
            .ToList();

        foreach (var issue in orderedIssues)
        {
            var icon = issue.Severity switch
            {
                Severity.Critical => "🔴",
                Severity.High => "🟠",
                Severity.Medium => "🟡",
                Severity.Low => "🟢",
                _ => "ℹ️"
            };

            sb.AppendLine($"{icon} [{issue.Severity}] {issue.Title}");
            sb.AppendLine($"   {issue.TitleJa}");

            if (!string.IsNullOrEmpty(issue.FilePath))
            {
                sb.AppendLine($"   File: {issue.FilePath}:{issue.LineNumber}");
            }

            sb.AppendLine($"   {issue.Description}");
            sb.AppendLine($"   {issue.DescriptionJa}");
            sb.AppendLine($"   💡 {issue.Recommendation}");
            sb.AppendLine($"   💡 {issue.RecommendationJa}");

            if (issue.References.Any())
            {
                sb.AppendLine($"   References: {string.Join(", ", issue.References)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("─────────────────────────────────────────────────────────────");

        if (result.HasCriticalIssues)
        {
            sb.AppendLine("⚠️  CRITICAL ISSUES DETECTED - Immediate action required!");
            sb.AppendLine("⚠️  重大な問題が検出されました - 即座の対応が必要です！");
        }
        else if (result.HasHighIssues)
        {
            sb.AppendLine("⚠️  High severity issues detected - Please review");
            sb.AppendLine("⚠️  高い重要度の問題が検出されました - 確認してください");
        }

        return sb.ToString();
    }

    /// <summary>
    /// JSON形式でエクスポート
    /// </summary>
    public string ExportToJson(ScanResult result)
    {
        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
