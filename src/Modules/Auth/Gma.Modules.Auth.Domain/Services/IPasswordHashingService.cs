namespace Gma.Modules.Auth.Domain.Services;

public interface IPasswordHashingService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}
