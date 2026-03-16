using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Web.Models;

public class RecipeCreateRequest
{
    [Required]
    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool IsFavorite { get; set; }

    /// <summary>
    /// Serbest metin adımları (Markdown).
    /// </summary>
    public string? StepsMarkdown { get; set; }

    /// <summary>
    /// Alternatif olarak adım listesi gönderilebilir.
    /// </summary>
    public List<string>? Instructions { get; set; }

    /// <summary>
    /// Ön yüzlerden gelen ham ingredient listesi.
    /// </summary>
    public List<RecipeIngredientRequest>? RecipeIngredients { get; set; }

    // UI'ların gönderdiği ancak henüz persist etmediğimiz alanlar
    public string? PrepTime { get; set; }
    public int? Servings { get; set; }
    public List<string>? Categories { get; set; }
    public string? Image { get; set; }
    public string? SourceUrl { get; set; }

    // Nutrition (per recipe total)
    public double? Calories { get; set; }
    public double? Protein { get; set; }
    public double? Fat { get; set; }
    public double? Carbs { get; set; }
}

public class RecipeIngredientRequest
{
    public IngredientRequest? Ingredient { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
}

public class IngredientRequest
{
    public string? Name { get; set; }
}

