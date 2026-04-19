#r "nuget: Microsoft.Data.Sqlite, 9.0.0"
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "kitchenhelper.db");
var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// Mevcut uploads klasöründeki resimler → tarif eşleştirmesi
var mappings = new Dictionary<int, string>
{
    { 1,  "/uploads/carbonara.jpg" },         // Classic Spaghetti Carbonara
    { 2,  "/uploads/tikka-masala.jpg" },       // Chicken Tikka Masala
    { 3,  "/uploads/cookies.jpg" },            // Chocolate Chip Cookies
    { 4,  "/uploads/greek-salad.jpg" },        // Greek Salad
    { 5,  "/uploads/pancakes.jpg" },           // Blueberry Pancakes
    { 6,  "/uploads/tacos.jpg" },              // Beef Tacos
    { 7,  "/uploads/caesar-salad.jpg" },       // Caesar Salad
    { 8,  "/uploads/risotto.jpg" },            // Mushroom Risotto
    { 9,  "/uploads/salmon.jpg" },             // Grilled Salmon
    { 10, "/uploads/stir-fry.jpg" },           // Vegetable Stir Fry
    { 12, "/uploads/tomato-soup.jpg" },        // Tomato Soup
    { 13, "/uploads/alfredo.jpg" },            // Chicken Alfredo
    { 14, "/uploads/omelette.jpg" },           // French Omelette
    { 16, "/uploads/shrimp-scampi.jpg" },      // Shrimp Scampi
    { 19, "/uploads/caprese.jpg" },            // Caprese Salad
    { 20, "/uploads/lemon-chicken.jpg" },      // Lemon Garlic Chicken
    { 21, "/uploads/chocolate-mousse.jpg" },   // Chocolate Mousse
    { 22, "/uploads/minestrone.jpg" },         // Minestrone Soup
    { 23, "/uploads/fish-chips.jpg" },         // Fish and Chips
    { 24, "/uploads/pad-thai.jpg" },           // Pad Thai
    { 11, "/uploads/banana-bread.jpg" },       // Banana
    { 18, "/uploads/stroganoff.jpg" },         // Beef
    { 38, "/uploads/bbq-pizza.jpg" },          // Yulaflı Pizza
    { 39, "/uploads/bbq-pizza.jpg" },          // Yulaflı Pizza (duplicate)
    { 28, "/uploads/shrimp-scampi.jpg" },      // Tereyağlı Karides
    { 40, "/uploads/alfredo.jpg" },            // Lazanya (alfredo benzeri)
};

// Duplicate Beef Tacos'lara da tacos.jpg ata
foreach (var id in new[] { 50, 51, 52, 53, 55, 56 })
    mappings[id] = "/uploads/tacos.jpg";

int updated = 0;
foreach (var (recipeId, imagePath) in mappings)
{
    var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE Recipes SET ImagePath = @img WHERE Id = @id AND (ImagePath IS NULL OR ImagePath = '')";
    cmd.Parameters.AddWithValue("@img", imagePath);
    cmd.Parameters.AddWithValue("@id", recipeId);
    var rows = cmd.ExecuteNonQuery();
    if (rows > 0)
    {
        Console.WriteLine($"  ✅ ID={recipeId} → {imagePath}");
        updated++;
    }
}

Console.WriteLine($"\n🎉 {updated} tarife resim atandı!");
