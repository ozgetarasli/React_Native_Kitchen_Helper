using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchenhelper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRecipeImportForProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VideoUrl",
                table: "RecipeImports",
                newName: "TranscriptPath");

            migrationBuilder.RenameColumn(
                name: "VideoFilePath",
                table: "RecipeImports",
                newName: "SourceUrl");

            migrationBuilder.AddColumn<string>(
                name: "AudioPath",
                table: "RecipeImports",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "RecipeImports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "RecipeImports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "RecipeImports",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "RecipeImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecipeImports",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioPath",
                table: "RecipeImports");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "RecipeImports");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "RecipeImports");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "RecipeImports");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "RecipeImports");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecipeImports");

            migrationBuilder.RenameColumn(
                name: "TranscriptPath",
                table: "RecipeImports",
                newName: "VideoUrl");

            migrationBuilder.RenameColumn(
                name: "SourceUrl",
                table: "RecipeImports",
                newName: "VideoFilePath");
        }
    }
}
