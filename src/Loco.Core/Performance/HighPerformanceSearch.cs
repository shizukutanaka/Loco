using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Loco.Core.Performance;

/// <summary>
/// SearchValues&lt;T&gt; を活用した高性能検索
///
/// パフォーマンス改善:
/// - IndexOfAny: 4-7倍高速化 (vs Span.IndexOfAny)
/// - 長いテキスト: 最大250倍高速化
/// - SIMD/ベクトル化: 自動的に最適なアルゴリズムを選択
///
/// 使用場面:
/// - 3文字以上の検索で効果的
/// - ホットパス (1リクエストあたり数百～数百万回実行)
/// - ログ解析、データフィルタリング、テキストパース
///
/// 参考: https://endjin.com/blog/2024/01/dotnet-8-searchvalues-string-search-performance-boost
/// </summary>
public static class HighPerformanceSearch
{
    // 事前にパースされた SearchValues (起動時に1回だけコスト発生)

    /// <summary>
    /// 空白文字の高速検索
    /// </summary>
    public static readonly SearchValues<char> Whitespace =
        SearchValues.Create(" \t\n\r\f\v");

    /// <summary>
    /// 数字の高速検索
    /// </summary>
    public static readonly SearchValues<char> Digits =
        SearchValues.Create("0123456789");

    /// <summary>
    /// 16進数の高速検索
    /// </summary>
    public static readonly SearchValues<char> HexDigits =
        SearchValues.Create("0123456789ABCDEFabcdef");

    /// <summary>
    /// 英字 (小文字) の高速検索
    /// </summary>
    public static readonly SearchValues<char> LowercaseLetters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyz");

    /// <summary>
    /// 英字 (大文字) の高速検索
    /// </summary>
    public static readonly SearchValues<char> UppercaseLetters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

    /// <summary>
    /// ワークフロー変数プレースホルダーの境界文字
    /// </summary>
    public static readonly SearchValues<char> VariablePlaceholderBoundaries =
        SearchValues.Create("${}");

    /// <summary>
    /// JSONの特殊文字
    /// </summary>
    public static readonly SearchValues<char> JsonSpecialChars =
        SearchValues.Create("\"\\/\b\f\n\r\t");

    /// <summary>
    /// パス区切り文字
    /// </summary>
    public static readonly SearchValues<char> PathSeparators =
        SearchValues.Create("/\\");

    /// <summary>
    /// 改行文字
    /// </summary>
    public static readonly SearchValues<char> LineBreaks =
        SearchValues.Create("\r\n");

    /// <summary>
    /// ワークフロー式の演算子
    /// </summary>
    public static readonly SearchValues<char> ExpressionOperators =
        SearchValues.Create("+-*/%=<>!&|^~?:");

    /// <summary>
    /// 危険なシェル文字 (コマンドインジェクション対策)
    /// </summary>
    public static readonly SearchValues<char> DangerousShellChars =
        SearchValues.Create(";|&$`\"'\\<>(){}[]!#*?~");

    /// <summary>
    /// 空白文字を含むかチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsWhitespace(ReadOnlySpan<char> text) =>
        text.ContainsAny(Whitespace);

    /// <summary>
    /// 最初の空白文字の位置を取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfWhitespace(ReadOnlySpan<char> text) =>
        text.IndexOfAny(Whitespace);

    /// <summary>
    /// 数字のみで構成されているかチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDigitsOnly(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return false;
        return text.IndexOfAnyExcept(Digits) < 0;
    }

    /// <summary>
    /// 16進数のみで構成されているかチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexOnly(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return false;
        return text.IndexOfAnyExcept(HexDigits) < 0;
    }

    /// <summary>
    /// 変数プレースホルダーを含むかチェック (${...} 形式)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsVariablePlaceholder(ReadOnlySpan<char> text) =>
        text.ContainsAny(VariablePlaceholderBoundaries);

    /// <summary>
    /// 危険なシェル文字を含むかチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsDangerousShellChars(ReadOnlySpan<char> command) =>
        command.ContainsAny(DangerousShellChars);

    /// <summary>
    /// 最初の危険な文字の位置を取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfDangerousChar(ReadOnlySpan<char> command) =>
        command.IndexOfAny(DangerousShellChars);

    /// <summary>
    /// JSONエスケープが必要かチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsJsonEscaping(ReadOnlySpan<char> text) =>
        text.ContainsAny(JsonSpecialChars);

    /// <summary>
    /// 改行で分割 (高速版)
    /// </summary>
    public static void SplitLines(
        ReadOnlySpan<char> text,
        Span<Range> ranges,
        out int lineCount)
    {
        lineCount = 0;
        var start = 0;

        while (start < text.Length && lineCount < ranges.Length)
        {
            var remaining = text[start..];
            var newlineIndex = remaining.IndexOfAny(LineBreaks);

            if (newlineIndex < 0)
            {
                // 最後の行
                ranges[lineCount++] = new Range(start, text.Length);
                break;
            }

            ranges[lineCount++] = new Range(start, start + newlineIndex);
            start += newlineIndex + 1;

            // \r\n の場合は \n もスキップ
            if (start < text.Length && text[start - 1] == '\r' && text[start] == '\n')
            {
                start++;
            }
        }
    }

    /// <summary>
    /// パスセグメントに分割 (高速版)
    /// </summary>
    public static void SplitPath(
        ReadOnlySpan<char> path,
        Span<Range> segments,
        out int segmentCount)
    {
        segmentCount = 0;
        var start = 0;

        // 先頭のセパレータをスキップ
        while (start < path.Length && (path[start] == '/' || path[start] == '\\'))
        {
            start++;
        }

        while (start < path.Length && segmentCount < segments.Length)
        {
            var remaining = path[start..];
            var sepIndex = remaining.IndexOfAny(PathSeparators);

            if (sepIndex < 0)
            {
                // 最後のセグメント
                if (start < path.Length)
                {
                    segments[segmentCount++] = new Range(start, path.Length);
                }
                break;
            }

            if (sepIndex > 0)
            {
                segments[segmentCount++] = new Range(start, start + sepIndex);
            }
            start += sepIndex + 1;
        }
    }

    /// <summary>
    /// 式から演算子を抽出
    /// </summary>
    public static int CountOperators(ReadOnlySpan<char> expression)
    {
        var count = 0;
        foreach (var c in expression)
        {
            if (ExpressionOperators.Contains(c))
            {
                count++;
            }
        }
        return count;
    }
}

