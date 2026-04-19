#r "nuget: Microsoft.Data.Sqlite, 9.0.0"
using Microsoft.Data.Sqlite;

var conn = new SqliteConnection("Data Source=kitchenhelper.db");
conn.Open();

// Önce bozuk kayıtları göster
var selectCmd = conn.CreateCommand();
selectCmd.CommandText = "SELECT Id, substr(ImagePath, 1, 80) FROM Recipes WHERE ImagePath IS NOT NULL AND ImagePath != '' LIMIT 20;";
var reader = selectCmd.ExecuteReader();
Console.WriteLine("=== Mevcut ImagePath değerleri ===");
while (reader.Read())
{
    Console.WriteLine($"  ID={reader.GetInt32(0)} | ImagePath={reader.GetString(1)}...");
}
reader.Close();

// base64 / data:image içeren bozuk path'leri temizle
var updateCmd = conn.CreateCommand();
updateCmd.CommandText = "UPDATE Recipes SET ImagePath = NULL WHERE ImagePath LIKE '%data:image%' OR ImagePath LIKE '%;base64%';";
var rows = updateCmd.ExecuteNonQuery();
Console.WriteLine($"\n✅ {rows} adet bozuk ImagePath temizlendi (NULL yapıldı).");

conn.Close();
