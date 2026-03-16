-- Tüm tabloları temizle
DELETE FROM RecipeIngredients;
DELETE FROM Recipes;
DELETE FROM Ingredients;
DELETE FROM ShoppingListItems;
DELETE FROM PantryItems;
DELETE FROM Users;

-- ID'leri sıfırla
DELETE FROM sqlite_sequence;
