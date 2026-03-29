export type RootStackParamList = {
  Login: undefined;
  Register: undefined;
  MainApp: undefined;
  RecipeDetail: { recipeId: string };
  AddEditRecipe: { recipeId?: string } | undefined;
};

export type MainTabParamList = {
  Home: undefined;
  RecipeList: undefined;
  ShoppingList: undefined;
};
