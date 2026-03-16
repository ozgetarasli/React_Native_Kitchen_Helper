using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Models;

namespace Kitchenhelper.Core.Services;

public interface IRecipeService
{
    Task<List<Recipe>> GetAllAsync();
    Task<Recipe?> GetAsync(int id);

    // Create/Update: ingredientsCsv = "domates, yumurta, zeytinyağı"
    Task<int> CreateAsync(string title, string? description, string? stepsMarkdown, string ingredientsCsv);

    // Create with structured format (quantity and unit support)
    Task<int> CreateAsync(string title, string? description, string? stepsMarkdown, List<RecipeIngredientDto> ingredients);

    Task UpdateAsync(int id, string title, string? description, string? stepsMarkdown, string ingredientsCsv);

    Task DeleteAsync(int id);

    Task<List<RecipeMatch>> SearchByIngredientsAsync(IEnumerable<string> have);
}
