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
  useWindowDimensions,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';
import api from '../services/api';

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
      const response = await api.get('/RecipesApi');
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

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView
        style={styles.container}
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >
      <StatusBar barStyle="dark-content" backgroundColor="#FFFDF9" />

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
