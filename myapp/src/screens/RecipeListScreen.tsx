import React, { useEffect, useState, useRef, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  TextInput,
  TouchableOpacity,
  ScrollView,
  Dimensions,
  Animated,
  Image,
} from 'react-native';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';
import api, { MEDIA_URL } from '../services/api';

type Props = CompositeScreenProps<
  BottomTabScreenProps<MainTabParamList, 'RecipeList'>,
  NativeStackScreenProps<RootStackParamList>
>;

const { width } = Dimensions.get('window');
const CARD_WIDTH = (width - 48 - 12) / 2; // 2 column grid

const normalizeText = (value: string) =>
  value
    .toLocaleLowerCase('tr-TR')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();

// UI'daki Türkçe kategori butonlarını API'den gelen İngilizce kategorilerle eşleştirir.
const CATEGORIES = [
  { label: 'Tümü', value: 'all', emoji: '🍽️', aliases: ['all'] },
  { label: 'Kahvaltı', value: 'kahvalti', emoji: '🍳', aliases: ['kahvalti', 'breakfast'] },
  { label: 'Ana Yemek', value: 'ana-yemek', emoji: '🍗', aliases: ['ana yemek', 'dinner', 'lunch', 'main course'] },
  { label: 'Çorba', value: 'corba', emoji: '🥣', aliases: ['corba', 'soup'] },
  { label: 'Salata', value: 'salata', emoji: '🥗', aliases: ['salata', 'salad', 'vegetarian', 'vegan'] },
  { label: 'Tatlı', value: 'tatli', emoji: '🍮', aliases: ['tatli', 'dessert'] },
  { label: 'Aperatif', value: 'aperatif', emoji: '🥟', aliases: ['aperatif', 'snack', 'starter'] },
  { label: 'İçecek', value: 'icecek', emoji: '🍹', aliases: ['icecek', 'drink', 'beverage'] },
];

function FavoriteHeartButton({
  isFavorite,
  onPress,
}: {
  isFavorite: boolean;
  onPress: () => void;
}) {
  const heartScale = useRef(new Animated.Value(1)).current;

  const handlePress = () => {
    Animated.sequence([
      Animated.spring(heartScale, {
        toValue: 1.25,
        useNativeDriver: true,
        speed: 22,
        bounciness: 10,
      }),
      Animated.spring(heartScale, {
        toValue: 1,
        useNativeDriver: true,
        speed: 20,
        bounciness: 8,
      }),
    ]).start();

    onPress();
  };

  return (
    <TouchableOpacity
      style={styles.favoriteBtn}
      onPress={handlePress}
      hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
    >
      <Animated.Text style={{ fontSize: 18, transform: [{ scale: heartScale }] }}>
        {isFavorite ? '❤️' : '🤍'}
      </Animated.Text>
    </TouchableOpacity>
  );
}

