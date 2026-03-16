using Kitchenhelper.Core.Models;
using Kitchenhelper.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Recipe draft extractor using OpenRouter LLM API.
/// Converts raw transcript text into structured recipe JSON.
/// </summary>
public class GeminiDraftExtractor : IRecipeDraftExtractor
{
    private readonly ILogger<GeminiDraftExtractor> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    // Strict extraction prompt with flags for uncertain data (TURKISH)
    private const string ExtractionPromptTR = @"Sen bir yemek tarifi çıkarma asistanısın. Aşağıdaki transkriptten yapılandırılmış bir tarif JSON'ı çıkar.

KRİTİK KURALLAR:
1. SADECE geçerli JSON çıktısı ver - açıklama, markdown veya ek metin YASAK
2. Bilinmeyen değerler için null veya boş array kullan
3. Türkçe malzeme isimlerini olduğu gibi koru, normalize etme
4. Belirsiz ifadeleri ""note"" alanına yaz
5. Adımları mantıklı sıraya koy, order 1'den başlasın
6. BELİRSİZ ÖLÇÜLER için flags ekle (örn: ""bir tutam"", ""yeteri kadar"", ""göz kararı"")
7. EKSİK SÜRELER için flags ekle (timeHintMin null ise missingTime: true)

JSON ŞEMASI (bu yapıya KESINLIKLE uy):
{{
  ""title"": ""string (tarif adı)"",
  ""servings"": number|null,
  ""prepTimeMin"": number|null,
  ""cookTimeMin"": number|null,
  ""totalTimeMin"": number|null,
  ""ingredients"": [
    {{
      ""name"":""string"",
      ""quantity"":number|null,
      ""unit"":""string|null"",
      ""note"":""string|null"",
      ""flags"": {{
        ""uncertainQuantity"": boolean (""bir tutam"", ""yeteri kadar"", ""biraz"" gibi belirsiz ifadeler için true),
        ""missingUnit"": boolean (birim yoksa true),
        ""originalText"": ""string|null (parse edilemeyen orijinal metin)""
      }}|null
    }}
  ],
  ""steps"": [
    {{
      ""order"":number,
      ""text"":""string"",
      ""timeHintMin"":number|null,
      ""flags"": {{
        ""missingTime"": boolean (süre belirtilmemişse true),
        ""uncertainTemperature"": boolean (""orta ateş"", ""kısık ateş"" gibi belirsiz sıcaklık için true),
        ""incomplete"": boolean (adım eksik/belirsiz görünüyorsa true)
      }}|null
    }}
  ],
  ""tips"": [""string""],
  ""calories"": number|null,
  ""protein"": number|null,
  ""fat"": number|null,
  ""carbs"": number|null,
  ""sourceNotes"": {{""language"":""tr"",""confidence"":0.0-1.0}}
}}

ÖNEMLİ: Belirsiz ölçüler (""bir tutam tuz"", ""yeteri kadar su"") için:
- quantity: null yap
- flags.uncertainQuantity: true yap
- flags.originalText: orijinal ifadeyi yaz

TRANSKRİPT:
{0}

SADECE JSON ÇIKTISI (başka hiçbir şey yazma):";

