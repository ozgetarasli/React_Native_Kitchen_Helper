using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kitchenhelper.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("api")]
public class AichatController : ControllerBase
{
    private readonly IAiChatService _aiChatService;

    public AichatController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
            return BadRequest(new { message = "Mesaj boş olamaz" });

        // Mesaj uzunluğu sınırı (DoS koruması)
        if (request.Message.Length > 2000)
            return BadRequest(new { message = "Mesaj en fazla 2000 karakter olabilir" });

        if (request.Message.ToLower().Trim() == "test")
            return Ok(new { reply = "Connectivity Test OK - Backend is reached!" });

        try
        {
            var reply = await _aiChatService.GetChatResponseAsync(request.Message);
            return Ok(new { reply });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "AI servisi şu anda kullanılamıyor, lütfen tekrar deneyin." });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