export default function RecipeListScreen({ navigation }: Props) {
  const [recipes, setRecipes] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('all');
  const [selectedIngredients, setSelectedIngredients] = useState<string[]>([]);

  // UI states
  const [showIngredientFilter, setShowIngredientFilter] = useState(false);
  const [ingredientSearchQuery, setIngredientSearchQuery] = useState('');

  // Animations
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(20)).current;

  useEffect(() => {
    fetchRecipes();
    Animated.parallel([
      Animated.timing(fadeAnim, { toValue: 1, duration: 400, useNativeDriver: true }),
      Animated.timing(slideAnim, { toValue: 0, duration: 450, useNativeDriver: true }),
    ]).start();
  }, []);

  const fetchRecipes = async () => {
    try {
      const response = await api.get('/RecipesApi');
      // response.data contains full objects based on our updated get endpoint
      setRecipes(response.data);
    } catch (error) {
      console.error('Error fetching recipes in list:', error);
      setRecipes([]);
    } finally {
      setLoading(false);
    }
  };

  const handleToggleFavorite = async (id: number) => {
    // Optimistic update
    setRecipes(prev => prev.map(r => r.id === id ? { ...r, isFavorite: !r.isFavorite } : r));
    try {
      await api.post(`/RecipesApi/${id}/toggle-favorite`);
    } catch (e) {
      // Revert optimistic update
      setRecipes(prev => prev.map(r => r.id === id ? { ...r, isFavorite: !r.isFavorite } : r));
    }
  };

  // Extract all unique ingredients from recipes for the autocomplete filter
  const allIngredients = useMemo(() => {
    const ings = new Set<string>();
    recipes.forEach(r => {
      r.ingredients?.forEach((i: any) => {
        if (i && i.name) ings.add(i.name.trim());
      });
    });
    return Array.from(ings).sort();
  }, [recipes]);

  // Apply filters
  const filteredRecipes = useMemo(() => {
    return recipes.filter((recipe) => {
      // 1. Search filter
      const matchesSearch =
        recipe.title?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        recipe.description?.toLowerCase().includes(searchQuery.toLowerCase());

      // 2. Category filter
      let matchesCategory = selectedCategory === 'all';
      if (!matchesCategory) {
        const selectedCategoryConfig = CATEGORIES.find((c) => c.value === selectedCategory);
        const aliases = selectedCategoryConfig?.aliases ?? [selectedCategory];
        const normalizedAliases = aliases.map(normalizeText);

        matchesCategory = recipe.categories?.some((cat: string) => {
          const normalizedCategory = normalizeText(cat);
          return normalizedAliases.includes(normalizedCategory);
        });
      }

      // 3. Ingredient filter
      let matchesIngredients = selectedIngredients.length === 0;
      if (!matchesIngredients) {
        // Recipe must contain AT LEAST ONE of the selected ingredients 
        // (can change this to ALL if needed)
        matchesIngredients = selectedIngredients.some(selectedIng =>
          recipe.ingredients?.some((recipeIng: any) =>
            recipeIng.name.toLowerCase().includes(selectedIng.toLowerCase())
          )
        );
      }

      return matchesSearch && matchesCategory && matchesIngredients;
    });
  }, [recipes, searchQuery, selectedCategory, selectedIngredients]);

  // Get color for card randomly or cyclically
  const getCardColor = (index: number) => {
    const colors = ['#FFF3E0', '#F3E5F5', '#E8F5E9', '#FFF8E1', '#E3F2FD', '#FCE4EC'];
    return colors[index % colors.length];
  };

  // Get valid image URI
  const getImageUrl = (imagePath?: string): string | null => {
    if (!imagePath || typeof imagePath !== 'string' || imagePath.trim() === '') return null;
    const path = imagePath.trim();
    // base64 data URI'leri ve bozuk path'leri filtrele
    if (path.includes('data:image') || path.includes(';base64')) return null;
    // Zaten tam URL ise doğrudan döndür
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    // Göreceli path'e MEDIA_URL ekle
    return path.startsWith('/') ? `${MEDIA_URL}${path}` : `${MEDIA_URL}/${path}`;
  };

  const [brokenImages, setBrokenImages] = React.useState<Set<string>>(new Set());

  const renderRecipeCard = ({ item, index }: { item: any, index: number }) => {
    const validImageUrl = brokenImages.has(item.id?.toString()) ? null : getImageUrl(item.image);

    return (
      <TouchableOpacity
        style={[styles.recipeCard, { backgroundColor: getCardColor(index) }]}
        onPress={() => navigation.navigate('RecipeDetail', { recipeId: item.id.toString() })}
        activeOpacity={0.85}
      >
        <FavoriteHeartButton
          isFavorite={item.isFavorite}
          onPress={() => handleToggleFavorite(item.id)}
        />

        {validImageUrl ? (
          <Image
            source={{ uri: validImageUrl }}
            style={styles.recipeImage}
            resizeMode="cover"
            onError={() => setBrokenImages(prev => new Set(prev).add(item.id?.toString()))}
          />
        ) : (
          <View style={styles.recipeImagePlaceholder}>
            <Text style={styles.recipeEmoji}>🍽️</Text>
          </View>
        )}

        <View style={styles.categoryBadge}>
          <Text style={styles.categoryBadgeText}>
            {item.categories && item.categories.length > 0 ? item.categories[0] : 'Tarif'}
          </Text>
        </View>

        <Text style={styles.recipeTitle} numberOfLines={2}>{item.title}</Text>

        <View style={styles.recipeMeta}>
          <Text style={styles.recipeMetaText}>👥 {item.servings || '-'} kişi</Text>
          <Text style={styles.recipeMetaText}>⏱ {item.prepTime || '- dk'}</Text>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <View style={styles.container}>
      <Animated.ScrollView
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
        style={{ opacity: fadeAnim, transform: [{ translateY: slideAnim }] }}
      >
        {/* Header Section */}
        <View style={styles.header}>
          <Text style={styles.title}>Tarifler</Text>
          <Text style={styles.subtitle}>Sizin için seçilmiş en güzel tarifler</Text>
        </View>

        {/* Search Bar */}
        <View style={styles.searchContainer}>
          <Text style={styles.searchIcon}>🔍</Text>
          <TextInput
            style={styles.searchInput}
            placeholder="Tarif ara..."
            placeholderTextColor="#999"
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity onPress={() => setSearchQuery('')}>
              <Text style={styles.clearIcon}>✕</Text>
            </TouchableOpacity>
          )}
        </View>

        {/* Category Filter */}
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.categoryRow}
          style={styles.categoryScroll}
        >
          {CATEGORIES.map((cat) => (
            <TouchableOpacity
              key={cat.value}
              style={[styles.categoryChip, selectedCategory === cat.value && styles.categoryChipActive]}
              onPress={() => setSelectedCategory(cat.value)}
              activeOpacity={0.8}
            >
              <Text style={styles.categoryChipEmoji}>{cat.emoji}</Text>
              <Text style={[styles.categoryChipText, selectedCategory === cat.value && styles.categoryChipTextActive]}>
                {cat.label}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>

        {/* Ingredient Filter Toggle */}
        <View style={styles.ingredientsFilterContainer}>
          <View style={styles.ingredientsFilterHeader}>
            <Text style={styles.ingredientsFilterLabel}>Malzemeye Göre Filtrele</Text>
            <TouchableOpacity
              style={styles.filterToggleBtn}
              onPress={() => setShowIngredientFilter(!showIngredientFilter)}
            >
              <Text style={styles.filterToggleBtnText}>
                {selectedIngredients.length > 0
                  ? `${selectedIngredients.length} Seçildi ▼`
                  : 'Filtrele ▼'}
              </Text>
            </TouchableOpacity>
          </View>

          {/* Expandable Ingredient Section */}
          {showIngredientFilter && (
            <View style={styles.ingredientsFilterContent}>

              {/* Selected Ingredients Badges */}
              {selectedIngredients.length > 0 && (
                <View style={styles.selectedIngredientsWrap}>
                  {selectedIngredients.map(ing => (
                    <TouchableOpacity
                      key={ing}
                      style={styles.selectedIngredientBadge}
                      onPress={() => setSelectedIngredients(prev => prev.filter(i => i !== ing))}
                    >
                      <Text style={styles.selectedIngredientText}>{ing}</Text>
                      <Text style={styles.selectedIngredientClose}>✕</Text>
                    </TouchableOpacity>
                  ))}
                  <TouchableOpacity onPress={() => setSelectedIngredients([])}>
                    <Text style={styles.clearAllText}>Hepsini Temizle</Text>
                  </TouchableOpacity>
                </View>
              )}

              {/* Ingredient Search */}
              <View style={styles.ingSearchWrap}>
                <TextInput
                  style={styles.ingSearchInput}
                  placeholder="Malzeme ekleyin..."
                  placeholderTextColor="#AAA"
                  value={ingredientSearchQuery}
                  onChangeText={setIngredientSearchQuery}
                />
              </View>

              {/* Ingredient Options */}
              {ingredientSearchQuery.length > 0 ? (
                <View style={styles.ingListWrap}>
                  {allIngredients
                    .filter(i => i.toLowerCase().includes(ingredientSearchQuery.toLowerCase()) && !selectedIngredients.includes(i))
                    .slice(0, 5)
                    .map(ing => (
                      <TouchableOpacity
                        key={ing}
                        style={styles.ingListItem}
                        onPress={() => {
                          setSelectedIngredients(prev => [...prev, ing]);
                          setIngredientSearchQuery('');
                        }}
                      >
                        <Text style={styles.ingListItemText}>+ {ing}</Text>
                      </TouchableOpacity>
                    ))}
                </View>
              ) : (
                /* Popular ingredients if search is empty */
                <View style={styles.popularTagsWrap}>
                  <Text style={styles.popularTagsLabel}>Önerilen Malzemeler</Text>
                  <View style={styles.popularTags}>
                    {allIngredients
                      .filter(i => !selectedIngredients.includes(i))
                      .slice(0, 8)
                      .map(ing => (
                        <TouchableOpacity
                          key={ing}
                          style={styles.popularTagBtn}
                          onPress={() => setSelectedIngredients(prev => [...prev, ing])}
                        >
                          <Text style={styles.popularTagText}>{ing}</Text>
                        </TouchableOpacity>
                      ))}
                  </View>
                </View>
              )}
            </View>
          )}
        </View>

        <View style={styles.resultsCountWrap}>
          <Text style={styles.resultsCountText}>
            {filteredRecipes.length} tarif bulundu
          </Text>
        </View>

        {/* Recipe Grid */}
        {loading ? (
          <ActivityIndicator size="large" color="#FF6B35" style={{ marginTop: 40 }} />
        ) : filteredRecipes.length > 0 ? (
          <FlatList
            data={filteredRecipes}
            renderItem={renderRecipeCard}
            keyExtractor={(item) => item.id.toString()}
            numColumns={2}
            columnWrapperStyle={styles.row}
            scrollEnabled={false}
            contentContainerStyle={styles.gridContainer}
          />
        ) : (
          <View style={styles.emptyState}>
            <Text style={styles.emptyEmoji}>🍽️</Text>
            <Text style={styles.emptyTitle}>Sonuç bulunamadı</Text>
            <Text style={styles.emptyDesc}>Filtreleri veya arama terimini değiştirin.</Text>
          </View>
        )}
      </Animated.ScrollView>

      {/* FAB (Floating Action Button) for Adding Recipe */}
      <TouchableOpacity
        style={styles.fabBtn}
        activeOpacity={0.85}
        onPress={() => navigation.navigate('AddEditRecipe')}
      >
        <Text style={styles.fabText}>+ Yeni Tarif</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FAFAFA',
  },
  scrollContent: {
    paddingBottom: 100, // Extra padding for FAB
  },

  /* Header */
  header: {
    paddingHorizontal: 16,
    paddingTop: 50,
    paddingBottom: 10,
    backgroundColor: '#FFFFFF',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 10,
    elevation: 3,
  },
  title: {
    fontSize: 26,
    fontWeight: '800',
    color: '#1A1A1A',
    letterSpacing: -0.5,
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
    marginBottom: 8,
  },

  /* Search */
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    marginHorizontal: 16,
    marginTop: 20,
    borderRadius: 16,
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderWidth: 1,
    borderColor: '#EFEFEF',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 4,
    elevation: 1,
  },
  searchIcon: {
    fontSize: 16,
    marginRight: 10,
  },
  searchInput: {
    flex: 1,
    fontSize: 15,
    color: '#1A1A1A',
    paddingVertical: 0, // fix for Android 
  },
  clearIcon: {
    fontSize: 16,
    color: '#999',
    padding: 4,
  },

  /* Categories */
  categoryScroll: {
    marginTop: 20,
  },
  categoryRow: {
    paddingHorizontal: 16,
    gap: 10,
  },
  categoryChip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#EFEFEF',
    gap: 6,
  },
  categoryChipActive: {
    backgroundColor: '#1A1A1A',
    borderColor: '#1A1A1A',
  },
  categoryChipEmoji: {
    fontSize: 14,
  },
  categoryChipText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#444',
  },
  categoryChipTextActive: {
    color: '#FFFFFF',
  },

  /* Ingredient Filter */
  ingredientsFilterContainer: {
    marginHorizontal: 16,
    marginTop: 20,
  },
  ingredientsFilterHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  ingredientsFilterLabel: {
    fontSize: 15,
    fontWeight: '700',
    color: '#1A1A1A',
  },
  filterToggleBtn: {
    backgroundColor: '#F0F0F0',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 12,
  },
  filterToggleBtnText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#444',
  },
  ingredientsFilterContent: {
    marginTop: 12,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 14,
    borderWidth: 1,
    borderColor: '#EFEFEF',
  },
  selectedIngredientsWrap: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 12,
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#F0F0F0',
    alignItems: 'center',
  },
  selectedIngredientBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFF3E0',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 10,
    gap: 6,
  },
  selectedIngredientText: {
    fontSize: 13,
    color: '#FF6B35',
    fontWeight: '600',
  },
  selectedIngredientClose: {
    fontSize: 12,
    color: '#FF6B35',
  },
  clearAllText: {
    fontSize: 12,
    color: '#999',
    textDecorationLine: 'underline',
    marginLeft: 4,
  },
  ingSearchWrap: {
    backgroundColor: '#F9F9F9',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  ingSearchInput: {
    fontSize: 14,
    paddingVertical: 0,
    color: '#1A1A1A',
  },
  ingListWrap: {
    marginTop: 8,
  },
  ingListItem: {
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#F5F5F5',
  },
  ingListItemText: {
    fontSize: 14,
    color: '#444',
  },
  popularTagsWrap: {
    marginTop: 12,
  },
  popularTagsLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: '#999',
    textTransform: 'uppercase',
    marginBottom: 8,
  },
  popularTags: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  popularTagBtn: {
    borderWidth: 1,
    borderColor: '#EFEFEF',
    borderStyle: 'dashed',
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  popularTagText: {
    fontSize: 12,
    color: '#666',
  },

  /* Results Info */
  resultsCountWrap: {
    marginHorizontal: 16,
    marginTop: 20,
    marginBottom: 8,
  },
  resultsCountText: {
    fontSize: 13,
    color: '#888',
    fontWeight: '500',
  },

  /* Grid */
  gridContainer: {
    paddingHorizontal: 16,
  },
  row: {
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  recipeCard: {
    width: CARD_WIDTH,
    borderRadius: 18,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 5,
    elevation: 2,
    position: 'relative',
    overflow: 'hidden',
  },
  recipeImagePlaceholder: {
    width: '100%',
    height: 80,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.4)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 10,
  },
  recipeImage: {
    width: '100%',
    height: 80,
    borderRadius: 12,
    marginBottom: 10,
  },
  recipeEmoji: {
    fontSize: 34,
  },
  favoriteBtn: {
    position: 'absolute',
    top: 8,
    right: 8,
    zIndex: 10,
    backgroundColor: 'rgba(255,255,255,0.8)',
    borderRadius: 15,
    width: 30,
    height: 30,
    alignItems: 'center',
    justifyContent: 'center',
  },
  categoryBadge: {
    alignSelf: 'flex-start',
    backgroundColor: 'rgba(255,255,255,0.7)',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 3,
    marginBottom: 6,
  },
  categoryBadgeText: {
    fontSize: 9,
    fontWeight: '700',
    color: '#444',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  recipeTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1A1A1A',
    lineHeight: 18,
    marginBottom: 12,
    minHeight: 36, // Force min height for 2 lines to align cards mostly
  },
  recipeMeta: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 'auto',
  },
  recipeMetaText: {
    fontSize: 10,
    color: '#777',
    fontWeight: '600',
  },

  /* Empty State */
  emptyState: {
    alignItems: 'center',
    paddingTop: 60,
  },
  emptyEmoji: {
    fontSize: 48,
    marginBottom: 14,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1A1A1A',
    marginBottom: 6,
  },
  emptyDesc: {
    fontSize: 14,
    color: '#777',
  },

  /* FAB */
  fabBtn: {
    position: 'absolute',
    bottom: 24,
    right: 20,
    backgroundColor: '#FF6B35',
    paddingHorizontal: 20,
    paddingVertical: 14,
    borderRadius: 30,
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.4,
    shadowRadius: 8,
    elevation: 5,
  },
  fabText: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: '700',
  },
});
