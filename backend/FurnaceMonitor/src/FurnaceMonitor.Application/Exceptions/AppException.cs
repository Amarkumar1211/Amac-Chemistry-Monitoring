namespace FurnaceMonitor.Application.Exceptions;

/// <summary>
/// Base application exception carrying an HTTP status for the API layer.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message)
        => StatusCode = statusCode;
}

/// <summary>Thrown when a requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}

/// <summary>Thrown when the caller is not permitted to perform an action (HTTP 403).</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, 403) { }
}

/// <summary>Thrown when authentication fails (HTTP 401).</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, 401) { }
}

/// <summary>Thrown when a business/validation rule is violated (HTTP 409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409) { }
}

