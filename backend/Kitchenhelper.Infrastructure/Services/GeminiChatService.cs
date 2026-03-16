using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Kitchenhelper.Infrastructure.Services;

public class GeminiChatService : IAiChatService
{
    private readonly ILogger<GeminiChatService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IRecipeService _recipeService;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    private const string SystemPromptTemplate = @"Sen KitchenHelper uygulamasının akıllı mutfak asistanısın. Görevin kullanıcılara yemek seçiminde yardımcı olmak, tarif önermek ve mutfakla ilgili sorularını yanıtlamaktır.

AŞAĞIDAKİ TARİFLER SİSTEMDE MEVCUTTUR (Bu tarifleri önerirken KESİNLİKLE belirtilen formatta link kullan):
{0}

KURALLAR:
1. Eğer kullanıcı belirli bir kalori, yemek türü veya elindeki malzemelere göre öneri isterse, yukarıdaki listeden en uygun olanları öner.
2. Önerdiğin tarifleri KESİNLİKLE şu formatta yaz: [Tarif Adı](recipe:ID). Örn: '[Domates Çorbası](recipe:5) tarifini deneyebilirsiniz.'
3. Link formatını sadece sistemde mevcut olan tarifler için kullan. Genel öneriler için normal metin kullan.
4. Önerdiğin tariflerin neden uygun olduğunu açıkla (örn: 'Düşük kalorili bir seçenek istediğiniz için 350 kalori olan [X](recipe:ID) tarifini öneririm').
5. Eğer sistemde uygun bir tarif yoksa, genel bir mutfak bilgisi ver veya 'Şu an tam olarak bunu içeren bir tarifim yok ama benzer bir [X](recipe:ID) yapabiliriz' gibi yapıcı ol.
6. Uygulama hakkında bilgi sorulursa: KitchenHelper videoları içe aktararak otomatik tarif çıkaran, alışveriş listesi oluşturan ve besin değerlerini hesaplayan modern bir mutfak asistanıdır.
7. Samimi, yardımsever ve kısa/öz cevaplar ver.
8. Yanıtlarını kullanıcının diliyle ver (Türkçe ise Türkçe, İngilizce ise İngilizce).
";

    public GeminiChatService(
        ILogger<GeminiChatService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IRecipeService recipeService)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _recipeService = recipeService;
        _apiKey = configuration["OpenRouter:ApiKey"] 
            ?? throw new InvalidOperationException("OpenRouter API key not configured");
        _baseUrl = configuration["OpenRouter:BaseUrl"] 
            ?? "https://openrouter.ai/api/v1/chat/completions";
        _model = configuration["OpenRouter:Model"] 
            ?? "google/gemini-2.0-flash-001";
    }

    public async Task<string> GetChatResponseAsync(string userMessage)
    {
        _logger.LogInformation("Processing chat message: {Message}", userMessage);

        try
        {
            var recipes = await _recipeService.GetAllAsync();
            var recipesListText = string.Join("\n", recipes.Select(r => 
                $"- ID: {r.Id}, Başlık: {r.Title}, Açıklama: {r.Description?.Substring(0, Math.Min(r.Description?.Length ?? 0, 100))}..."));

            var systemPrompt = string.Format(SystemPromptTemplate, recipesListText);

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new 
                    { 
                        role = "user", 
                        content = $"{systemPrompt}\n\nKullanıcı Sorusu: {userMessage}" 
                    }
                },
                temperature = 0.7,
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Headers.Add("HTTP-Referer", "https://kitchenhelper.local");
            request.Headers.Add("X-Title", "KitchenHelper");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenRouter API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return "Üzgünüm, şu an bağlantı kuramıyorum (API Hatası). Lütfen daha sonra tekrar deneyin.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(responseJson);

            var choices = result.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return "Anlayamadım, lütfen farklı bir şekilde sorar mısın?";
            }

            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            return $"Bir hata oluştu: {ex.Message}";
        }
    }
}