/// <summary>
/// カスタム SearchValues ビルダー
/// アプリケーション固有の検索パターンを定義
/// </summary>
public sealed class SearchValuesBuilder
{
    private readonly HashSet<char> _chars = new();

    /// <summary>
    /// 文字を追加
    /// </summary>
    public SearchValuesBuilder Add(char c)
    {
        _chars.Add(c);
        return this;
    }

    /// <summary>
    /// 文字列の全文字を追加
    /// </summary>
    public SearchValuesBuilder Add(string chars)
    {
        foreach (var c in chars)
        {
            _chars.Add(c);
        }
        return this;
    }

    /// <summary>
    /// 文字範囲を追加
    /// </summary>
    public SearchValuesBuilder AddRange(char from, char to)
    {
        for (var c = from; c <= to; c++)
        {
            _chars.Add(c);
        }
        return this;
    }

    /// <summary>
    /// SearchValues を構築
    /// </summary>
    public SearchValues<char> Build()
    {
        return SearchValues.Create(new string(_chars.ToArray()));
    }

    /// <summary>
    /// 数字 (0-9) を追加
    /// </summary>
    public SearchValuesBuilder AddDigits() => AddRange('0', '9');

    /// <summary>
    /// 小文字 (a-z) を追加
    /// </summary>
    public SearchValuesBuilder AddLowercase() => AddRange('a', 'z');

    /// <summary>
    /// 大文字 (A-Z) を追加
    /// </summary>
    public SearchValuesBuilder AddUppercase() => AddRange('A', 'Z');

    /// <summary>
    /// 英数字を追加
    /// </summary>
    public SearchValuesBuilder AddAlphanumeric() =>
        AddDigits().AddLowercase().AddUppercase();
}

/// <summary>
/// ワークフロー変数パーサー (SearchValues 最適化版)
/// </summary>
public static class FastVariableParser
{
    private static readonly SearchValues<char> VariableStart =
        SearchValues.Create("$");

    private static readonly SearchValues<char> VariableNameChars =
        new SearchValuesBuilder()
            .AddAlphanumeric()
            .Add('_')
            .Build();

    /// <summary>
    /// テキスト内の変数参照を検出
    /// 例: ${var_name} または $VAR
    /// </summary>
    public static int FindVariables(
        ReadOnlySpan<char> text,
        Span<Range> variables,
        out int variableCount)
    {
        variableCount = 0;
        var pos = 0;

        while (pos < text.Length && variableCount < variables.Length)
        {
            var remaining = text[pos..];
            var dollarIndex = remaining.IndexOfAny(VariableStart);

            if (dollarIndex < 0) break;

            var varStart = pos + dollarIndex;
            pos = varStart + 1;

            if (pos >= text.Length) break;

            // ${...} 形式
            if (text[pos] == '{')
            {
                var closeIndex = text[pos..].IndexOf('}');
                if (closeIndex > 1)
                {
                    variables[variableCount++] = new Range(varStart, pos + closeIndex + 1);
                    pos += closeIndex + 1;
                    continue;
                }
            }

            // $VAR 形式 (英数字+アンダースコアのみ)
            var nameEnd = pos;
            while (nameEnd < text.Length && VariableNameChars.Contains(text[nameEnd]))
            {
                nameEnd++;
            }

            if (nameEnd > pos)
            {
                variables[variableCount++] = new Range(varStart, nameEnd);
                pos = nameEnd;
            }
        }

        return variableCount;
    }

    /// <summary>
    /// 変数参照を含むかチェック
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsVariables(ReadOnlySpan<char> text) =>
        text.ContainsAny(VariableStart);
}
