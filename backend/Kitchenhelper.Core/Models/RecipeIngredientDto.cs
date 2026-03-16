namespace Kitchenhelper.Core.Models;

public class RecipeIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
}

