using Kitchenhelper.Core.Entities;

namespace Kitchenhelper.Core.Services;

public interface IPantryService
{
    Task<List<PantryItem>> GetAllAsync();
    Task<PantryItem?> GetAsync(int id);
    Task<int> AddAsync(string name, decimal quantity, string unit, string category, DateTime? expiryDate = null, string? notes = null);
    Task UpdateAsync(int id, string name, decimal quantity, string unit, string category, DateTime? expiryDate = null, string? notes = null);
    Task DeleteAsync(int id);
    Task<List<PantryItem>> SearchAsync(string query);
    Task<List<PantryItem>> GetByCategoryAsync(string category);
    Task<List<PantryItem>> GetExpiringSoonAsync(int days = 7);
    Task<List<PantryItem>> GetExpiredAsync();
    Task<int> GetTotalCountAsync();
    Task<int> GetExpiringSoonCountAsync(int days = 7);
}
