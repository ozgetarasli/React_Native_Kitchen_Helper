import React, { useRef, useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Animated,
  FlatList,
  StatusBar,
  Alert,
  useWindowDimensions,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';
import api from '../services/api';
import { clearSession } from '../services/authSession';

type Props = CompositeScreenProps<
  BottomTabScreenProps<MainTabParamList, 'Home'>,
  NativeStackScreenProps<RootStackParamList>
>;

const ACTION_BUTTONS = [
  { label: 'Tarif Yaz', icon: '✍️', screen: 'AddEditRecipe' as const, primary: true },
  { label: 'Tariflerim',      icon: '📖', screen: 'RecipeList'   as const, primary: false },
  { label: 'Alışveriş\nListesi',       icon: '🛒', screen: 'ShoppingList' as const, primary: false },
];

export default function HomeScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const isCompact = width < 380;
  const numColumns = width < 360 ? 1 : 2;
  const horizontalPadding = isCompact ? 12 : 16;
  const cardGap = 12;
  const cardWidth = numColumns === 1 ? width - horizontalPadding * 2 : (width - horizontalPadding * 2 - cardGap) / 2;

  const fadeAnim  = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(24)).current;
  const cardAnim  = useRef(new Animated.Value(0)).current;

  const [recipes, setRecipes] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showSettingsMenu, setShowSettingsMenu] = useState(false);
  const settingsMenuAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.sequence([
      Animated.parallel([
        Animated.timing(fadeAnim,  { toValue: 1, duration: 500, useNativeDriver: true }),
        Animated.timing(slideAnim, { toValue: 0, duration: 450, useNativeDriver: true }),
      ]),
      Animated.timing(cardAnim, { toValue: 1, duration: 400, useNativeDriver: true }),
    ]).start();

    fetchRecipes();
  }, []);

  const fetchRecipes = async () => {
    try {
      const pantryResponse = await api.get('/PantryApi');
      const pantryIngredients = Array.isArray(pantryResponse.data)
        ? pantryResponse.data
            .map((item: any) => item?.name)
            .filter((name: any) => typeof name === 'string' && name.trim().length > 0)
        : [];

      const response = pantryIngredients.length > 0
        ? await api.post('/RecipesApi/search', { have: pantryIngredients })
        : await api.get('/RecipesApi');

      // Format backend recipes to match the mock structure
      const formatted = response.data.slice(0, 6).map((r: any, index: number) => ({
        id: r.id.toString(),
        title: r.title,
        category: r.categories && r.categories.length > 0 ? r.categories[0] : 'Diğer',
        servings: r.servings || 4,
        emoji: '🍽️', 
        color: ['#FFF3E0', '#F3E5F5', '#E8F5E9', '#FFF8E1', '#E3F2FD', '#FCE4EC'][index % 6],
        duration: r.prepTime || '30 dk'
      }));
      setRecipes(formatted);
    } catch (error) {
      console.error('Error fetching recipes:', error);
      // Removed fallback to mock recipes
      setRecipes([]);
    } finally {
      setLoading(false);
    }
  };

  const renderRecipeCard = ({ item }: { item: any }) => (
    <TouchableOpacity
      style={[styles.recipeCard, { backgroundColor: item.color, width: cardWidth }]}
      onPress={() => navigation.navigate('RecipeDetail', { recipeId: item.id })}
      activeOpacity={0.82}
    >
      <Text style={styles.recipeEmoji}>{item.emoji}</Text>
      <View style={styles.categoryBadge}>
        <Text style={styles.categoryBadgeText}>{item.category}</Text>
      </View>
      <Text style={styles.recipeTitle} numberOfLines={2}>{item.title}</Text>
      <View style={styles.recipeMeta}>
        <Text style={styles.recipeMetaText}>👥 {item.servings} kişi</Text>
        <Text style={styles.recipeMetaText}>⏱ {item.duration}</Text>
      </View>
    </TouchableOpacity>
  );

  const handleLogout = () => {
    setShowSettingsMenu(false);
    settingsMenuAnim.setValue(0);

    Alert.alert('Cikis Yap', 'Uygulamadan cikis yapmak istiyor musunuz?', [
      { text: 'Iptal', style: 'cancel' },
      {
        text: 'Cikis Yap',
        style: 'destructive',
        onPress: async () => {
          await clearSession();
          navigation.reset({
            index: 0,
            routes: [{ name: 'Login' }],
          });
        },
      },
    ]);
  };

  const toggleSettingsMenu = () => {
    const nextValue = !showSettingsMenu;
    setShowSettingsMenu(nextValue);

    Animated.spring(settingsMenuAnim, {
      toValue: nextValue ? 1 : 0,
      useNativeDriver: true,
      speed: 20,
      bounciness: 6,
    }).start();
  };

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="dark-content" backgroundColor="#FFFDF9" />
      <ScrollView
        style={styles.container}
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >


      {/* ── Hero Section ── */}
      <Animated.View
        style={[
          styles.heroSection,
          {
            opacity: fadeAnim,
            transform: [{ translateY: slideAnim }],
            marginHorizontal: horizontalPadding,
            marginTop: 12,
            padding: isCompact ? 16 : 20,
          },
        ]}
      >
        {/* Brand Row */}
        <View style={styles.brandRow}>
          <View style={styles.logoCircle}>
            <Text style={styles.logoEmoji}>👨‍🍳</Text>
          </View>
          <View style={styles.brandText}>
            <Text style={styles.heroTitle}>KitchenHelper</Text>
          </View>
          <View style={styles.settingsWrap}>
            <TouchableOpacity style={styles.settingsButton} onPress={toggleSettingsMenu} activeOpacity={0.85}>
              <Text style={styles.settingsButtonIcon}>⚙️</Text>
              <Text style={styles.settingsButtonText}>Ayarlar</Text>
            </TouchableOpacity>

            {showSettingsMenu && (
              <Animated.View
                style={[
                  styles.settingsMenu,
                  {
                    opacity: settingsMenuAnim,
                    transform: [
                      {
                        translateY: settingsMenuAnim.interpolate({
                          inputRange: [0, 1],
                          outputRange: [-10, 0],
                        }),
                      },
                      {
                        scale: settingsMenuAnim.interpolate({
                          inputRange: [0, 1],
                          outputRange: [0.96, 1],
                        }),
                      },
                    ],
                  },
                ]}
              >
                <Text style={styles.settingsMenuLabel}>Hesap</Text>
                <TouchableOpacity style={styles.settingsMenuItem} onPress={handleLogout} activeOpacity={0.82}>
                  <Text style={styles.settingsMenuItemIcon}>↩</Text>
                  <Text style={styles.settingsMenuItemText}>Cikis Yap</Text>
                </TouchableOpacity>
              </Animated.View>
            )}
          </View>
        </View>

        {/* <Text style={styles.heroDesc}>
          Yemek tariflerinizi yönetin, videolarınızı saniyeler içinde tarife dönüştürün ve mutfaktaki işlerinizi kolaylaştırın.
        </Text> */}

        {/* Action Buttons */}
        <View style={[styles.actionRow, { gap: isCompact ? 8 : 10 }]}> 
          {ACTION_BUTTONS.map((btn) => (
            <TouchableOpacity
              key={btn.label}
              style={[styles.actionBtn, btn.primary ? styles.actionBtnPrimary : styles.actionBtnSecondary]}
              onPress={() => navigation.navigate(btn.screen as any)}
              activeOpacity={0.85}
            >
              <Text style={styles.actionBtnIcon}>{btn.icon}</Text>
              <Text style={[styles.actionBtnLabel, btn.primary ? styles.actionBtnLabelPrimary : styles.actionBtnLabelSecondary]}>
                {btn.label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </Animated.View>

      {/* ── Popular Recipes Section ── */}
      <Animated.View style={{ opacity: cardAnim }}>
        <View style={styles.sectionHeader}>
          <Text style={styles.sectionTitle}>Popüler Tarifler</Text>
          <TouchableOpacity onPress={() => navigation.navigate('RecipeList')}>
            <Text style={styles.seeAllText}>Tümünü Gör →</Text>
          </TouchableOpacity>
        </View>

        <FlatList
          key={`home-grid-${numColumns}`}
          data={recipes}
          renderItem={renderRecipeCard}
          keyExtractor={(item) => item.id}
          numColumns={numColumns}
          columnWrapperStyle={numColumns > 1 ? styles.row : undefined}
          scrollEnabled={false}
          contentContainerStyle={[styles.grid, { paddingHorizontal: horizontalPadding }]}
          ListEmptyComponent={
            !loading ? (
              <View style={{ alignItems: 'center', marginTop: 40 }}>
                <Text style={{ fontSize: 40 }}>🍽️</Text>
                <Text style={{ fontSize: 16, color: '#666', marginTop: 10 }}>Henüz tarif bulunmuyor.</Text>
              </View>
            ) : null
          }
        />
      </Animated.View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFDF9',
  },
  scrollContent: {
    paddingBottom: 24,
  },

  /* Hero */
  heroSection: {
    backgroundColor: '#FFFFFF',
    borderRadius: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.07,
    shadowRadius: 8,
    elevation: 3,
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 15,
  },
  logoCircle: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#FFF3E0',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
    elevation: 2,
  },
  logoEmoji: {
    fontSize: 28,
  },
  brandText: {
    flex: 1,
  },
  heroTitle: {
    fontSize: 24,
    fontWeight: '800',
    color: '#1A1A1A',
    letterSpacing: -0.5,
  },
  settingsWrap: {
    position: 'relative',
  },
  settingsButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: '#F7F8FA',
    borderWidth: 1,
    borderColor: '#E7EAF0',
    borderRadius: 14,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  settingsButtonIcon: {
    fontSize: 13,
  },
  settingsButtonText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#415066',
  },
  settingsMenu: {
    position: 'absolute',
    top: 46,
    right: 0,
    minWidth: 170,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 8,
    borderWidth: 1,
    borderColor: '#ECEFF4',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.08,
    shadowRadius: 18,
    elevation: 6,
    zIndex: 20,
  },
  settingsMenuLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: '#8B94A5',
    textTransform: 'uppercase',
    letterSpacing: 0.8,
    paddingHorizontal: 10,
    paddingTop: 4,
    paddingBottom: 8,
  },
  settingsMenuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    borderRadius: 12,
    paddingHorizontal: 10,
    paddingVertical: 10,
    backgroundColor: '#FFF4F1',
  },
  settingsMenuItemIcon: {
    fontSize: 14,
    color: '#BE3B2B',
  },
  settingsMenuItemText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#BE3B2B',
  },
  heroSubtitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#FF6B35',
    marginTop: 1,
  },
  heroDesc: {
    fontSize: 13,
    color: '#666',
    lineHeight: 20,
    marginBottom: 18,
  },

  /* Action Buttons */
  actionRow: {
    flexDirection: 'row',
    gap: 10,
  },
  actionBtn: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 14,
    minHeight: 68,
  },
  actionBtnPrimary: {
    backgroundColor: '#FF6B35',
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.35,
    shadowRadius: 6,
    elevation: 4,
  },
  actionBtnSecondary: {
    backgroundColor: '#F5F5F5',
    borderWidth: 1,
    borderColor: '#EBEBEB',
  },
  actionBtnIcon: {
    fontSize: 20,
    marginBottom: 4,
  },
  actionBtnLabel: {
    fontSize: 11,
    fontWeight: '600',
    textAlign: 'center',
    lineHeight: 15,
  },
  actionBtnLabelPrimary: {
    color: '#FFFFFF',
  },
  actionBtnLabelSecondary: {
    color: '#444',
  },

  /* Section Header */
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginHorizontal: 16,
    marginTop: 28,
    marginBottom: 14,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1A1A1A',
  },
  seeAllText: {
    fontSize: 13,
    color: '#FF6B35',
    fontWeight: '600',
  },

  /* Recipe Grid */
  grid: {
    paddingHorizontal: 16,
  },
  row: {
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  recipeCard: {
    borderRadius: 16,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.06,
    shadowRadius: 6,
    elevation: 2,
  },
  recipeEmoji: {
    fontSize: 32,
    marginBottom: 8,
  },
  categoryBadge: {
    alignSelf: 'flex-start',
    backgroundColor: 'rgba(255,255,255,0.75)',
    borderRadius: 8,
    paddingHorizontal: 7,
    paddingVertical: 2,
    marginBottom: 6,
  },
  categoryBadgeText: {
    fontSize: 10,
    fontWeight: '600',
    color: '#555',
    textTransform: 'uppercase',
    letterSpacing: 0.4,
  },
  recipeTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1A1A1A',
    lineHeight: 19,
    marginBottom: 20,
  },
  recipeMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  recipeMetaText: {
    fontSize: 10,
    color: '#777',
    fontWeight: '500',
  },
});
