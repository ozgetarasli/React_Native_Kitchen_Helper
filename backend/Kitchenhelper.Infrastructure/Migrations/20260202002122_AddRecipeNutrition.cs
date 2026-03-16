using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchenhelper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Calories",
                table: "Recipes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Carbs",
                table: "Recipes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Fat",
                table: "Recipes",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Protein",
                table: "Recipes",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calories",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Carbs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Fat",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Protein",
                table: "Recipes");
        }
    }
}
