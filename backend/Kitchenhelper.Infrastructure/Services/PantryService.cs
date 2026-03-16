using Kitchenhelper.Core.Entities;
using Kitchenhelper.Core.Services;
using Kitchenhelper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kitchenhelper.Infrastructure.Services;

public class PantryService : IPantryService
{
    private readonly AppDbContext _db;
    public PantryService(AppDbContext db) => _db = db;

    public Task<List<PantryItem>> GetAllAsync() =>
        _db.PantryItems.OrderBy(i => i.Name).ToListAsync();

    public Task<PantryItem?> GetAsync(int id) =>
        _db.PantryItems.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<int> AddAsync(string name, decimal quantity, string unit, string category, DateTime? expiryDate = null, string? notes = null)
    {
        var item = new PantryItem
        {
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Category = category,
            ExpiryDate = expiryDate,
            Notes = notes,
            DateAdded = DateTime.Now
        };

        _db.PantryItems.Add(item);
        return await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, string name, decimal quantity, string unit, string category, DateTime? expiryDate = null, string? notes = null)
    {
        var item = await _db.PantryItems.FindAsync(id);
        if (item is null) return;

        item.Name = name;
        item.Quantity = quantity;
        item.Unit = unit;
        item.Category = category;
        item.ExpiryDate = expiryDate;
        item.Notes = notes;
        item.LastUpdated = DateTime.Now;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.PantryItems.FindAsync(id);
        if (item is null) return;
        
        _db.PantryItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public Task<List<PantryItem>> SearchAsync(string query)
    {
        var lowerQuery = query.ToLower();
        return _db.PantryItems
            .Where(i => i.Name.ToLower().Contains(lowerQuery) ||
                       i.Category.ToLower().Contains(lowerQuery) ||
                       (i.Notes != null && i.Notes.ToLower().Contains(lowerQuery)))
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public Task<List<PantryItem>> GetByCategoryAsync(string category) =>
        _db.PantryItems
            .Where(i => i.Category.ToLower() == category.ToLower())
            .OrderBy(i => i.Name)
            .ToListAsync();

    public Task<List<PantryItem>> GetExpiringSoonAsync(int days = 7)
    {
        var cutoffDate = DateTime.Now.AddDays(days);
        return _db.PantryItems
            .Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= cutoffDate && i.ExpiryDate.Value >= DateTime.Now)
            .OrderBy(i => i.ExpiryDate)
            .ToListAsync();
    }

    public Task<List<PantryItem>> GetExpiredAsync() =>
        _db.PantryItems
            .Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value < DateTime.Now)
            .OrderBy(i => i.ExpiryDate)
            .ToListAsync();

    public Task<int> GetTotalCountAsync() =>
        _db.PantryItems.CountAsync();

    public Task<int> GetExpiringSoonCountAsync(int days = 7)
    {
        var cutoffDate = DateTime.Now.AddDays(days);
        return _db.PantryItems
            .CountAsync(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= cutoffDate && i.ExpiryDate.Value >= DateTime.Now);
    }
}
