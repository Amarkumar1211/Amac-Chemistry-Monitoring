namespace FurnaceMonitor.Application.DTOs.Common;

/// <summary>
/// Standardised envelope for every API response.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }

    public static ApiResponse Ok(object? data = null, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

/// <summary>
/// Standardised paginated result.
/// </summary>
public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
