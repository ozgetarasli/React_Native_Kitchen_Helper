namespace Kitchenhelper.Core.Services;

public class NutritionInfo
{
    public double? Calories { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? Carbs { get; set; }
}

public interface INutritionService
{
    Task<NutritionInfo> CalculateNutritionAsync(string title, List<string> ingredients);
}
