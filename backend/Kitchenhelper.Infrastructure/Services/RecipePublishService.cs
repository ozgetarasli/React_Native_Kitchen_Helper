using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Models;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Kitchenhelper.Infrastructure.Services;

/// <summary>
/// Service for publishing drafts to normalized recipe tables
/// </summary>
public class RecipePublishService : IRecipePublishService
{
    private readonly AppDbContext _db;
    private readonly IRecipeDraftService _draftService;
    private readonly INutritionService _nutritionService;
    private readonly ILogger<RecipePublishService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Unit normalization mapping
    private static readonly Dictionary<string, string> UnitNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        // Volume
        { "bardak", "bardak" },
        { "su bardağı", "bardak" },
        { "çay bardağı", "çay bardağı" },
        { "fincan", "fincan" },
        { "lt", "litre" },
        { "l", "litre" },
        { "litre", "litre" },
        { "ml", "ml" },
        { "mililitre", "ml" },
        
        // Weight
        { "gr", "g" },
        { "gram", "g" },
        { "g", "g" },
        { "kg", "kg" },
        { "kilogram", "kg" },
        
        // Spoon
        { "yemek kaşığı", "yemek kaşığı" },
        { "yk", "yemek kaşığı" },
        { "çay kaşığı", "çay kaşığı" },
        { "çk", "çay kaşığı" },
        { "tatlı kaşığı", "tatlı kaşığı" },
        
