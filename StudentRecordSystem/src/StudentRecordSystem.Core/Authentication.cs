using System.Security.Cryptography;

namespace StudentRecordSystem.Core;

public static class Authentication
{
    private const int Iterations = 120_000;

    public static Account CreateAccount(string id, string username, string displayName,
        UserRole role, string? studentId, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return new(id, username, displayName, role, studentId,
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static UserSession? Login(SchoolData data, string username, string password)
    {
        var account = data.Accounts.SingleOrDefault(a =>
            a.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (account is null) return null;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(account.Salt),
            Iterations, HashAlgorithmName.SHA256, 32);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(account.PasswordHash)))
            return null;
        return new(account.Id, account.DisplayName, account.Role, account.StudentId);
    }
}