    // English version of extraction prompt
    private const string ExtractionPromptEN = @"You are a recipe extraction assistant. Extract a structured recipe JSON from the following transcript.

CRITICAL RULES:
1. Output ONLY valid JSON - NO explanations, markdown, or additional text
2. Use null or empty array for unknown values
3. Keep ingredient names in English, normalize them where possible
4. Write ambiguous expressions in the ""note"" field
5. Order steps logically, starting from 1
6. Add flags for UNCERTAIN MEASUREMENTS (e.g. ""a pinch"", ""to taste"", ""a handful"")
7. Add flags for MISSING TIMES (if timeHintMin is null, set missingTime: true)

JSON SCHEMA (STRICTLY follow this structure):
{{
  ""title"": ""string (recipe name)"",
  ""servings"": number|null,
  ""prepTimeMin"": number|null,
  ""cookTimeMin"": number|null,
  ""totalTimeMin"": number|null,
  ""ingredients"": [
    {{
      ""name"":""string"",
      ""quantity"":number|null,
      ""unit"":""string|null"",
      ""note"":""string|null"",
      ""flags"": {{
        ""uncertainQuantity"": boolean (true for vague expressions like ""a pinch"", ""to taste"", ""some""),
        ""missingUnit"": boolean (true if no unit specified),
        ""originalText"": ""string|null (original text that couldn't be parsed)""
      }}|null
    }}
  ],
  ""steps"": [
    {{
      ""order"":number,
      ""text"":""string"",
      ""timeHintMin"":number|null,
      ""flags"": {{
        ""missingTime"": boolean (true if no time specified),
        ""uncertainTemperature"": boolean (true for vague temperatures like ""medium heat"", ""low heat""),
        ""incomplete"": boolean (true if step seems incomplete/unclear)
      }}|null
    }}
  ],
  ""tips"": [""string""],
  ""calories"": number|null,
  ""protein"": number|null,
  ""fat"": number|null,
  ""carbs"": number|null,
  ""sourceNotes"": {{""language"":""en"",""confidence"":0.0-1.0}}
}}

IMPORTANT: For uncertain measurements (""a pinch of salt"", ""enough water""):
- Set quantity: null
- Set flags.uncertainQuantity: true
- Set flags.originalText: original expression

TRANSCRIPT:
{0}

ONLY JSON OUTPUT (write nothing else):";

    // Repair prompt for retry (Turkish)
    private const string RepairPromptTR = @"Aşağıdaki JSON geçersiz veya şemaya uymuyor. Düzelt ve SADECE geçerli JSON döndür.

BEKLENEN ŞEMA:
{{
  ""title"": ""string"",
  ""servings"": number|null,
  ""prepTimeMin"": number|null,
  ""cookTimeMin"": number|null,
  ""totalTimeMin"": number|null,
  ""ingredients"": [{{
    ""name"":""string"",
    ""quantity"":number|null,
    ""unit"":""string|null"",
    ""note"":""string|null"",
    ""flags"": {{""uncertainQuantity"":boolean,""missingUnit"":boolean,""originalText"":""string|null""}}|null
  }}],
  ""steps"": [{{
    ""order"":number,
    ""text"":""string"",
    ""timeHintMin"":number|null,
    ""flags"": {{""missingTime"":boolean,""uncertainTemperature"":boolean,""incomplete"":boolean}}|null
  }}],
    ""tips"": [""string""],
    ""calories"": number|null,
    ""protein"": number|null,
    ""fat"": number|null,
    ""carbs"": number|null,
    ""sourceNotes"": {{""language"":""string"",""confidence"":number}}
}}

HATALI JSON:
{0}

HATALAR:
{1}

DÜZELTİLMİŞ JSON (SADECE JSON, başka bir şey yazma):";

    // Repair prompt for retry (English)
    private const string RepairPromptEN = @"The following JSON is invalid or doesn't match the schema. Fix it and return ONLY valid JSON.

EXPECTED SCHEMA:
{{
  ""title"": ""string"",
  ""servings"": number|null,
  ""prepTimeMin"": number|null,
  ""cookTimeMin"": number|null,
  ""totalTimeMin"": number|null,
  ""ingredients"": [{{
    ""name"":""string"",
    ""quantity"":number|null,
    ""unit"":""string|null"",
    ""note"":""string|null"",
    ""flags"": {{""uncertainQuantity"":boolean,""missingUnit"":boolean,""originalText"":""string|null""}}|null
  }}],
  ""steps"": [{{
    ""order"":number,
    ""text"":""string"",
    ""timeHintMin"":number|null,
    ""flags"": {{""missingTime"":boolean,""uncertainTemperature"":boolean,""incomplete"":boolean}}|null
  }}],
    ""tips"": [""string""],
    ""calories"": number|null,
    ""protein"": number|null,
    ""fat"": number|null,
    ""carbs"": number|null,
    ""sourceNotes"": {{""language"":""string"",""confidence"":number}}
}}

INVALID JSON:
{0}

ERRORS:
{1}

FIXED JSON (ONLY JSON, write nothing else):";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public GeminiDraftExtractor(
        ILogger<GeminiDraftExtractor> logger,
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
            ?? "google/gemini-2.0-flash-exp:free";
    }

    public async Task<string> ExtractDraftAsync(string transcriptText, string language = "tr")
    {
        _logger.LogInformation("Starting draft extraction from transcript ({Length} chars, lang: {Lang})", transcriptText.Length, language);

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new DraftExtractionException(language == "en" ? "Transcript is empty" : "Transcript boş");
        }

        // Select prompt based on language
        var extractionPrompt = language == "en" ? ExtractionPromptEN : ExtractionPromptTR;
        var repairPromptTemplate = language == "en" ? RepairPromptEN : RepairPromptTR;

