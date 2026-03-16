using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace Kitchenhelper.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int?> RegisterAsync(string email, string password)
    {
        email = email?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        // Email zaten kayıtlı mı kontrol et
        if (await EmailExistsAsync(email))
            return null;

        // Şifreyi hashle
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user.Id;
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        email = email?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            return null;

        // Şifre kontrolü
        bool isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValidPassword)
            return null;

        return user;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        email = email?.Trim().ToLowerInvariant() ?? "";
        return await _db.Users.AnyAsync(u => u.Email == email);
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
