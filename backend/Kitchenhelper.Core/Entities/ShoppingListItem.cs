namespace Kitchenhelper.Core.Entities;

public class ShoppingListItem
{
    public int Id { get; set; }
    public int? IngredientId { get; set; } // ingredient bağlıysa
    public string Name { get; set; } = "";
    public string? Quantity { get; set; }
    public string? Category { get; set; }
    public bool IsChecked { get; set; }
}
