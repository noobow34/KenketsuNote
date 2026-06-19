using System.Security.Cryptography;

namespace KenketsuNote.Infrastructure;

public static class IdGenerator
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string Generate(int length = 10)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => Chars[b % Chars.Length]).ToArray());
    }
}
