import React from 'react';
import { Text } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { RootStackParamList, MainTabParamList } from './src/types/navigation';

// Screens
import LoginScreen from './src/screens/LoginScreen';
import RegisterScreen from './src/screens/RegisterScreen';
import HomeScreen from './src/screens/HomeScreen';
import RecipeListScreen from './src/screens/RecipeListScreen';
import RecipeDetailScreen from './src/screens/RecipeDetailScreen';
import AddEditRecipeScreen from './src/screens/AddEditRecipeScreen';
import ShoppingListScreen from './src/screens/ShoppingListScreen';

const Stack = createNativeStackNavigator<RootStackParamList>();
const Tab   = createBottomTabNavigator<MainTabParamList>();

const TAB_ICONS: Record<string, string> = {
  Home:         '🏠',
  RecipeList:   '📖',
  ShoppingList: '🛒',
};

function MainTabs() {
  return (
    <Tab.Navigator
      id="MainTabs"
      screenOptions={({ route }) => ({
        tabBarIcon: () => (
          <Text style={{ fontSize: 22 }}>{TAB_ICONS[route.name]}</Text>
        ),
        tabBarActiveTintColor:   '#FF6B35',
        tabBarInactiveTintColor: '#9E9E9E',
        tabBarStyle: {
          backgroundColor: '#FFFFFF',
          borderTopWidth: 1,
          borderTopColor: '#F0F0F0',
          height: 62,
          paddingBottom: 6,
          paddingTop: 4,
          marginBottom: 16,
          marginHorizontal: 16,
          borderRadius: 20,
          position: 'absolute',
          bottom: 0,
          left: 0,
          right: 0,
          elevation: 8,
          shadowColor: '#000',
          shadowOffset: { width: 0, height: -2 },
          shadowOpacity: 0.08,
          shadowRadius: 8,
        },
        tabBarLabelStyle: { fontSize: 11, fontWeight: '600' },
      })}
    >
      <Tab.Screen
        name="Home"
        component={HomeScreen}
        options={{ headerShown: false, tabBarLabel: 'Ana Sayfa' }}
      />
      <Tab.Screen
        name="RecipeList"
        component={RecipeListScreen}
        options={{ title: 'Tarifler', tabBarLabel: 'Tarifler' }}
      />
      <Tab.Screen
        name="ShoppingList"
        component={ShoppingListScreen}
        options={{ title: 'Alışveriş Listesi', headerShown: false, tabBarLabel: 'Alışveriş' }}
      />
    </Tab.Navigator>
  );
}

export default function App() {
  return (
    <NavigationContainer>
      <Stack.Navigator id="RootStack" initialRouteName="Login">
        <Stack.Screen name="Login"     component={LoginScreen}     options={{ headerShown: false }} />
        <Stack.Screen name="Register"  component={RegisterScreen}  options={{ headerShown: false }} />
        <Stack.Screen name="MainApp"   component={MainTabs}        options={{ headerShown: false }} />
        <Stack.Screen name="RecipeDetail"   component={RecipeDetailScreen}  options={{ title: 'Tarif Detayı' }} />
        <Stack.Screen name="AddEditRecipe"  component={AddEditRecipeScreen} options={{ title: 'Tarif Ekle' }} />
      </Stack.Navigator>
    </NavigationContainer>
  );
}

