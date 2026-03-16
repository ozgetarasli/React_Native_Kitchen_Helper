using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kitchenhelper.Infrastructure.Services;

public class GeminiNutritionService : INutritionService
{
    private readonly ILogger<GeminiNutritionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    private const string NutritionPrompt = @"Sen bir beslenme uzmanı ve diyetisyensin. Aşağıdaki yemek tarifi başlığı ve malzeme listesine bakarak bu tarifin TOPLAM besin değerlerini tahmin et.
    
Tarif Başlığı: {0}
Malzemeler:
{1}

KRİTİK KURALLAR:
1. SADECE geçerli JSON çıktısı ver - açıklama, markdown veya ek metin YASAK.
2. Tahminlerin mantıklı ve gerçekçi olsun.
3. Değerler tarifin TAMAMI (tüm porsiyonlar toplamı) için olmalıdır.
4. Eğer miktar belirtilmemişse, standart bir porsiyon veya mantıklı bir miktar varsay.

JSON ŞEMASI (bu yapıya KESINLIKLE uy):
{{
  ""calories"": number,
  ""protein"": number,
  ""fat"": number,
  ""carbs"": number
}}

SADECE JSON ÇIKTISI (başka hiçbir şey yazma):";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiNutritionService(
        ILogger<GeminiNutritionService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["OpenRouter:ApiKey"] 
            ?? throw new InvalidOperationException("OpenRouter API key not configured");
        _baseUrl = configuration["OpenRouter:BaseUrl"] 
            ?? "https://openrouter.ai/api/v1/chat/completions";
        _model = configuration["OpenRouter:Model"] 
            ?? "google/gemini-2.0-flash-001";
    }

    public async Task<NutritionInfo> CalculateNutritionAsync(string title, List<string> ingredients)
    {
        _logger.LogInformation("Calculating nutrition for recipe: {Title}", title);

        var ingredientsText = string.Join("\n", ingredients);
        var prompt = string.Format(NutritionPrompt, title, ingredientsText);

        try
        {
            var rawResponse = await CallGeminiAsync(prompt);
            var cleanedJson = CleanJsonResponse(rawResponse);
            
            var nutrition = JsonSerializer.Deserialize<NutritionInfo>(cleanedJson, JsonOptions);
            return nutrition ?? new NutritionInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate nutrition for {Title}", title);
            return new NutritionInfo(); // Return empty if failed
        }
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.1,
            max_tokens = 500
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
            throw new Exception($"LLM API hatası: {response.StatusCode}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(responseJson);

        var choices = result.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new Exception("LLM yanıt üretemedi");
        }

        var responseContent = choices[0].GetProperty("message").GetProperty("content").GetString();
        
        if (string.IsNullOrEmpty(responseContent))
        {
            throw new Exception("LLM boş yanıt döndürdü");
        }

        return responseContent;
    }

    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "{}";

        var cleaned = Regex.Replace(response, @"```json\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"```\s*", "");
        cleaned = cleaned.Trim();

        var startIndex = cleaned.IndexOf('{');
        var endIndex = cleaned.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            cleaned = cleaned.Substring(startIndex, endIndex - startIndex + 1);
        }

        return cleaned;
    }
}
