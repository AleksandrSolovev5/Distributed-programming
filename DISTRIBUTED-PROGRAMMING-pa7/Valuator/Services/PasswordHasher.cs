using System.Security.Cryptography;

namespace Valuator.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static string HashPassword( string password )
    {
        byte[] salt = RandomNumberGenerator.GetBytes( SaltSize );

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize );

        return $"v1.{Iterations}.{Convert.ToBase64String( salt )}.{Convert.ToBase64String( hash )}";
    }

    public static bool VerifyPassword( string password, string savedPasswordHash )
    {
        string[] parts = savedPasswordHash.Split( '.' );

        if ( parts.Length != 4 )
            return false;

        if ( parts[ 0 ] != "v1" )
            return false;

        if ( !int.TryParse( parts[ 1 ], out int iterations ) )
            return false;

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String( parts[ 2 ] );
            expectedHash = Convert.FromBase64String( parts[ 3 ] );
        }
        catch
        {
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: expectedHash.Length );

        return CryptographicOperations.FixedTimeEquals( actualHash, expectedHash );
    }
}