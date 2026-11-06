using FurnaceMonitor.Application.Interfaces.Security;

namespace FurnaceMonitor.Infrastructure.Security;

/// <summary>
/// BCrypt password hashing implementation.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string storedHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
