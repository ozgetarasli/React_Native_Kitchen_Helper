# 📖 Tarif Detay Ekranı - Geliştirici Rehberi

## 🎯 Özet
Kullanıcıların seçtikleri tarifin tam detayını görüntüleyebileceği, malzemeleri pantryları ile eşleştirebileceği ve adım adım talimatları takip edebileceği ekran.

---

## 🏗️ Mimari

### Dosyalar ve Sorumlulukları

#### 1. **RecipeDetailScreen.tsx** (Ana Ekran Bileşeni)
```
Sorumluluklar:
- Tarif verilerini getir ve göster
- Pantry eşleştirmesini yönet
- Markdownı step'lere ayrıştır
- Animasyonları uygula
- Kullanıcı etkileşimlerini işle
```

**Devlet Değişkenleri:**
- `recipe`: Tarif verileri
- `loading`: Yükleme durumu
- `ingredientMatches`: Malzeme eşleştirme haritası
- `matchPercentage`: Eşleştirme yüzdesisi istatistikleri
- `steps`: Ayrıştırılmış hazırlama adımları

#### 2. **recipeService.ts** (Tarif İşlemler)
```typescript
// Tarif detayını ID ile getir
fetchRecipeDetail(recipeId: string): Promise<RecipeDetailResponse>

// Favori durumunu değiştir
toggleRecipeFavorite(recipeId: string): Promise<void>
```

#### 3. **pantryService.ts** (Pantry ve Eşleştirme)
```typescript
// Kullanıcının pantry öğelerini getir
fetchUserPantryItems(): Promise<PantryItem[]>

// Malzemeleri pantry ile eşleştir
checkIngredientMatch(
  recipeIngredients, 
  pantryItems
): Map<string, boolean>

// Eşleştirme yüzdesini hesapla
calculateMatchPercentage(matchMap): { matched, total, percentage }
```

---

## 🎨 UI Komponetleri

### 1. **Başlık Bölümü**
```
┌─────────────────────────────────┐
│  [Tarif Görseli]                │
│  [Geri] Favori[❤️]              │
├─────────────────────────────────┤
│ Tarif Adı                       │
│ [Kategori1] [Kategori2]         │
│ 👥 4 kişi  ⏱️ 30 dk  🔥 250 kcal│
│ Tarif açıklaması...             │
└─────────────────────────────────┘
```

### 2. **Malzeme Eşleştirme Bölümü**
```
📦 Malzemeler                [75% eşleşme]
████████░░ 75%
6 / 8 malzeme pantryinizde var

✓ Soğan              1 adet
✓ Domates            3 adet
○ Biber              1 adet
✓ Sarımsak           2 diş
○ Zeytin yağı        2 yemek kaşığı
```

### 3. **Adımlar Bölümü**
```
👨‍🍳 Hazırlama Adımları

1. Soğanları ince ince doğrayın...
2. Bir tava da yağı ısıtın...
3. Doğranmış soğanları tavanın içine koyun...
```

### 4. **Besin Bilgileri**
```
🥗 Besin Bilgileri

┌─────────┬─────────┬─────────┬──────────┐
│ 250 kcal│ 12g     │ 8g      │ 22g      │
│ Kalori  │ Protein │ Yağ     │ Karbo    │
└─────────┴─────────┴─────────┴──────────┘
```

---

## 🔄 Veri Akışı

```
RecipeListScreen
    ↓
[Tarif Tıkla]
    ↓
RecipeDetailScreen
    ├─→ fetchRecipeDetail(id)     → RecipeAPI
    │
    ├─→ fetchUserPantryItems()    → PantryAPI
    │
    ├─→ checkIngredientMatch()    [Client-side]
    │
    └─→ parseSteps()              [Client-side]
         ↓
    [Ekran Güncelle]
```

---

## 📋 Eşleştirme Renk Kodu

| Yüzde | Renk         | Anlam                  |
|------|--------------|------------------------|
| 80%+ | 🟢 Yeşil     | Çoğu malzeme mevcut   |
| 50-79%| 🟠 Turuncu   | Bazı malzemeler mevcut|
| <50%  | 🔴 Kırmızı   | Az sayıda mevcut      |

---

## 🚀 Performans Optimizasyonları

1. **Paralel API Çağrıları**
   ```typescript
   // Tarif ve pantry verilerini aynı anda getir
   await Promise.all([
     fetchRecipeDetail(id),
     fetchUserPantryItems()
   ])
   ```

