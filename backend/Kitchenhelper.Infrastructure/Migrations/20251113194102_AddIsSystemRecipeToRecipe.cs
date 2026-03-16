using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchenhelper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSystemRecipeToRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemRecipe",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystemRecipe",
                table: "Recipes");
        }
    }
}
