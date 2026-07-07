namespace Gma.Modules.Auth.Contracts;

public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);
