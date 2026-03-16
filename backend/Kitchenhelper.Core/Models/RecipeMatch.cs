using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Models;

public record RecipeMatch(Recipe Recipe, List<string> MissingIngredients);
