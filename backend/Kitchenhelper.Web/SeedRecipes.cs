using Kitchenhelper.Core.Entities;
using Kitchenhelper.Infrastructure.Data;
using Kitchenhelper.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kitchenhelper.Web;

public static class SeedRecipes
{
    public static async Task<int> SeedAsync(AppDbContext db, string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            Console.WriteLine($"❌ Hata: JSON dosyası bulunamadı: {jsonFilePath}");
            return 1;
        }

        Console.WriteLine($"📖 JSON dosyası okunuyor: {jsonFilePath}");

        string jsonContent;
        try
        {
            jsonContent = await File.ReadAllTextAsync(jsonFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ JSON dosyası okunamadı: {ex.Message}");
            return 1;
        }

        List<RecipeCreateRequest>? recipes;
        try
        {
            recipes = JsonSerializer.Deserialize<List<RecipeCreateRequest>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ JSON parse hatası: {ex.Message}");
            return 1;
        }

        if (recipes == null || recipes.Count == 0)
        {
            Console.WriteLine("⚠️ JSON dosyasında tarif bulunamadı.");
            return 1;
        }

        Console.WriteLine($"📋 {recipes.Count} tarif bulundu. Veritabanına yükleniyor...");

        int added = 0;
        int skipped = 0;
        int updated = 0;

        foreach (var recipeRequest in recipes)
        {
            if (string.IsNullOrWhiteSpace(recipeRequest.Title))
            {
                Console.WriteLine($"⚠️ Başlıksız tarif atlandı.");
                skipped++;
                continue;
            }

            // Duplicate kontrolü - aynı title'a sahip sistem tarifi var mı?
            var existing = await db.Recipes
                .FirstOrDefaultAsync(r => r.Title == recipeRequest.Title.Trim() && r.IsSystemRecipe);

            if (existing != null)
            {
                // Mevcut tarif varsa, güncelle
                bool wasUpdated = false;
                
                var newImagePath = string.IsNullOrWhiteSpace(recipeRequest.Image) ? null : recipeRequest.Image.Trim();
                if (existing.ImagePath != newImagePath)
                {
                    existing.ImagePath = newImagePath;
                    wasUpdated = true;
                }
                
                // PrepTime güncelle
                if (existing.PrepTime != recipeRequest.PrepTime)
                {
                    existing.PrepTime = recipeRequest.PrepTime;
                    wasUpdated = true;
                }
                
                // Servings güncelle
                if (existing.Servings != recipeRequest.Servings)
                {
                    existing.Servings = recipeRequest.Servings;
                    wasUpdated = true;
                }
                
                // Categories güncelle
                if (recipeRequest.Categories != null && recipeRequest.Categories.Count > 0)
                {
                    var newCategories = System.Text.Json.JsonSerializer.Serialize(recipeRequest.Categories);
                    if (existing.Categories != newCategories)
                    {
                        existing.Categories = newCategories;
                        wasUpdated = true;
                    }
                }
                
                if (wasUpdated)
                {
                    await db.SaveChangesAsync();
                    Console.WriteLine($"🔄 Güncellendi: {recipeRequest.Title}");
                    updated++;
                }
                else
                {
                    Console.WriteLine($"⏭️  Zaten mevcut: {recipeRequest.Title}");
                    skipped++;
                }
                continue;
            }

            try
            {
                var recipe = new Recipe
                {
                    Title = recipeRequest.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(recipeRequest.Description)
                        ? null
                        : recipeRequest.Description.Trim(),
                    StepsMarkdown = ResolveStepsMarkdown(recipeRequest),
                    IsFavorite = recipeRequest.IsFavorite,
                    IsSystemRecipe = true,
                    ImagePath = string.IsNullOrWhiteSpace(recipeRequest.Image) ? null : recipeRequest.Image.Trim(),
                    PrepTime = recipeRequest.PrepTime,
                    Servings = recipeRequest.Servings
                };

                // Kategorileri set et
                if (recipeRequest.Categories != null && recipeRequest.Categories.Count > 0)
                {
                    recipe.SetCategories(recipeRequest.Categories);
                }

                recipe.RecipeIngredients = await BuildRecipeIngredientsAsync(db, recipeRequest.RecipeIngredients);

                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();

                Console.WriteLine($"✅ Yüklendi: {recipe.Title}");
                added++;
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"❌ Hata ({recipeRequest.Title}): {errorMsg}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                skipped++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"✨ Tamamlandı!");
        Console.WriteLine($"   ✅ Yüklenen: {added}");
        Console.WriteLine($"   🔄 Güncellenen: {updated}");
        Console.WriteLine($"   ⏭️  Atlanan: {skipped}");
        Console.WriteLine($"   📊 Toplam: {recipes.Count}");

        return 0;
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

    private static async Task<List<RecipeIngredient>> BuildRecipeIngredientsAsync(
        AppDbContext db,
        List<RecipeIngredientRequest>? items)
    {
        if (items is null || items.Count == 0)
            return new List<RecipeIngredient>();

        var normalized = items
            .Where(i => !string.IsNullOrWhiteSpace(i?.Ingredient?.Name))
            .Select(i => new
            {
                Name = i!.Ingredient!.Name!.Trim(),
                Quantity = i.Quantity,
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit!.Trim()
            })
            .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return new List<RecipeIngredient>();

        var names = normalized.Select(i => i.Name).ToList();
        var existing = await db.Ingredients
            .Where(i => names.Contains(i.Name))
            .ToDictionaryAsync(i => i.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<RecipeIngredient>();

        foreach (var item in normalized)
        {
            if (!existing.TryGetValue(item.Name, out var ingredient))
            {
                ingredient = new Ingredient { Name = item.Name };
                db.Ingredients.Add(ingredient);
                existing[item.Name] = ingredient;
            }

            result.Add(new RecipeIngredient
            {
                Ingredient = ingredient,
                Unit = item.Unit,
                Quantity = item.Quantity
            });
        }

        return result;
    }
}

