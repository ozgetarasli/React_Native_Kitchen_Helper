using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kitchenhelper.Infrastructure.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly AppDbContext _db;
    public ShoppingListService(AppDbContext db) => _db = db;

    public async Task<List<ShoppingListItem>> GetAllAsync()
    {
        var allItems = await _db.ShoppingListItems.ToListAsync();
        
        // Malzemeleri normalize edilmiş isimlerine göre grupla
        var grouped = allItems
            .GroupBy(i => NormalizeIngredientName(i.Name))
            .Select(g => {
                // Grup içindeki en kısa ismi (genelde kök halini) ana isim olarak seç
                var mainItem = g.OrderBy(x => x.Name.Length).First();
                
                // Miktarları birleştir (tekil miktarları virgülle ayır)
                var combinedQuantity = string.Join(", ", g
                    .Select(x => x.Quantity?.Trim())
                    .Where(q => !string.IsNullOrEmpty(q))
                    .Distinct());

                return new ShoppingListItem {
                    Id = mainItem.Id,
                    Name = mainItem.Name,
                    Quantity = string.IsNullOrEmpty(combinedQuantity) ? mainItem.Quantity : combinedQuantity,
                    IsChecked = g.Any(x => x.IsChecked), // Herhangi biri işaretliyse işaretli sayılabilir veya g.All(...) tercihe göre
                    Category = mainItem.Category,
                    IngredientId = mainItem.IngredientId
                };
            })
            .OrderBy(i => i.IsChecked)
            .ThenBy(i => i.Name)
            .ToList();

        return grouped;
    }

    public Task<ShoppingListItem?> GetAsync(int id) =>
        _db.ShoppingListItems.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<int> AddAsync(string name, int? ingredientId = null, string? quantity = null, string? category = null)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return 0;

        var normalizedNew = NormalizeIngredientName(name);

        // Aynı isim veya çoğul hali listede varsa tekrar ekleme
        var existingItems = await _db.ShoppingListItems.ToListAsync();
        var exists = existingItems.Any(x => NormalizeIngredientName(x.Name) == normalizedNew);
        
        if (exists) return 0;

        var item = new ShoppingListItem 
        { 
            Name = name, 
            IngredientId = ingredientId, 
            Quantity = quantity,
            Category = category ?? "Other",
            IsChecked = false 
        };
        _db.ShoppingListItems.Add(item);
        return await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(int id)
    {
        var item = await _db.ShoppingListItems.FindAsync(id);
        if (item is null) return;
        
        var normalized = NormalizeIngredientName(item.Name);
        var allItems = await _db.ShoppingListItems.ToListAsync();
        var toRemove = allItems.Where(x => NormalizeIngredientName(x.Name) == normalized).ToList();

        _db.ShoppingListItems.RemoveRange(toRemove);
        await _db.SaveChangesAsync();
    }

    public async Task ToggleAsync(int id, bool isChecked)
    {
        var item = await _db.ShoppingListItems.FindAsync(id);
        if (item is null) return;
        
        var normalized = NormalizeIngredientName(item.Name);
        var allItems = await _db.ShoppingListItems.ToListAsync();
        var toToggle = allItems.Where(x => NormalizeIngredientName(x.Name) == normalized).ToList();

        foreach (var i in toToggle)
            i.IsChecked = isChecked;
            
        await _db.SaveChangesAsync();
    }

    public async Task<int> BuildFromMissingAsync(int recipeId, IEnumerable<string> have)
    {
        // 1?? Kullanıcının elindeki malzemeleri normalize et (trim + lower + turkish plural)
        var haveSet = (have ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => NormalizeIngredientName(s))
            .ToHashSet();

        Console.WriteLine($"[DEBUG] BuildFromMissingAsync - RecipeId: {recipeId}");
        Console.WriteLine($"[DEBUG] Have ingredients (normalized): [{string.Join(", ", haveSet)}]");

        // 2?? Tarif ve malzemeleri DB'den çek
        var recipe = await _db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe is null) return 0;

        // 3?? Tarifteki tüm malzemeleri normalize et
        var needed = recipe.RecipeIngredients
            .Select(ri => NormalizeIngredientName(ri.Ingredient.Name))
            .Distinct()
            .ToList();

        // 4?? Eksikleri hesapla (kullanıcının elindekileri çıkar)
        var missing = needed
            .Where(n => !haveSet.Contains(n))
            .ToList();

        Console.WriteLine($"[DEBUG] Recipe ingredients (normalized): [{string.Join(", ", needed)}]");
        Console.WriteLine($"[DEBUG] Missing ingredients (normalized): [{string.Join(", ", missing)}]");

        if (missing.Count == 0) return 0;

        // 5?? Zaten listede olanları atla
        var existingItems = await _db.ShoppingListItems.ToListAsync();
        var existingNormalizedNames = existingItems
            .Select(i => NormalizeIngredientName(i.Name))
            .ToHashSet();

        var toAdd = missing
            .Where(n => !existingNormalizedNames.Contains(n))
            .ToList();

        // 6?? Eksikleri alışveriş listesine ekle
        foreach (var normalizedName in toAdd)
        {
            // Orijinal adı bulmaya çalış (tarif içinden en kısa halini alabiliriz veya direkt normalize halini kullanırız)
            var originalName = recipe.RecipeIngredients
                .Select(ri => ri.Ingredient.Name)
                .FirstOrDefault(n => NormalizeIngredientName(n) == normalizedName) ?? normalizedName;

            _db.ShoppingListItems.Add(new ShoppingListItem { Name = originalName, IsChecked = false });
        }

        return await _db.SaveChangesAsync();
    }

    private static string NormalizeIngredientName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        
        var text = name.Trim().ToLowerInvariant();

        if (text.Length <= 4) return text;

        // Turkish plural suffixes: -lar, -ler
        if (text.EndsWith("lar"))
        {
            var stem = text.Substring(0, text.Length - 3);
            if (HasLastVowelOfCategory(stem, "aıou")) return stem;
        }
        else if (text.EndsWith("ler"))
        {
            var stem = text.Substring(0, text.Length - 3);
            if (HasLastVowelOfCategory(stem, "eiöü")) return stem;
        }

        return text;
    }

    private static bool HasLastVowelOfCategory(string text, string vowels)
    {
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char c = text[i];
            if ("aeıioöuü".Contains(c))
            {
                return vowels.Contains(c);
            }
        }
        return false;
    }
}