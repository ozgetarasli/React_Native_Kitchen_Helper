using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Core.Entities;

public class Recipe
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    public List<RecipeIngredient> RecipeIngredients { get; set; } = new();

    public string? StepsMarkdown { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsSystemRecipe { get; set; } = false;
    
    [StringLength(500)]
    public string? ImagePath { get; set; }

    [StringLength(500)]
    public string? SourceUrl { get; set; }

    // Yeni alanlar
    [StringLength(100)]
    public string? PrepTime { get; set; }
    
    public int? Servings { get; set; }
    
    [StringLength(500)]
    public string? Categories { get; set; } // JSON string olarak saklanacak

    // Nutrition (per recipe total)
    public double? Calories { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? Carbs { get; set; }

    // Helper property - kategorileri liste olarak döndürür
    public List<string> GetCategories()
    {
        if (string.IsNullOrEmpty(Categories))
            return new List<string>();
        
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Categories) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    // Helper method - kategorileri JSON string olarak set eder
    public void SetCategories(List<string> categories)
    {
        Categories = System.Text.Json.JsonSerializer.Serialize(categories);
    }
}
