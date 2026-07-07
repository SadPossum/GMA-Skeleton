namespace Gma.Modules.Auth.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Auth.Domain.Services;
using Microsoft.Extensions.Options;

internal sealed class RefreshTokenHashingService(IOptions<RefreshTokenHashingOptions> options)
    : IRefreshTokenHashingService
{
    private const string AlgorithmPrefix = "hmac-sha256";

    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        byte[] key = Encoding.UTF8.GetBytes(options.Value.Pepper);
        byte[] data = Encoding.UTF8.GetBytes(refreshToken);
        byte[] hash = HMACSHA256.HashData(key, data);

        return $"{AlgorithmPrefix}:{Convert.ToBase64String(hash)}";
    }
}
