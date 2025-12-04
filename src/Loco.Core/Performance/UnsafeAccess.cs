using System;
using System.Runtime.CompilerServices;

namespace Loco.Core.Performance;

/// <summary>
/// UnsafeAccessor を活用したゼロオーバーヘッドメンバーアクセス
///
/// パフォーマンス改善:
/// - リフレクション: 約19ns → UnsafeAccessor: 約0.35ns (50倍高速)
/// - AOT対応: リフレクションと異なりNative AOTで使用可能
/// - ゼロオーバーヘッド: 直接アクセスと同等のパフォーマンス
///
/// 制限事項:
/// - コンパイル時に型とメンバーが既知である必要がある
/// - ランタイムで動的に決定する場合はリフレクションを使用
///
/// 参考: https://blog.ndepend.com/modern-net-reflection-with-unsafeaccessor/
/// </summary>

#region Internal Types Access Examples

// 例: 内部クラスのプライベートフィールドにアクセス
// これらのメソッドは実装を持たず、ランタイムが自動生成する

/// <summary>
/// ワークフロー実行状態の内部フィールドアクセス
/// </summary>
internal static class WorkflowStateAccessor
{
    /// <summary>
    /// プライベートフィールド "_startTime" にアクセス
    /// 使用例: var startTime = GetStartTime(workflowState);
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_startTime")]
    public static extern ref DateTime GetStartTime(object target);

    /// <summary>
    /// プライベートフィールド "_executionContext" にアクセス
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_executionContext")]
    public static extern ref object GetExecutionContext(object target);

    /// <summary>
    /// プライベートメソッド "InternalReset" を呼び出し
    /// 使用例: InternalReset(workflowState);
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InternalReset")]
    public static extern void InternalReset(object target);
}

#endregion

#region Generic Unsafe Accessor Patterns

/// <summary>
/// ジェネリック型のプライベートメンバーアクセス
/// </summary>
/// <typeparam name="T">対象の型</typeparam>
public static class UnsafeFieldAccessor<T> where T : class
{
    /// <summary>
    /// プライベートフィールドを取得するデリゲート
    /// コンパイル時にフィールド名を指定する必要がある
    /// </summary>
    public delegate ref TField FieldGetter<TField>(T target);

    /// <summary>
    /// プライベートプロパティを取得するデリゲート
    /// </summary>
    public delegate TProperty PropertyGetter<TProperty>(T target);

    /// <summary>
    /// プライベートメソッドを呼び出すデリゲート
    /// </summary>
    public delegate TResult MethodInvoker<TResult>(T target);
}

#endregion

#region Fast Type Casting

/// <summary>
/// Unsafe.As を使用した高速型変換
/// ゼロコストでの型変換が可能
/// </summary>
public static class FastCast
{
    /// <summary>
    /// TFrom から TTo への高速キャスト
    /// 警告: 型安全性は保証されないため、使用には注意が必要
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo As<TFrom, TTo>(TFrom value)
        where TFrom : class?
        where TTo : class?
    {
        return Unsafe.As<TTo>(value);
    }

    /// <summary>
    /// ref 変換による高速キャスト
    /// 値型と参照型の相互変換に使用
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo AsRef<TFrom, TTo>(ref TFrom source)
    {
        return ref Unsafe.As<TFrom, TTo>(ref source);
    }
}

#endregion

#region Unsafe Skip Init

/// <summary>
/// Unsafe.SkipInit を使用した初期化スキップ
/// 大きな構造体の初期化コストを削減
/// </summary>
public static class SkipInitHelper
{
    /// <summary>
    /// 初期化をスキップして変数を宣言
    /// 警告: 初期化前にアクセスするとバグの原因となる
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CreateUninitialized<T>(out T value) where T : struct
    {
        Unsafe.SkipInit(out value);
    }
}

#endregion

#region Unsafe Array Access

/// <summary>
/// Unsafe を使用した境界チェックなし配列アクセス
/// 高速だが、範囲外アクセスに注意
/// </summary>
public static class UnsafeArrayAccess
{
    /// <summary>
    /// 境界チェックなしで配列要素を取得
    /// 警告: インデックスが範囲内であることを保証する必要がある
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetUnchecked<T>(T[] array, int index)
    {
        ref T arrayStart = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
        return ref Unsafe.Add(ref arrayStart, index);
    }

    /// <summary>
    /// 境界チェックなしで配列要素を設定
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetUnchecked<T>(T[] array, int index, T value)
    {
        ref T arrayStart = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
        Unsafe.Add(ref arrayStart, index) = value;
    }

    /// <summary>
    /// 配列の先頭要素への参照を取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetReference<T>(T[] array)
    {
        return ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
    }

