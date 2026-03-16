using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

public interface IAuthService
{
    /// <summary>
    /// Email ve şifre ile yeni kullanıcı kaydı
    /// </summary>
    /// <returns>Oluşturulan kullanıcı ID, başarısızsa null</returns>
    Task<int?> RegisterAsync(string email, string password);
    
    /// <summary>
    /// Email ve şifre ile giriş kontrolü
    /// </summary>
    /// <returns>Kullanıcı bulundu ve şifre doğruysa User nesnesi, değilse null</returns>
    Task<User?> LoginAsync(string email, string password);
    
    /// <summary>
    /// Email'in sistemde kayıtlı olup olmadığını kontrol eder
    /// </summary>
    Task<bool> EmailExistsAsync(string email);
    
    /// <summary>
    /// Kullanıcının son giriş zamanını günceller
    /// </summary>
    Task UpdateLastLoginAsync(int userId);
}
