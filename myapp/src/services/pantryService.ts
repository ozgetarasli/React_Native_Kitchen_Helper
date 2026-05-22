import api from './api';

// Türkçe karakterleri ve büyük/küçük harf farklarını normalize eder
const normalizeIngredient = (value: string): string =>
  value
    .toLocaleLowerCase('tr-TR')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();

export interface PantryItem {
  id: string;
  name: string;
  quantity: string;
  unit: string;
  category?: string;
  expiryDate?: string;
  notes?: string;
}

export const fetchUserPantryItems = async (): Promise<PantryItem[]> => {
  try {
    const response = await api.get('/PantryApi');
    console.log('📦 Pantry Items Response:', response.data);
    return response.data || [];
  } catch (error) {
    console.error('❌ Error fetching pantry items:', error);
    return [];
  }
};

export const checkIngredientMatch = (
  recipeIngredients: Array<{ name?: string }>,
  pantryItems: PantryItem[]
): Map<string, boolean> => {
  console.log('🔍 Checking ingredients:', recipeIngredients);
  console.log('🛒 Against pantry:', pantryItems);
  
  // Filter and normalize pantry ingredients
  const pantryIngredients = new Set(
    pantryItems
      .filter(item => item && item.name)
      .map(item => {
        const normalized = normalizeIngredient(item.name);
        console.log(`  ✓ Pantry item: "${item.name}" -> "${normalized}"`);
        return normalized;
      })
  );

  console.log('🛒 Pantry Set:', Array.from(pantryIngredients));

  const matchMap = new Map<string, boolean>();
  recipeIngredients.forEach(ing => {
    // Skip if ingredient name is undefined or empty
    if (!ing || !ing.name || typeof ing.name !== 'string') {
      console.log(`  ✗ Skipping invalid ingredient:`, ing);
      return;
    }
    
    const normalizedName = normalizeIngredient(ing.name);
    // Tam eşleşme veya kısmi eşleşme (malzeme adı pantry'deki bir girişi içeriyor mu)
    const hasMatch = pantryIngredients.has(normalizedName) ||
      Array.from(pantryIngredients).some(
        pantryName => pantryName.includes(normalizedName) || normalizedName.includes(pantryName)
      );
    console.log(`  🔎 Recipe: "${ing.name}" -> "${normalizedName}" = ${hasMatch ? '✓ MATCH' : '✗ no match'}`);
    matchMap.set(ing.name, hasMatch);
  });

  console.log('📊 Match Map:', matchMap);
  return matchMap;
};

export const calculateMatchPercentage = (
  matchMap: Map<string, boolean>
): { matched: number; total: number; percentage: number } => {
  if (matchMap.size === 0) return { matched: 0, total: 0, percentage: 0 };

  let matched = 0;
  matchMap.forEach(hasMatch => {
    if (hasMatch) matched++;
  });

  const total = matchMap.size;
  const percentage = Math.round((matched / total) * 100);

  return { matched, total, percentage };
};
