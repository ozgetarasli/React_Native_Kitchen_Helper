using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Kitchenhelper.Core.Models;

namespace Kitchenhelper.Infrastructure.Services;

public class RecipeService : IRecipeService
{
    private readonly AppDbContext _db;
    public RecipeService(AppDbContext db) => _db = db;

    public Task<List<Recipe>> GetAllAsync() =>
        _db.Recipes
           .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
           .OrderBy(r => r.Title).ToListAsync();

    public Task<Recipe?> GetAsync(int id) =>
        _db.Recipes
           .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
           .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<int> CreateAsync(string title, string? desc, string? steps, string ingredientsCsv)
    {
        var r = new Recipe { Title = title, Description = desc, StepsMarkdown = steps };
        r.RecipeIngredients = await BuildRecipeIngredientsAsync(ingredientsCsv);
        _db.Recipes.Add(r);
        await _db.SaveChangesAsync();
        return r.Id;
    }

    public async Task<int> CreateAsync(string title, string? desc, string? steps, List<RecipeIngredientDto> ingredients)
    {
        var r = new Recipe { Title = title, Description = desc, StepsMarkdown = steps };
        r.RecipeIngredients = await BuildRecipeIngredientsAsync(ingredients);
        _db.Recipes.Add(r);
        await _db.SaveChangesAsync();
        return r.Id;
    }

    public async Task UpdateAsync(int id, string title, string? desc, string? steps, string ingredientsCsv)
    {
        var r = await _db.Recipes
                         .Include(x => x.RecipeIngredients)
                         .FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return;

        r.Title = title; r.Description = desc; r.StepsMarkdown = steps;

        // mevcut ilişkileri sıfırla ve yeniden kur
        _db.RecipeIngredients.RemoveRange(r.RecipeIngredients);
        r.RecipeIngredients = await BuildRecipeIngredientsAsync(ingredientsCsv);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var r = await _db.Recipes.FindAsync(id);
        if (r is null) return;
        
        if (r.IsSystemRecipe)
            throw new InvalidOperationException("Sistem tarifleri silinemez.");
        
        _db.Recipes.Remove(r);
        await _db.SaveChangesAsync();
    }

   public async Task<List<RecipeMatch>> SearchByIngredientsAsync(IEnumerable<string> have)
    {
        var haveSet = have
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .ToHashSet();

        // Tüm tarifleri malzemeleriyle çek (basitlik için)
        var recipes = await _db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .ToListAsync();

        var matches = recipes.Select(r =>
        {
            var needed = r.RecipeIngredients
                .Select(ri => ri.Ingredient.Name.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            var missing = needed.Where(n => !haveSet.Contains(n)).ToList();
            return new RecipeMatch(r, missing);
        })
        // Önce tam uyanlar, sonra en az eksik olandan çok eksiğe doğru
        .OrderBy(m => m.MissingIngredients.Count)
        .ThenBy(m => m.Recipe.Title)
        .ToList();

        return matches;
    }


    // yardımcı
    private async Task<List<RecipeIngredient>> BuildRecipeIngredientsAsync(string ingredientsCsv)
    {
        var names = (ingredientsCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        var exist = await _db.Ingredients
            .Where(i => names.Contains(i.Name.ToLower()))
            .ToListAsync();

        var dict = exist.ToDictionary(i => i.Name.ToLower());
        foreach (var n in names)
            if (!dict.ContainsKey(n))
            {
                var ing = new Ingredient { Name = n };
                _db.Ingredients.Add(ing);
                dict[n] = ing;
            }

        return names.Select(n => new RecipeIngredient { Ingredient = dict[n] }).ToList();
    }

    private async Task<List<RecipeIngredient>> BuildRecipeIngredientsAsync(List<RecipeIngredientDto> ingredients)
    {
        if (ingredients is null || ingredients.Count == 0)
            return new List<RecipeIngredient>();

        var normalized = ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i?.Name))
            .Select(i => new
            {
                Name = i!.Name!.Trim(),
                Quantity = i.Quantity,
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit!.Trim()
            })
            .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return new List<RecipeIngredient>();

        var names = normalized.Select(i => i.Name).ToList();
        var existing = await _db.Ingredients
            .Where(i => names.Contains(i.Name))
            .ToDictionaryAsync(i => i.Name, StringComparer.OrdinalIgnoreCase);

        var result = new List<RecipeIngredient>();

        foreach (var item in normalized)
        {
            if (!existing.TryGetValue(item.Name, out var ingredient))
            {
                ingredient = new Ingredient { Name = item.Name };
                _db.Ingredients.Add(ingredient);
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
