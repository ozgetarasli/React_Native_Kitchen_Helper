using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Web.Models;

public class RecipeFormVM
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? StepsMarkdown { get; set; }

    [Display(Name = "Ingredients (comma-separated)")]
    [Required(ErrorMessage = "En az bir malzeme giriniz")]
    public string IngredientsCsv { get; set; } = "";
}