        // First attempt
        var prompt = string.Format(extractionPrompt, transcriptText);
        var rawResponse = await CallGeminiAsync(prompt);
        var cleanedJson = CleanJsonResponse(rawResponse);

        var validation = ValidateDraftJson(cleanedJson);
        if (validation.IsValid)
        {
            _logger.LogInformation("Draft extraction successful on first attempt");
            return cleanedJson;
        }

        _logger.LogWarning("First extraction attempt failed validation: {Errors}", 
            string.Join(", ", validation.Errors));

        // Retry with repair prompt
        var repairPromptText = string.Format(repairPromptTemplate, cleanedJson, string.Join("\n", validation.Errors));
        var repairedResponse = await CallGeminiAsync(repairPromptText);
        var repairedJson = CleanJsonResponse(repairedResponse);

        var retryValidation = ValidateDraftJson(repairedJson);
        if (retryValidation.IsValid)
        {
            _logger.LogInformation("Draft extraction successful after repair");
            return repairedJson;
        }

        _logger.LogError("Draft extraction failed after repair attempt: {Errors}", 
            string.Join(", ", retryValidation.Errors));

        throw new DraftExtractionException(
            language == "en" 
                ? "Recipe extraction failed. Please check the transcript." 
                : "Tarif çıkarma başarısız oldu. Lütfen transkripti kontrol edin.",
            repairedJson,
            retryValidation.Errors);
    }

    public DraftValidationResult ValidateDraftJson(string json)
    {
        var errors = new List<string>();

        // 1. JSON parse check
        RecipeDraftSchema? draft;
        try
        {
            draft = JsonSerializer.Deserialize<RecipeDraftSchema>(json, JsonOptions);
            if (draft == null)
            {
                return DraftValidationResult.Failure("JSON parse edilemedi");
            }
        }
        catch (JsonException ex)
        {
            return DraftValidationResult.Failure($"Geçersiz JSON: {ex.Message}");
        }

        // 2. Required fields check
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            errors.Add("title alanı zorunlu ve boş olamaz");
        }

        if (draft.Ingredients == null || draft.Ingredients.Count == 0)
        {
            errors.Add("ingredients alanı zorunlu ve en az 1 malzeme içermeli");
        }
        else
        {
            // Check each ingredient has a name
            for (int i = 0; i < draft.Ingredients.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(draft.Ingredients[i].Name))
                {
                    errors.Add($"ingredients[{i}].name boş olamaz");
                }
            }
        }

        if (draft.Steps == null || draft.Steps.Count == 0)
        {
            errors.Add("steps alanı zorunlu ve en az 1 adım içermeli");
        }
        else
        {
            // Check step order is continuous starting from 1
            var expectedOrder = 1;
            foreach (var step in draft.Steps.OrderBy(s => s.Order))
            {
                if (step.Order != expectedOrder)
                {
                    errors.Add($"steps sıralaması 1'den başlamalı ve ardışık olmalı. Beklenen: {expectedOrder}, Bulunan: {step.Order}");
                    break;
                }
                if (string.IsNullOrWhiteSpace(step.Text))
                {
                    errors.Add($"steps[{step.Order - 1}].text boş olamaz");
                }
                expectedOrder++;
            }
        }

        return errors.Count == 0 
            ? DraftValidationResult.Success() 
            : DraftValidationResult.Failure(errors.ToArray());
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
            max_tokens = 8192
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
            throw new DraftExtractionException($"LLM API hatası: {response.StatusCode}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(responseJson);

        // OpenRouter uses OpenAI format: choices[0].message.content
        var choices = result.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new DraftExtractionException("LLM yanıt üretemedi");
        }

        var firstChoice = choices[0];
        var message = firstChoice.GetProperty("message");
        var responseContent = message.GetProperty("content").GetString();
        
        if (string.IsNullOrEmpty(responseContent))
        {
            throw new DraftExtractionException("LLM boş yanıt döndürdü");
        }

        return responseContent;
    }

    /// <summary>
    /// Cleans LLM response to extract pure JSON
    /// </summary>
    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "{}";

        // Remove markdown code blocks
        var cleaned = Regex.Replace(response, @"```json\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"```\s*", "");
        cleaned = cleaned.Trim();

        // Try to find JSON object boundaries
        var startIndex = cleaned.IndexOf('{');
        var endIndex = cleaned.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            cleaned = cleaned.Substring(startIndex, endIndex - startIndex + 1);
        }

        return cleaned;
    }
}
