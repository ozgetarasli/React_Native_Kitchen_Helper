using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

public interface IShoppingListService
{
    Task<List<ShoppingListItem>> GetAllAsync();
    Task<ShoppingListItem?> GetAsync(int id);

    Task<int> AddAsync(string name, int? ingredientId = null, string? quantity = null, string? category = null);
    Task RemoveAsync(int id);
    Task ToggleAsync(int id, bool isChecked);

    /// <summary>
    /// Verilen recipe i�in, kullan�c�da bulunan malzemeler (have) d���ld�kten sonra
    /// eksik kalanlar� al��veri� listesine ekler. (Zaten listede olanlar� atlar)
    /// </summary>
    Task<int> BuildFromMissingAsync(int recipeId, IEnumerable<string> have);
}
