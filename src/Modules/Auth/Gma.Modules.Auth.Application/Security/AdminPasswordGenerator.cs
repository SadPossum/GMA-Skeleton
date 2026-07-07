namespace Gma.Modules.Auth.Application.Security;

using System.Security.Cryptography;

public static class AdminPasswordGenerator
{
    public const int GeneratedLength = 24;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%^*-_";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[GeneratedLength];

        for (int index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
