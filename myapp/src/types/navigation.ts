export type RootStackParamList = {
  Login: undefined;
  Register: undefined;
  MainApp: undefined;
  RecipeDetail: { recipeId: string | number };
  AddEditRecipe: { recipeId?: string } | undefined;
};

export type MainTabParamList = {
  Home: undefined;
  RecipeList: undefined;
  ShoppingList: undefined;
};
