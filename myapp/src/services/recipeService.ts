import api from './api';

export interface RecipeDetailResponse {
  id: number;
  title: string;
  description?: string;
  image?: string;
  prepTime?: string;
  servings?: number;
  categories: string[];
  sourceUrl?: string;
  isFavorite: boolean;
  calories?: number;
  protein?: number;
  fat?: number;
  carbs?: number;
  ingredients: Array<{
    name: string;
    amount?: string;
    quantity?: number;
    unit?: string;
  }>;
  instructions?: string[];
  stepsMarkdown?: string;
}

export const fetchRecipeDetail = async (recipeId: string): Promise<RecipeDetailResponse> => {
  try {
    const response = await api.get(`/RecipesApi/${recipeId}`);
    const data = response.data;
    
    console.log('📥 Raw API Response:', data);

    // Ensure ingredients array exists and is properly formatted
    if (data && !Array.isArray(data.ingredients)) {
      data.ingredients = [];
    }

    // Filter out invalid ingredient entries and log them
    if (data && Array.isArray(data.ingredients)) {
      console.log('📦 Recipe Ingredients:');
      data.ingredients.forEach((ing: any) => {
        console.log(`  - Name: "${ing.name}", Amount: "${ing.amount}"`);
      });
      data.ingredients = data.ingredients.filter((ing: any) => ing && ing.name);
    }

    return data;
  } catch (error) {
    console.error('Error fetching recipe detail:', error);
    throw error;
  }
};

export const toggleRecipeFavorite = async (recipeId: string): Promise<void> => {
  try {
    await api.post(`/RecipesApi/${recipeId}/toggle-favorite`);
  } catch (error) {
    console.error('Error toggling favorite:', error);
    throw error;
  }
};
