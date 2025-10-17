using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Quality;

namespace CodeQualityAnalyzer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Loco Code Quality Analyzer");
        Console.WriteLine("================================");

        var solutionPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        solutionPath = Path.GetFullPath(solutionPath);

        if (!Directory.Exists(solutionPath))
        {
            Console.WriteLine($"❌ Solution path not found: {solutionPath}");
            return;
        }

        Console.WriteLine($"📁 Analyzing solution: {solutionPath}");
        Console.WriteLine();

        try
        {
            var analyzer = new CodeQualityAnalyzer();
            var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

            // コンソール出力
            DisplayReport(report);

            // JSONレポート保存
            var reportPath = Path.Combine(solutionPath, "code-quality-report.json");
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(reportPath, json);

            Console.WriteLine();
            Console.WriteLine($"📄 Report saved to: {reportPath}");

            // HTMLレポート生成
            var htmlPath = Path.Combine(solutionPath, "code-quality-report.html");
            var html = GenerateHtmlReport(report);
            await File.WriteAllTextAsync(htmlPath, html);

            Console.WriteLine($"🌐 HTML report saved to: {htmlPath}");

            // 品質スコアに基づく終了コード
            Environment.Exit(report.QualityScore >= 70 ? 0 : 1);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Analysis failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void DisplayReport(CodeQualityReport report)
    {
        Console.WriteLine($"📊 Overall Quality Score: {report.QualityScore:F1}/100");
        Console.WriteLine();

        Console.WriteLine("📈 Overall Metrics:");
        Console.WriteLine($"   • Total Projects: {report.TotalProjects}");
        Console.WriteLine($"   • Total Source Files: {report.TotalSourceFiles}");
        Console.WriteLine($"   • Total Lines of Code: {report.OverallMetrics.TotalLines:N0}");
        Console.WriteLine($"   • Code Lines: {report.OverallMetrics.TotalCodeLines:N0}");
        Console.WriteLine($"   • Comment Lines: {report.OverallMetrics.TotalCommentLines:N0}");
        Console.WriteLine($"   • Comment Ratio: {report.OverallMetrics.CodeCommentRatio:P1}");
        Console.WriteLine($"   • Average Complexity: {report.OverallMetrics.AverageComplexity:F1}");
        Console.WriteLine($"   • Average Maintainability: {report.OverallMetrics.AverageMaintainability:F1}");
        Console.WriteLine($"   • Total Violations: {report.OverallMetrics.TotalViolations}");
        Console.WriteLine();

        if (report.Recommendations.Any())
        {
            Console.WriteLine("💡 Recommendations:");
            foreach (var recommendation in report.Recommendations)
            {
                Console.WriteLine($"   • {recommendation}");
            }
            Console.WriteLine();
        }

        if (report.Errors.Any())
        {
            Console.WriteLine("❌ Errors:");
            foreach (var error in report.Errors)
            {
                Console.WriteLine($"   • {error}");
            }
            Console.WriteLine();
        }

        // 品質スコアに基づく評価
        var score = report.QualityScore;
        if (score >= 90)
        {
            Console.WriteLine("🎉 Excellent code quality!");
        }
        else if (score >= 70)
        {
            Console.WriteLine("✅ Good code quality with room for improvement");
        }
        else if (score >= 50)
        {
            Console.WriteLine("⚠️ Moderate code quality - improvements recommended");
        }
        else
        {
            Console.WriteLine("🚨 Poor code quality - immediate improvements needed");
        }
    }

    static string GenerateHtmlReport(CodeQualityReport report)
    {
        var scoreColor = report.QualityScore >= 70 ? "#28a745" : report.QualityScore >= 50 ? "#ffc107" : "#dc3545";

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Loco Code Quality Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f8f9fa; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 10px; text-align: center; margin-bottom: 30px; }}
        .score {{ font-size: 48px; font-weight: bold; color: {scoreColor}; margin: 20px 0; }}
        .metrics {{ background: white; padding: 20px; border-radius: 10px; margin-bottom: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .metric {{ display: inline-block; margin: 10px 20px 10px 0; }}
        .metric-label {{ font-weight: bold; color: #666; }}
        .metric-value {{ font-size: 18px; color: #333; }}
        .recommendations {{ background: white; padding: 20px; border-radius: 10px; margin-bottom: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .recommendation {{ margin: 10px 0; padding: 10px; background: #f8f9fa; border-left: 4px solid #667eea; }}
        .errors {{ background: #f8d7da; border: 1px solid #f5c6cb; padding: 20px; border-radius: 10px; margin-bottom: 20px; }}
        .error {{ color: #721c24; margin: 5px 0; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Loco Code Quality Report</h1>
        <p>Generated on {report.AnalysisDate:yyyy-MM-dd HH:mm:ss} UTC</p>
        <div class='score'>{report.QualityScore:F1}/100</div>
    </div>

    <div class='metrics'>
        <h2>📊 Overall Metrics</h2>
        <div class='metric'><span class='metric-label'>Projects:</span> <span class='metric-value'>{report.TotalProjects}</span></div>
        <div class='metric'><span class='metric-label'>Source Files:</span> <span class='metric-value'>{report.TotalSourceFiles}</span></div>
        <div class='metric'><span class='metric-label'>Total Lines:</span> <span class='metric-value'>{report.OverallMetrics.TotalLines:N0}</span></div>
        <div class='metric'><span class='metric-label'>Code Lines:</span> <span class='metric-value'>{report.OverallMetrics.TotalCodeLines:N0}</span></div>
        <div class='metric'><span class='metric-label'>Comment Lines:</span> <span class='metric-value'>{report.OverallMetrics.TotalCommentLines:N0}</span></div>
        <div class='metric'><span class='metric-label'>Comment Ratio:</span> <span class='metric-value'>{report.OverallMetrics.CodeCommentRatio:P1}</span></div>
        <div class='metric'><span class='metric-label'>Avg Complexity:</span> <span class='metric-value'>{report.OverallMetrics.AverageComplexity:F1}</span></div>
        <div class='metric'><span class='metric-label'>Avg Maintainability:</span> <span class='metric-value'>{report.OverallMetrics.AverageMaintainability:F1}</span></div>
        <div class='metric'><span class='metric-label'>Total Violations:</span> <span class='metric-value'>{report.OverallMetrics.TotalViolations}</span></div>
    </div>

    {(report.Recommendations.Any() ? $@"
    <div class='recommendations'>
        <h2>💡 Recommendations</h2>
        {string.Join("", report.Recommendations.Select(r => $"<div class='recommendation'>• {r}</div>"))}
    </div>" : "")}

    {(report.Errors.Any() ? $@"
    <div class='errors'>
        <h2>❌ Errors</h2>
        {string.Join("", report.Errors.Select(e => $"<div class='error'>• {e}</div>"))}
    </div>" : "")}
</body>
</html>";
    }
}
