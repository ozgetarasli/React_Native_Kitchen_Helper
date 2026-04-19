using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Kitchenhelper.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email ve şifre gereklidir" });

        // Email format doğrulama
        if (!IsValidEmail(request.Email))
            return BadRequest(new { message = "Geçerli bir email adresi giriniz" });

        // Şifre güçlülük kontrolü
        if (request.Password.Length < 6)
            return BadRequest(new { message = "Şifre en az 6 karakter olmalıdır" });

        if (request.Password.Length > 128)
            return BadRequest(new { message = "Şifre en fazla 128 karakter olabilir" });

        // Email zaten kayıtlı mı kontrol et
        if (await _authService.EmailExistsAsync(request.Email))
            return Conflict(new { message = "Bu email ile kayıtlı bir kullanıcı zaten var" });

        var userId = await _authService.RegisterAsync(request.Email, request.Password);
        if (userId == null)
            return BadRequest(new { message = "Kayıt başarısız oldu" });

        return StatusCode(201, new { message = "Kayıt başarılı, giriş yapabilirsiniz", userId });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email ve şifre gereklidir" });

        var user = await _authService.LoginAsync(request.Email, request.Password);
        if (user == null)
            return Unauthorized(new { message = "Email veya şifre hatalı" });

        // Son giriş zamanını güncelle
        await _authService.UpdateLastLoginAsync(user.Id);

        // JWT token oluştur
        var token = GenerateJwtToken(user.Id, user.Email);

        // HttpOnly cookie olarak token'ı gönder
        Response.Cookies.Append("auth_token", token, new CookieOptions
        {
            HttpOnly = true,        // JavaScript'ten erişilemez (XSS koruması)
            Secure = true,          // Sadece HTTPS üzerinden gönderilir
            SameSite = SameSiteMode.Lax, // CSRF koruması - cross-origin POST isteklerini engeller
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"              // Tüm path'lerde geçerli
        });

        return Ok(new
        {
            message = "Giriş başarılı",
            token = token,
            user = new
            {
                id = user.Id,
                email = user.Email
            }
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Cookie'yi sil
        Response.Cookies.Delete("auth_token");
        return Ok(new { message = "Çıkış başarılı" });
    }

    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        // Cookie'den token'ı al
        var token = Request.Cookies["auth_token"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { message = "Giriş yapmanız gerekiyor" });

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "");
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = int.Parse(jwtToken.Claims.First(x => x.Type == "userId").Value);
            var email = jwtToken.Claims.First(x => x.Type == ClaimTypes.Email).Value;

            return Ok(new
            {
                user = new { id = userId, email }
            });
        }
        catch
        {
            return Unauthorized(new { message = "Geçersiz token" });
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }

    private string GenerateJwtToken(int userId, string email)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? ""));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
