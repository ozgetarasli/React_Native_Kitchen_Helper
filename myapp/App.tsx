import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Platform, Text, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { navigationRef } from './src/services/navigationRef';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { RootStackParamList, MainTabParamList } from './src/types/navigation';

// Screens
import LoginScreen from './src/screens/LoginScreen';
import RegisterScreen from './src/screens/RegisterScreen';
import HomeScreen from './src/screens/HomeScreen';
import RecipeListScreen from './src/screens/RecipeListScreen';
import FavoritesScreen from './src/screens/FavoritesScreen';
import RecipeDetailScreen from './src/screens/RecipeDetailScreen';
import AddEditRecipeScreen from './src/screens/AddEditRecipeScreen';
import ShoppingListScreen from './src/screens/ShoppingListScreen';
import { hasAuthToken } from './src/services/authSession';

const Stack = createNativeStackNavigator<RootStackParamList>();
const Tab   = createBottomTabNavigator<MainTabParamList>();

const TAB_ICONS: Record<keyof MainTabParamList, string> = {
  Home: '🏠',
  RecipeList: '📖',
  Favorites: '❤️',
  ShoppingList: '🛒',
};

function MainTabs() {
  return (
    <Tab.Navigator
      id="MainTabs"
      screenOptions={({ route }) => ({
        tabBarIcon: ({ focused }) => (
          <Text style={{ fontSize: focused ? 22 : 20 }}>{TAB_ICONS[route.name]}</Text>
        ),
        tabBarActiveTintColor: '#E85D2A',
        tabBarInactiveTintColor: '#8B94A5',
        headerTitleStyle: {
          fontWeight: '700',
          color: '#1A2433',
        },
        headerStyle: {
          backgroundColor: '#FFFDF9',
        },
        headerShadowVisible: false,
        tabBarStyle: {
          backgroundColor: '#FFFFFF',
          borderTopWidth: 1,
          borderTopColor: '#EEF0F4',
          height: Platform.OS === 'ios' ? 86 : 72,
          paddingBottom: 6,
          paddingTop: 4,
          marginBottom: 12,
          marginHorizontal: 16,
          borderRadius: 18,
          position: 'absolute',
          bottom: 0,
          left: 0,
          right: 0,
          elevation: 8,
          shadowColor: '#000',
          shadowOffset: { width: 0, height: -2 },
          shadowOpacity: 0.09,
          shadowRadius: 10,
        },
        tabBarLabelStyle: { fontSize: 11, fontWeight: '700' },
        tabBarHideOnKeyboard: true,
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
        name="Favorites"
        component={FavoritesScreen}
        options={{ title: 'Favoriler', tabBarLabel: 'Favoriler' }}
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
  const [initialRouteName, setInitialRouteName] = useState<keyof RootStackParamList | null>(null);

  useEffect(() => {
    const resolveInitialRoute = async () => {
      try {
        const tokenExists = await hasAuthToken();
        setInitialRouteName(tokenExists ? 'MainApp' : 'Login');
      } catch {
        setInitialRouteName('Login');
      }
    };

    resolveInitialRoute();
  }, []);

  if (!initialRouteName) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFFDF9' }}>
        <ActivityIndicator size="large" color="#E85D2A" />
      </View>
    );
  }

  return (
    <NavigationContainer ref={navigationRef}>
      <Stack.Navigator
        id="RootStack"
        initialRouteName={initialRouteName}
        screenOptions={{
          animation: 'slide_from_right',
          headerShadowVisible: false,
          headerStyle: { backgroundColor: '#FFFDF9' },
          headerTitleStyle: { fontWeight: '700', color: '#1A2433' },
          headerTintColor: '#E85D2A',
        }}
      >
        <Stack.Screen name="Login" component={LoginScreen} options={{ headerShown: false, animation: 'fade_from_bottom' }} />
        <Stack.Screen name="Register" component={RegisterScreen} options={{ headerShown: false, animation: 'fade_from_bottom' }} />
        <Stack.Screen name="MainApp" component={MainTabs} options={{ headerShown: false, animation: 'fade' }} />
        <Stack.Screen name="RecipeDetail" component={RecipeDetailScreen} options={{ title: 'Tarif Detayı' }} />
        <Stack.Screen name="AddEditRecipe" component={AddEditRecipeScreen} options={{ title: 'Tarif Ekle' }} />
      </Stack.Navigator>
    </NavigationContainer>
  );
}

