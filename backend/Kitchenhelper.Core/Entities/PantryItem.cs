using System.ComponentModel.DataAnnotations;

namespace Kitchenhelper.Core.Entities;

public class PantryItem
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public decimal Quantity { get; set; }
    
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;
    
    public DateTime? ExpiryDate { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public DateTime DateAdded { get; set; } = DateTime.Now;
    
    public DateTime? LastUpdated { get; set; }
}
