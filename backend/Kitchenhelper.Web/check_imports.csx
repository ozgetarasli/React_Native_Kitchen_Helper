// Quick check script for RecipeImports
using Microsoft.EntityFrameworkCore;
using Kitchenhelper.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

Console.WriteLine("\n=== RecipeImports Status ===\n");

var imports = await db.RecipeImports
    .OrderByDescending(ri => ri.Id)
    .Take(10)
    .ToListAsync();

if (imports.Count == 0)
{
    Console.WriteLine("No imports found.");
}
else
{
    foreach (var i in imports)
    {
        Console.WriteLine($"ID: {i.Id}");
        Console.WriteLine($"  UserId: {i.UserId}");
        Console.WriteLine($"  Status: {i.Status}");
        Console.WriteLine($"  SourceType: {i.SourceType}");
        Console.WriteLine($"  SourceFilePath: {i.SourceFilePath ?? "N/A"}");
        Console.WriteLine($"  AudioPath: {i.AudioPath ?? "N/A"}");
        Console.WriteLine($"  TranscriptPath: {i.TranscriptPath ?? "N/A"}");
        Console.WriteLine($"  Error: {i.ErrorMessage ?? "N/A"}");
        Console.WriteLine($"  Created: {i.CreatedAt}");
        Console.WriteLine($"  Updated: {i.UpdatedAt}");
        Console.WriteLine();
    }
}