        // Count
        { "adet", "adet" },
        { "tane", "adet" },
        { "ad", "adet" },
        { "baş", "baş" },
        { "diş", "diş" },
        { "dal", "dal" },
        { "demet", "demet" },
        { "dilim", "dilim" },
        { "paket", "paket" },
        { "pk", "paket" },
        { "poşet", "poşet" },
        { "tutam", "tutam" },
        { "avuç", "avuç" }
    };

    public RecipePublishService(
        AppDbContext db,
        IRecipeDraftService draftService,
        INutritionService nutritionService,
        ILogger<RecipePublishService> logger)
    {
        _db = db;
        _draftService = draftService;
        _nutritionService = nutritionService;
        _logger = logger;
    }

    public async Task<Recipe> PublishDraftAsync(int draftId, int userId)
    {
        _logger.LogInformation("Publishing draft {DraftId} for user {UserId}", draftId, userId);

        // Get draft with user validation
        var draft = await _draftService.GetByIdAsync(draftId, userId);
        if (draft == null)
        {
            throw new PublishException("Draft bulunamadı veya erişim yetkiniz yok");
        }

        if (draft.Status == DraftStatus.Published)
        {
            throw new PublishException("Bu draft zaten yayınlanmış");
        }

        // Parse draft JSON
        RecipeDraftSchema draftSchema;
        try
        {
            draftSchema = JsonSerializer.Deserialize<RecipeDraftSchema>(draft.DraftJson, JsonOptions)
                ?? throw new PublishException("Draft JSON parse edilemedi");
        }
        catch (JsonException ex)
        {
            throw new PublishException($"Geçersiz draft JSON: {ex.Message}");
        }

        // Validate required fields
        var validationErrors = ValidateForPublish(draftSchema);
        if (validationErrors.Count > 0)
        {
            throw new PublishException("Yayınlama validasyonu başarısız", validationErrors);
        }

        // Create recipe using transaction
        using var transaction = await _db.Database.BeginTransactionAsync();
        
        try
        {
            // Nutrition calculation if missing
            if (draftSchema.Calories == null)
            {
                try
                {
                    var ingredientsList = draftSchema.Ingredients
                        .Select(i => $"{(i.Quantity.HasValue ? i.Quantity.Value.ToString() : "")} {i.Unit ?? ""} {i.Name} {i.Note ?? ""}".Trim())
                        .ToList();
                    
                    var nutrition = await _nutritionService.CalculateNutritionAsync(draftSchema.Title, ingredientsList);
                    draftSchema.Calories = nutrition.Calories;
                    draftSchema.Protein = nutrition.Protein;
                    draftSchema.Fat = nutrition.Fat;
                    draftSchema.Carbs = nutrition.Carbs;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-calculate nutrition during publish for draft {DraftId}", draftId);
                }
            }

            // Create Recipe
            var recipe = new Recipe
            {
                Title = draftSchema.Title.Trim(),
                Description = GenerateDescription(draftSchema),
                StepsMarkdown = GenerateStepsMarkdown(draftSchema),
                PrepTime = FormatTime(draftSchema.PrepTimeMin, draftSchema.CookTimeMin, draftSchema.TotalTimeMin),
                Servings = draftSchema.Servings,
                Categories = "[]", // Default empty
                IsFavorite = false,
                IsSystemRecipe = false,
                SourceUrl = draftSchema.SourceUrl ?? draft.Import?.SourceUrl,
                Calories = draftSchema.Calories,
                Protein = draftSchema.Protein,
                Fat = draftSchema.Fat,
                Carbs = draftSchema.Carbs
            };

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

            // Process ingredients with deduplication
            var processedIngredients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var draftIngredient in draftSchema.Ingredients)
            {
                var normalizedName = NormalizeIngredientName(draftIngredient.Name);
                
                // Skip duplicates within this recipe
                if (processedIngredients.Contains(normalizedName))
                {
                    _logger.LogWarning("Skipping duplicate ingredient: {Name}", normalizedName);
                    continue;
                }
                processedIngredients.Add(normalizedName);

                // Find or create ingredient
                var ingredient = await _db.Ingredients
                    .FirstOrDefaultAsync(i => i.Name.ToLower() == normalizedName.ToLower());

                if (ingredient == null)
                {
                    ingredient = new Ingredient { Name = normalizedName };
                    _db.Ingredients.Add(ingredient);
                    await _db.SaveChangesAsync();
                }

                // Create recipe-ingredient junction
                var recipeIngredient = new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = ingredient.Id,
                    Quantity = draftIngredient.Quantity,
                    Unit = NormalizeUnit(draftIngredient.Unit)
                };

                _db.RecipeIngredients.Add(recipeIngredient);
            }

            await _db.SaveChangesAsync();

            // Update import with recipe ID
            if (draft.Import != null)
            {
                draft.Import.RecipeId = recipe.Id;
                await _db.SaveChangesAsync();
            }

            // Mark draft as published
            await _draftService.MarkAsPublishedAsync(draftId, recipe.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Successfully published draft {DraftId} as Recipe {RecipeId}", 
                draftId, recipe.Id);

            return recipe;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to publish draft {DraftId}", draftId);
            throw new PublishException($"Yayınlama sırasında hata: {ex.Message}");
        }
    }

    private List<string> ValidateForPublish(RecipeDraftSchema draft)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Title))
            errors.Add("Tarif adı zorunludur");

        if (draft.Title?.Length > 100)
            errors.Add("Tarif adı 100 karakterden uzun olamaz");

        if (draft.Ingredients == null || draft.Ingredients.Count == 0)
            errors.Add("En az bir malzeme gereklidir");

        if (draft.Steps == null || draft.Steps.Count == 0)
            errors.Add("En az bir adım gereklidir");

        return errors;
    }

    private string NormalizeIngredientName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // Trim and normalize whitespace
        var normalized = name.Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

        // First letter uppercase, rest lowercase (for Turkish)
        if (normalized.Length > 0)
        {
            normalized = char.ToUpper(normalized[0]) + normalized.Substring(1).ToLower();
        }

        return normalized;
    }

    private string? NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return null;

        var trimmed = unit.Trim().ToLower();

        if (UnitNormalization.TryGetValue(trimmed, out var normalized))
            return normalized;

        return unit.Trim();
    }

    private string GenerateDescription(RecipeDraftSchema draft)
    {
        var parts = new List<string>();

        if (draft.Servings.HasValue)
            parts.Add($"{draft.Servings} kişilik");

        if (draft.TotalTimeMin.HasValue)
            parts.Add($"{draft.TotalTimeMin} dakika");
        else if (draft.PrepTimeMin.HasValue || draft.CookTimeMin.HasValue)
        {
            var total = (draft.PrepTimeMin ?? 0) + (draft.CookTimeMin ?? 0);
            if (total > 0)
                parts.Add($"~{total} dakika");
        }

        parts.Add($"{draft.Ingredients.Count} malzeme");
        parts.Add($"{draft.Steps.Count} adım");

        return string.Join(" • ", parts);
    }

    private string GenerateStepsMarkdown(RecipeDraftSchema draft)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var step in draft.Steps.OrderBy(s => s.Order))
        {
            sb.AppendLine($"{step.Order}. {step.Text}");
            
            if (step.TimeHintMin.HasValue)
            {
                sb.AppendLine($"   ⏱️ ~{step.TimeHintMin} dakika");
            }
            
            sb.AppendLine();
        }

        // Add tips if any
        if (draft.Tips != null && draft.Tips.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("**İpuçları:**");
            
            foreach (var tip in draft.Tips)
            {
                sb.AppendLine($"- {tip}");
            }
        }

        return sb.ToString().Trim();
    }

    private string? FormatTime(int? prepMin, int? cookMin, int? totalMin)
    {
        if (totalMin.HasValue)
            return $"{totalMin} dakika";

        var parts = new List<string>();
        
        if (prepMin.HasValue)
            parts.Add($"Hazırlık: {prepMin} dk");
        
        if (cookMin.HasValue)
            parts.Add($"Pişirme: {cookMin} dk");

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