2. **Paralel Animasyonlar**
   ```typescript
   Animated.parallel([fadeAnim, slideAnim, scaleAnim])
   ```

3. **ScrollView Performansı**
   - `showsVerticalScrollIndicator={false}`
   - `scrollEventThrottle={16}` (60fps)
   - Content container stilizasyonu

4. **Koşullu Render (Lazy Loading)**
   ```typescript
   // Sadece mevcut veri göster
   {recipe.calories && <Nutrition />}
   ```

---

## ⚙️ API Bağlantıları

### 1. Tarif Detayı
```
GET /api/RecipesApi/:id
Response:
{
  id: number,
  title: string,
  description: string,
  imagePath: string,
  prepTime: string,
  servings: number,
  categories: string[],
  stepsMarkdown: string,
  ingredients: [{name, quantity, unit}],
  isFavorite: boolean,
  calories: number,
  protein: number,
  fat: number,
  carbs: number
}
```

### 2. Pantry Öğeleri
```
GET /api/PantryApi
Response:
[{
  id: number,
  ingredientName: string,
  quantity: number,
  unit: string,
  expiryDate?: string
}]
```

### 3. Favori Toggle
```
POST /api/RecipesApi/:id/toggle-favorite
Response: {}
```

---

## 🐛 Hata Yönetimi

```typescript
// Hata durumunda kullanıcı dostu mesaj
Error Page:
  😞
  Hata
  Tarif yüklenirken bir hata oluştu. Lütfen tekrar deneyin.
  [Tekrar Dene]
```

---

## 📱 Responsive Tasarım

- **Safe Area**: Tüm cihazlarda güvenli bölgede render
- **Dinamik İçerik**: İçerik ekran genişliğine uyum sağlar
- **Flexbox Layout**: Responsive grid ve row yapıları
- **Threshold Bildirimleri**: Metin uzunluğuna göre "Daha fazla" butonu

---

## 🔧 Geliştirme Notları

1. **Markdown Parsing**
   - Destekler: `1. Step` ve `- Step` formatları
   - Yeni satırları otomatik ayrıştırır

2. **Malzeme Normalizasyonu**
   - Case-insensitive karşılaştırma
   - Trim whitespace
   - Türkçe karakterler desteklenir

3. **Animasyonlar**
   - Native Driver kullanımı (60fps)
   - Paralel çalıştırma yapısı
   - 400-450ms duration

---

## ✅ Test İçin Kontrol Listesi

- [ ] Tarif yüklendiğinde animasyonlar düzgün mi?
- [ ] Eşleştirme yüzdesisi doğru hesaplanıyor mu?
- [ ] Renkler doğru yüzdelere göre gösteriliyor mu?
- [ ] Adımlar doğru şekilde ayrıştırılıyor mu?
- [ ] Geri butonuna tıklandığında liste dönüyor mu?
- [ ] Favori butonlu favoriler toggle ediyor mu?
- [ ] Hata durumunda hata sayfası görünüyor mu?
- [ ] Besin bilgileri varsa gösteriliyor mu?

---

## 🎓 Örnek Kullanım Akışı

```
1. Tarif Listesi Ekranında
   ├─ Bir tarifi tıkla
   └─ RecipeDetailScreen açılır

2. Tarif Detay Sayfasında
   ├─ Animasyon başlar (fade + slide)
   ├─ Tarif görseli ve başlığı göster
   ├─ Pantry verileriyle eşleştir
   ├─ Eşleştirme yüzdesini göster
   ├─ Malzemeleri listele
   ├─ Adımları göster
   └─ Besin bilgilerini göster (varsa)

3. Kullanıcı İşlemleri
   ├─ Favori butonuna tıkla
   ├─ "Daha Fazla Göster" tıkla
   ├─ Düzenle butonuna tıkla
   └─ Geri butonuna tıkla
```

---

## 📚 İlgili Dosyalar

- [RecipeListScreen.tsx](./myapp/src/screens/RecipeListScreen.tsx)
- [HomeScreen.tsx](./myapp/src/screens/HomeScreen.tsx)
- [navigation.ts](./myapp/src/types/navigation.ts)
- [RECIPE_DETAIL_IMPLEMENTATION.md](./RECIPE_DETAIL_IMPLEMENTATION.md)