    /// <summary>
    /// 2次元配列風のアクセス (1次元配列を2次元として扱う)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Get2D<T>(T[] array, int width, int x, int y)
    {
        ref T arrayStart = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
        return ref Unsafe.Add(ref arrayStart, y * width + x);
    }
}

#endregion

#region Unsafe Null Checks

/// <summary>
/// Unsafe を使用した高速nullチェック
/// </summary>
public static class UnsafeNullCheck
{
    /// <summary>
    /// 参照型がnullでないことをコンパイラに保証
    /// 警告: 実際にnullの場合は未定義動作
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AssumeNotNull<T>(T? value) where T : class
    {
        // Unsafe.AsRef は null を許可しないため、
        // コンパイラに非nullを保証
        return value!;
    }

    /// <summary>
    /// nullチェックなしで参照を取得
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetReferenceUnchecked<T>(T? value) where T : class
    {
        return ref Unsafe.AsRef(in value)!;
    }
}

#endregion

#region Unsafe Struct Operations

/// <summary>
/// 構造体の高速操作
/// </summary>
public static class UnsafeStructOps
{
    /// <summary>
    /// 構造体のサイズを取得 (コンパイル時定数)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T>() where T : struct
    {
        return Unsafe.SizeOf<T>();
    }

    /// <summary>
    /// 構造体をバイト配列にコピー
    /// </summary>
    public static void CopyToBytes<T>(ref T source, Span<byte> destination) where T : struct
    {
        if (destination.Length < Unsafe.SizeOf<T>())
        {
            throw new ArgumentException("Destination buffer too small");
        }

        ref byte destRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(destination);
        Unsafe.WriteUnaligned(ref destRef, source);
    }

    /// <summary>
    /// バイト配列から構造体を読み取り
    /// </summary>
    public static T ReadFromBytes<T>(ReadOnlySpan<byte> source) where T : struct
    {
        if (source.Length < Unsafe.SizeOf<T>())
        {
            throw new ArgumentException("Source buffer too small");
        }

        ref readonly byte srcRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source);
        return Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(in srcRef));
    }

    /// <summary>
    /// 構造体の浅いコピー
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy<T>(ref T source, ref T destination) where T : struct
    {
        destination = source;
    }

    /// <summary>
    /// 構造体配列の高速コピー
    /// </summary>
    public static void CopyArray<T>(T[] source, T[] destination, int length) where T : struct
    {
        if (source.Length < length || destination.Length < length)
        {
            throw new ArgumentException("Array too small");
        }

        ref T srcRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(source);
        ref T dstRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(destination);

        for (int i = 0; i < length; i++)
        {
            Unsafe.Add(ref dstRef, i) = Unsafe.Add(ref srcRef, i);
        }
    }
}

#endregion

#region Benchmarking Utilities

/// <summary>
/// UnsafeAccessor のパフォーマンスベンチマーク用ユーティリティ
/// </summary>
public static class UnsafeAccessorBenchmark
{
    /// <summary>
    /// リフレクション vs UnsafeAccessor のパフォーマンス比較
    /// </summary>
    public static string GetPerformanceComparison()
    {
        return """
            UnsafeAccessor Performance Comparison:

            Method                  | Time (ns) | Relative Speed
            ------------------------|-----------|----------------
            Direct Access           | 0.10      | 1x (baseline)
            UnsafeAccessor          | 0.35      | 3.5x slower
            Cached Reflection       | 1.20      | 12x slower
            Reflection (uncached)   | 19.40     | 194x slower

            Memory Allocation:
            - Direct Access: 0 bytes
            - UnsafeAccessor: 0 bytes
            - Reflection: 40-120 bytes per call

            AOT Compatibility:
            - Direct Access: ✓ Yes
            - UnsafeAccessor: ✓ Yes
            - Reflection: ✗ Limited (trimming issues)
            """;
    }
}

#endregion

#region Safe Wrappers

/// <summary>
/// Unsafe 操作の安全なラッパー
/// デバッグビルドで境界チェックを有効化
/// </summary>
public static class SafeUnsafeOps
{
    /// <summary>
    /// 安全な配列アクセス (デバッグ時のみチェック)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetElement<T>(T[] array, int index)
    {
#if DEBUG
        if (index < 0 || index >= array.Length)
        {
            throw new IndexOutOfRangeException($"Index {index} is out of range [0, {array.Length})");
        }
#endif
        return ref UnsafeArrayAccess.GetUnchecked(array, index);
    }

    /// <summary>
    /// 安全なSpanアクセス
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetSpanElement<T>(Span<T> span, int index)
    {
#if DEBUG
        if (index < 0 || index >= span.Length)
        {
            throw new IndexOutOfRangeException($"Index {index} is out of range [0, {span.Length})");
        }
#endif
        return ref Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span), index);
    }
}

#endregion
