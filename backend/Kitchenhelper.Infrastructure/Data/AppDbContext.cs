using Kitchenhelper.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kitchenhelper.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RecipeImport> RecipeImports => Set<RecipeImport>();
    public DbSet<RecipeDraft> RecipeDrafts => Set<RecipeDraft>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // many-to-many ilişki: RecipeIngredient aratablo
        b.Entity<RecipeIngredient>()
            .HasKey(x => new { x.RecipeId, x.IngredientId });

        // (aynı isimli malzemelere izin verebilir)
        b.Entity<Ingredient>()
            .HasIndex(i => i.Name).IsUnique(false);
        
        // Email unique olmalı
        b.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // RecipeDraft -> RecipeImport ilişkisi
        b.Entity<RecipeDraft>()
            .HasOne(d => d.Import)
            .WithMany()
            .HasForeignKey(d => d.ImportId)
            .OnDelete(DeleteBehavior.Cascade);

        // RecipeDraft -> User ilişkisi
        b.Entity<RecipeDraft>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // RecipeDraft -> PublishedRecipe ilişkisi
        b.Entity<RecipeDraft>()
            .HasOne(d => d.PublishedRecipe)
            .WithMany()
            .HasForeignKey(d => d.PublishedRecipeId)
            .OnDelete(DeleteBehavior.SetNull);

        // RecipeImport -> Draft ilişkisi (optional)
        b.Entity<RecipeImport>()
            .HasOne(i => i.Draft)
            .WithMany()
            .HasForeignKey(i => i.DraftId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
