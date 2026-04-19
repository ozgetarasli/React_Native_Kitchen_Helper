using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kitchenhelper.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("api")]
public class PantryApiController : ControllerBase
{
    private readonly IPantryService _service;

    public PantryApiController(IPantryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        var result = items.Select(i => new
        {
            id = i.Id.ToString(),
            name = i.Name,
            quantity = i.Quantity.ToString(),
            unit = i.Unit,
            category = string.IsNullOrEmpty(i.Category) ? "Diğer" : i.Category,
            expiryDate = i.ExpiryDate,
            notes = i.Notes
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetAsync(id);
        if (item == null)
            return NotFound();

        return Ok(new
        {
            id = item.Id.ToString(),
            name = item.Name,
            quantity = item.Quantity.ToString(),
            unit = item.Unit,
            category = string.IsNullOrEmpty(item.Category) ? "Diğer" : item.Category,
            expiryDate = item.ExpiryDate,
            notes = item.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePantryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.name))
            return BadRequest("Name is required");

        if (!decimal.TryParse(request.quantity, out var quantity))
        {
            quantity = 1M;
        }

        var newId = await _service.AddAsync(
            request.name, 
            quantity, 
            request.unit ?? "adet", 
            request.category ?? "Diğer", 
            request.expiryDate, 
            request.notes
        );

        return Ok(new { message = "Item added successfully", id = newId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePantryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.name))
            return BadRequest("Name is required");

        if (!decimal.TryParse(request.quantity, out var quantity))
        {
            quantity = 1M;
        }

        await _service.UpdateAsync(
            id, 
            request.name, 
            quantity, 
            request.unit ?? "adet", 
            request.category ?? "Diğer", 
            request.expiryDate, 
            request.notes
        );
        return Ok(new { message = "Item updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(new { message = "Item deleted successfully" });
    }
}

public class CreatePantryRequest
{
    public string name { get; set; } = string.Empty;
    public string quantity { get; set; } = "1";
    public string? unit { get; set; }
    public string? category { get; set; }
    public DateTime? expiryDate { get; set; }
    public string? notes { get; set; }
}

public class UpdatePantryRequest : CreatePantryRequest
{
}
