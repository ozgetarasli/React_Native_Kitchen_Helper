export type RootStackParamList = {
  Login: undefined;
  Home: undefined;
  RecipeList: undefined;
  RecipeDetail: { recipeId: string };
  AddEditRecipe: { recipeId?: string } | undefined;
  ShoppingList: undefined;
};
