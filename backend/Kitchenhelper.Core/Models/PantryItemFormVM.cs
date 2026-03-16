using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Core.Models;

public class PantryItemFormVM
{
    public int? Id { get; set; }
    
    [Required(ErrorMessage = "Malzeme adı gereklidir")]
    [StringLength(100, ErrorMessage = "Malzeme adı en fazla 100 karakter olabilir")]
    [Display(Name = "Malzeme Adı")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Miktar gereklidir")]
    [Range(0.01, 9999.99, ErrorMessage = "Miktar 0.01 ile 9999.99 arasında olmalıdır")]
    [Display(Name = "Miktar")]
    public decimal Quantity { get; set; }
    
    [Required(ErrorMessage = "Birim gereklidir")]
    [StringLength(50, ErrorMessage = "Birim en fazla 50 karakter olabilir")]
    [Display(Name = "Birim")]
    public string Unit { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Kategori gereklidir")]
    [StringLength(50, ErrorMessage = "Kategori en fazla 50 karakter olabilir")]
    [Display(Name = "Kategori")]
    public string Category { get; set; } = string.Empty;
    
    [Display(Name = "Son Kullanma Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }
    
    [StringLength(500, ErrorMessage = "Notlar en fazla 500 karakter olabilir")]
    [Display(Name = "Notlar")]
    public string? Notes { get; set; }
}
