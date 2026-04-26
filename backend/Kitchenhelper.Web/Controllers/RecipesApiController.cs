using Kitchenhelper.Core.Services;
using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Models;
using Kitchenhelper.Infrastructure.Data;
using Kitchenhelper.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kitchenhelper.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("api")]
public class RecipesApiController : ControllerBase
{
    private readonly IRecipeService _recipeService;
    private readonly INutritionService _nutritionService;
    private readonly AppDbContext _db;
    private readonly ILogger<RecipesApiController> _logger;

    public RecipesApiController(IRecipeService recipeService, INutritionService nutritionService, AppDbContext db, ILogger<RecipesApiController> logger)
    {
        _recipeService = recipeService;
        _nutritionService = nutritionService;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var recipes = await _recipeService.GetAllAsync();
        
        // Debug: Malzeme sayısını kontrol et
        var recipeCount = recipes.Count;
        var ingredientCounts = recipes.Select(r => new { r.Title, IngredientCount = r.RecipeIngredients.Count }).ToList();
        System.Console.WriteLine($"📊 Toplam tarif: {recipeCount}");
        foreach (var rc in ingredientCounts)
        {
            System.Console.WriteLine($"  - {rc.Title}: {rc.IngredientCount} malzeme");
        }

        var result = recipes.Select(r => new
        {
            id = r.Id,
            title = r.Title,
            description = r.Description,
            isFavorite = r.IsFavorite,
            prepTime = r.PrepTime ?? "30 mins",
            servings = r.Servings ?? 4,
            categories = r.GetCategories().ToArray(),
            image = SanitizeImagePath(r.ImagePath),
            sourceUrl = r.SourceUrl ?? "",
            calories = r.Calories,
            protein = r.Protein,
            fat = r.Fat,
            carbs = r.Carbs,
            ingredients = r.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }).ToList(),
            instructions = !string.IsNullOrEmpty(r.StepsMarkdown)
                ? r.StepsMarkdown.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray()
                : new string[] { }
        }).ToList();

        return Ok(result);
    }

    [HttpGet("favorites")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFavorites()
    {
        var recipes = await _db.Recipes
            .Where(r => r.IsFavorite)
            .Include(r => r.RecipeIngredients)
            .ThenInclude(ri => ri.Ingredient)
            .ToListAsync();

        var result = recipes.Select(r => new
        {
            id = r.Id,
            title = r.Title,
            description = r.Description,
            isFavorite = r.IsFavorite,
            prepTime = r.PrepTime ?? "30 mins",
            servings = r.Servings ?? 4,
            categories = r.GetCategories().ToArray(),
            image = SanitizeImagePath(r.ImagePath),
            sourceUrl = r.SourceUrl ?? "",
            calories = r.Calories,
            protein = r.Protein,
            fat = r.Fat,
            carbs = r.Carbs,
            ingredients = r.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }).ToList(),
            instructions = !string.IsNullOrEmpty(r.StepsMarkdown)
                ? r.StepsMarkdown.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray()
                : new string[] { }
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var recipe = await _recipeService.GetAsync(id);
        if (recipe == null)
            return NotFound();

        var result = new
        {
            id = recipe.Id,
            title = recipe.Title,
            description = recipe.Description,
            isFavorite = recipe.IsFavorite,
            prepTime = recipe.PrepTime ?? "30 mins",
            servings = recipe.Servings ?? 4,
            categories = recipe.GetCategories().ToArray(),
            image = SanitizeImagePath(recipe.ImagePath),
            sourceUrl = recipe.SourceUrl ?? "",
            calories = recipe.Calories,
            protein = recipe.Protein,
            fat = recipe.Fat,
            carbs = recipe.Carbs,
            ingredients = recipe.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }).ToList(),
            instructions = !string.IsNullOrEmpty(recipe.StepsMarkdown)
                ? recipe.StepsMarkdown.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray()
                : new string[] { }
        };

        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchByIngredients([FromBody] SearchRequest request)
    {
        if (request == null || request.have == null)
            return BadRequest("have array is required");

        var matches = await _recipeService.SearchByIngredientsAsync(request.have);

        var result = matches.Select(m => new
        {
            id = m.Recipe.Id,
            title = m.Recipe.Title,
            description = m.Recipe.Description,
            isFavorite = m.Recipe.IsFavorite,
            image = SanitizeImagePath(m.Recipe.ImagePath),
            missingIngredients = m.MissingIngredients,
            ingredients = m.Recipe.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }).ToList()
        }).ToList();

        return Ok(result);
    }

    [HttpPost("{id}/toggle-favorite")]
    [AllowAnonymous]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var recipe = await _db.Recipes.FindAsync(id);
        if (recipe == null)
            return NotFound();

        recipe.IsFavorite = !recipe.IsFavorite;
        await _db.SaveChangesAsync();

        return Ok(new { id = recipe.Id, isFavorite = recipe.IsFavorite });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> AddRecipe([FromBody] RecipeCreateRequest request)
    {
        if (request is null)
            return BadRequest("Gönderim boş olamaz.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");

        // RecipeIngredientRequest'i RecipeIngredientDto'ya map et
        var ingredients = request.RecipeIngredients?
            .Where(i => !string.IsNullOrWhiteSpace(i?.Ingredient?.Name))
            .Select(i => new RecipeIngredientDto
            {
                Name = i!.Ingredient!.Name!.Trim(),
                Quantity = i.Quantity,
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit!.Trim()
            })
            .ToList() ?? new List<RecipeIngredientDto>();

        // IRecipeService kullanarak tarifi oluştur
        var recipeId = await _recipeService.CreateAsync(
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ResolveStepsMarkdown(request),
            ingredients
        );

        // ImagePath ve IsFavorite için güncelleme yap
        var recipe = await _db.Recipes.FindAsync(recipeId);
        if (recipe != null)
        {
            var sanitizedImage = SanitizeImagePath(request.Image);
            recipe.ImagePath = string.IsNullOrWhiteSpace(sanitizedImage) ? null : sanitizedImage;

            recipe.SourceUrl = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim();
            recipe.IsFavorite = request.IsFavorite;
            recipe.PrepTime = request.PrepTime;
            recipe.Servings = request.Servings;
            
            if (request.Categories != null && request.Categories.Count > 0)
            {
                recipe.SetCategories(request.Categories);
            }

            // Auto-calculate nutrition if missing
            if (request.Calories == null && ingredients.Count > 0)
            {
                try
                {
                    var ingredientsList = ingredients
                        .Select(i => $"{(i.Quantity.HasValue ? i.Quantity.Value.ToString() : "")} {i.Unit ?? ""} {i.Name}".Trim())
                        .ToList();
                    var nutrition = await _nutritionService.CalculateNutritionAsync(recipe.Title, ingredientsList);
                    recipe.Calories = nutrition.Calories;
                    recipe.Protein = nutrition.Protein;
                    recipe.Fat = nutrition.Fat;
                    recipe.Carbs = nutrition.Carbs;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"❌ Error calculating nutrition: {ex.Message}");
                }
            }
            else
            {
                recipe.Calories = request.Calories;
                recipe.Protein = request.Protein;
                recipe.Fat = request.Fat;
                recipe.Carbs = request.Carbs;
            }

            await _db.SaveChangesAsync();
        }

        var createdRecipe = await _recipeService.GetAsync(recipeId);
        if (createdRecipe == null)
            return NotFound();

        var response = new
        {
            id = createdRecipe.Id,
            title = createdRecipe.Title,
            description = createdRecipe.Description,
            isFavorite = createdRecipe.IsFavorite,
            prepTime = request.PrepTime ?? "30 mins",
            servings = request.Servings ?? 4,
            categories = request.Categories?.ToArray() ?? Array.Empty<string>(),
            image = SanitizeImagePath(createdRecipe.ImagePath),
            sourceUrl = createdRecipe.SourceUrl ?? "",
            calories = createdRecipe.Calories,
            protein = createdRecipe.Protein,
            fat = createdRecipe.Fat,
            carbs = createdRecipe.Carbs,
            ingredients = createdRecipe.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }),
            instructions = (createdRecipe.StepsMarkdown ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray()
        };

        return CreatedAtAction(nameof(GetById), new { id = createdRecipe.Id }, response);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateRecipe(int id, [FromBody] RecipeCreateRequest request)
    {
        if (request is null)
            return BadRequest("Gönderim boş olamaz.");

        var recipe = await _db.Recipes
            .Include(r => r.RecipeIngredients)
            .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return NotFound();

        // Temel bilgileri güncelle
        recipe.Title = request.Title?.Trim() ?? recipe.Title;
        recipe.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var sanitizedUpdateImage = SanitizeImagePath(request.Image);
        recipe.ImagePath = string.IsNullOrWhiteSpace(sanitizedUpdateImage) ? recipe.ImagePath : sanitizedUpdateImage;
        recipe.PrepTime = request.PrepTime ?? recipe.PrepTime;
        recipe.Servings = request.Servings ?? recipe.Servings;
        recipe.SourceUrl = request.SourceUrl ?? recipe.SourceUrl;
        
        // Auto-calculate nutrition if missing
        if (request.Calories == null && request.RecipeIngredients != null && request.RecipeIngredients.Count > 0)
        {
            try
            {
                var ingredientsList = request.RecipeIngredients
                    .Where(i => !string.IsNullOrWhiteSpace(i?.Ingredient?.Name))
                    .Select(i => $"{(i.Quantity.HasValue ? i.Quantity.Value.ToString() : "")} {i.Unit ?? ""} {i.Ingredient!.Name}".Trim())
                    .ToList();
                var nutrition = await _nutritionService.CalculateNutritionAsync(recipe.Title, ingredientsList);
                recipe.Calories = nutrition.Calories;
                recipe.Protein = nutrition.Protein;
                recipe.Fat = nutrition.Fat;
                recipe.Carbs = nutrition.Carbs;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Error calculating nutrition in update: {ex.Message}");
            }
        }
        else
        {
            recipe.Calories = request.Calories ?? recipe.Calories;
            recipe.Protein = request.Protein ?? recipe.Protein;
            recipe.Fat = request.Fat ?? recipe.Fat;
            recipe.Carbs = request.Carbs ?? recipe.Carbs;
        }

        recipe.StepsMarkdown = ResolveStepsMarkdown(request);
        
        if (request.Categories != null && request.Categories.Count > 0)
        {
            recipe.SetCategories(request.Categories);
        }

        // Mevcut malzemeleri sil
        _db.RecipeIngredients.RemoveRange(recipe.RecipeIngredients);

        // Yeni malzemeleri ekle
        if (request.RecipeIngredients != null)
        {
            foreach (var ri in request.RecipeIngredients.Where(i => !string.IsNullOrWhiteSpace(i?.Ingredient?.Name)))
            {
                var ingredientName = ri!.Ingredient!.Name!.Trim();
                var ingredient = await _db.Ingredients.FirstOrDefaultAsync(i => i.Name == ingredientName);
                
                if (ingredient == null)
                {
                    ingredient = new Ingredient { Name = ingredientName };
                    _db.Ingredients.Add(ingredient);
                    await _db.SaveChangesAsync();
                }

                recipe.RecipeIngredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = ingredient.Id,
                    Quantity = ri.Quantity,
                    Unit = string.IsNullOrWhiteSpace(ri.Unit) ? null : ri.Unit.Trim()
                });
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = recipe.Id,
            title = recipe.Title,
            description = recipe.Description,
            isFavorite = recipe.IsFavorite,
            prepTime = recipe.PrepTime ?? "30 mins",
            servings = recipe.Servings ?? 4,
            categories = recipe.GetCategories().ToArray(),
            image = SanitizeImagePath(recipe.ImagePath),
            sourceUrl = recipe.SourceUrl ?? "",
            calories = recipe.Calories,
            protein = recipe.Protein,
            fat = recipe.Fat,
            carbs = recipe.Carbs,
            ingredients = recipe.RecipeIngredients.Select(ri => new
            {
                name = ri.Ingredient.Name,
                amount = BuildAmountString(ri.Quantity, ri.Unit)
            }),
            instructions = (recipe.StepsMarkdown ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray()
        });
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteRecipe(int id)
    {
        var recipe = await _db.Recipes
            .Include(r => r.RecipeIngredients)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return NotFound();

        // Tarife ait malzemeleri sil
        _db.RecipeIngredients.RemoveRange(recipe.RecipeIngredients);
        
        // Tarifi sil
        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Tarif başarıyla silindi", id });
    }

    [HttpPost("{id}/calculate-nutrition")]
    public async Task<IActionResult> CalculateNutrition(int id)
    {
        var recipe = await _db.Recipes
            .Include(r => r.RecipeIngredients)
            .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);
    
        if (recipe == null)
            return NotFound();
    
        var ingredientsList = recipe.RecipeIngredients
            .Select(ri => $"{(ri.Quantity.HasValue ? ri.Quantity.Value.ToString() : "")} {ri.Unit ?? ""} {ri.Ingredient.Name}".Trim())
            .ToList();
    
        var nutrition = await _nutritionService.CalculateNutritionAsync(recipe.Title, ingredientsList);
    
        recipe.Calories = nutrition.Calories;
        recipe.Protein = nutrition.Protein;
        recipe.Fat = nutrition.Fat;
        recipe.Carbs = nutrition.Carbs;
        await _db.SaveChangesAsync();
    
        return Ok(new
        {
            id = recipe.Id,
            calories = recipe.Calories,
            protein = recipe.Protein,
            fat = recipe.Fat,
            carbs = recipe.Carbs
        });
    }

    [HttpPost("bulk-calculate-nutrition")]
    public async Task<IActionResult> BulkCalculateNutrition()
    {
        var recipes = await _db.Recipes
            .Include(r => r.RecipeIngredients)
            .ThenInclude(ri => ri.Ingredient)
            .Where(r => r.Calories == null)
            .ToListAsync();

        int successCount = 0;
        foreach (var recipe in recipes)
        {
            try
            {
                var ingredientsList = recipe.RecipeIngredients
                    .Select(ri => $"{(ri.Quantity.HasValue ? ri.Quantity.Value.ToString() : "")} {ri.Unit ?? ""} {ri.Ingredient.Name}".Trim())
                    .ToList();

                var nutrition = await _nutritionService.CalculateNutritionAsync(recipe.Title, ingredientsList);

                recipe.Calories = nutrition.Calories;
                recipe.Protein = nutrition.Protein;
                recipe.Fat = nutrition.Fat;
                recipe.Carbs = nutrition.Carbs;
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate nutrition for recipe {Id}: {Title}", recipe.Id, recipe.Title);
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"{successCount} adet tarifin besin değerleri güncellendi.", count = successCount });
    }

    private static string ResolveStepsMarkdown(RecipeCreateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.StepsMarkdown))
            return request.StepsMarkdown;

        if (request.Instructions is null || request.Instructions.Count == 0)
            return "";

        return string.Join("\n", request.Instructions
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// base64 data URI veya bozuk path'leri temizler.
    /// Sadece /uploads/... gibi geçerli relative path'leri döndürür.
    /// </summary>
    private static string SanitizeImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        // base64 data URI'leri geçersiz say
        if (imagePath.Contains("data:image") || imagePath.Contains(";base64")) return "";
        return imagePath;
    }

    private static string BuildAmountString(decimal? quantity, string? unit)
    {
        var hasQuantity = quantity.HasValue;
        var hasUnit = !string.IsNullOrWhiteSpace(unit);

        if (!hasQuantity && !hasUnit)
            return "";

        // Format quantity without trailing zeros (e.g., 200.00 -> 200, 1.5 -> 1.5)
        // Use InvariantCulture to ensure dot separator regardless of server locale
        string formattedQuantity = hasQuantity 
            ? quantity!.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) 
            : "";

        if (hasQuantity && hasUnit)
            return $"{formattedQuantity} {unit}".Trim();

        return hasQuantity
            ? formattedQuantity
            : unit!.Trim();
    }



    [HttpPost("upload-image")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Dosya seçilmedi.");

        // Dosya formatı kontrolü
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest("Sadece resim dosyaları yüklenebilir (jpg, jpeg, png, gif, webp).");

        // Dosya boyutu kontrolü (max 5MB)
        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (file.Length > maxFileSize)
            return BadRequest("Dosya boyutu 5MB'dan büyük olamaz.");

        try
        {
            // wwwroot/uploads klasörünü oluştur
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Benzersiz dosya adı oluştur
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Dosyayı kaydet
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // URL döndür
            var imageUrl = $"/uploads/{uniqueFileName}";
            return Ok(new { imageUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resim yükleme hatası");
            return StatusCode(500, "Dosya yüklenirken beklenmeyen bir hata oluştu.");
        }
    }
}



public class SearchRequest
{
    public string[] have { get; set; } = Array.Empty<string>();
}
