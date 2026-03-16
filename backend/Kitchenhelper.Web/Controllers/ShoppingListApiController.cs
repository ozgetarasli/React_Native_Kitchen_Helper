using Kitchenhelper.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kitchenhelper.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("api")]
public class ShoppingListApiController : ControllerBase
{
    private readonly IShoppingListService _service;

    public ShoppingListApiController(IShoppingListService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        var result = items.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            quantity = i.Quantity,
            isChecked = i.IsChecked,
            purchased = i.IsChecked, // React uygulaması için alias
            ingredientId = i.IngredientId,
            category = i.Category ?? "Recipe Ingredients" // Varsayılan kategori
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetAsync(id);
        if (item == null)
            return NotFound();

        var result = new
        {
            id = item.Id,
            name = item.Name,
            quantity = item.Quantity,
            isChecked = item.IsChecked,
            purchased = item.IsChecked,
            ingredientId = item.IngredientId,
            category = item.Category ?? "Recipe Ingredients"
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShoppingListItemRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.name))
            return BadRequest("Name is required");

        await _service.AddAsync(request.name, request.ingredientId, request.quantity, request.category);
        return Ok(new { message = "Item added successfully" });
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] ToggleRequest request)
    {
        await _service.ToggleAsync(id, request.isChecked);
        return Ok(new { message = "Item toggled successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.RemoveAsync(id);
        return Ok(new { message = "Item deleted successfully" });
    }

    [HttpPost("add-missing")]
    public async Task<IActionResult> AddMissing([FromBody] AddMissingRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        await _service.BuildFromMissingAsync(request.recipeId, request.have ?? Array.Empty<string>());
        return Ok(new { message = "Missing items added to shopping list" });
    }
}

public class CreateShoppingListItemRequest
{
    public string name { get; set; } = "";
    public int? ingredientId { get; set; }
    public string? quantity { get; set; }
    public string? category { get; set; }
}

public class ToggleRequest
{
    public bool isChecked { get; set; }
}

public class AddMissingRequest
{
    public int recipeId { get; set; }
    public string[]? have { get; set; }
}
