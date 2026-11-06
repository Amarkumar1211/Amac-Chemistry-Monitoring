namespace FurnaceMonitor.Application.Interfaces.Security;

/// <summary>
/// Abstraction over password hashing so the underlying algorithm can be
/// swapped without touching the domain/service layers.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
