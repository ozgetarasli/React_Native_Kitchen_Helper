import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { RootStackParamList } from './src/types/navigation';

// Import Screens
import LoginScreen from './src/screens/LoginScreen';
import HomeScreen from './src/screens/HomeScreen';
import RecipeListScreen from './src/screens/RecipeListScreen';
import RecipeDetailScreen from './src/screens/RecipeDetailScreen';
import AddEditRecipeScreen from './src/screens/AddEditRecipeScreen';
import ShoppingListScreen from './src/screens/ShoppingListScreen';

const Stack = createNativeStackNavigator<RootStackParamList>();

export default function App() {
  return (
    <NavigationContainer>
      <Stack.Navigator id="RootStack" initialRouteName="Login">
        <Stack.Screen name="Login" component={LoginScreen} options={{ headerShown: false }} />
        <Stack.Screen name="Home" component={HomeScreen} options={{ title: 'Kitchen Helper' }} />
        <Stack.Screen name="RecipeList" component={RecipeListScreen} options={{ title: 'Recipes' }} />
        <Stack.Screen name="RecipeDetail" component={RecipeDetailScreen} options={{ title: 'Recipe Details' }} />
        <Stack.Screen name="AddEditRecipe" component={AddEditRecipeScreen} options={{ title: 'Add/Edit Recipe' }} />
        <Stack.Screen name="ShoppingList" component={ShoppingListScreen} options={{ title: 'Shopping List' }} />
      </Stack.Navigator>
    </NavigationContainer>
  );
}
