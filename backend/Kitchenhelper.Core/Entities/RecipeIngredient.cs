namespace Kitchenhelper.Core.Entities;

public class RecipeIngredient
{
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
}
