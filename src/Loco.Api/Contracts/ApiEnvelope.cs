namespace Loco.Api.Contracts;

/// <summary>
/// The response envelope every endpoint returns, matching the Visual Editor's
/// <c>ApiResponse&lt;T&gt;</c> discriminated union exactly
/// (src/Loco.VisualEditor/src/api/types.ts):
///
///   { success: true,  data: T,        message?: string }
///   { success: false, error: ApiError, message?: string }
///
/// The previous controllers returned raw DTOs with no envelope, so the frontend
/// treated every successful call as a failure (response.success was undefined).
/// Serialized with camelCase globally (see Program.cs).
/// </summary>
public sealed class ApiEnvelope<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? Message { get; init; }
}

/// <summary>Matches the frontend's <c>ApiError</c> interface.</summary>
public sealed class ApiError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public Dictionary<string, object>? Details { get; init; }
}

/// <summary>Factory helpers so controllers read declaratively.</summary>
public static class Envelope
{
    public static ApiEnvelope<T> Ok<T>(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    /// <summary>Success with no payload (frontend types these as ApiResponse&lt;void&gt;).</summary>
    public static ApiEnvelope<object> Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiEnvelope<object> Fail(string code, string message, Dictionary<string, object>? details = null) =>
        new() { Success = false, Error = new ApiError { Code = code, Message = message, Details = details } };
}
