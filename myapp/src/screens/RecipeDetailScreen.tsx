import React, { useEffect, useState, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Image,
  Animated,
  Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { RootStackParamList } from '../types/navigation';
import { fetchRecipeDetail, toggleRecipeFavorite, RecipeDetailResponse } from '../services/recipeService';
import { fetchUserPantryItems, checkIngredientMatch, calculateMatchPercentage } from '../services/pantryService';
import api, { MEDIA_URL } from '../services/api';
import { canUseProtectedFeatures } from '../services/authSession';

type Props = NativeStackScreenProps<RootStackParamList, 'RecipeDetail'>;

interface Step {
  number: number;
  text: string;
}

export default function RecipeDetailScreen({ route, navigation }: Props) {
  const { recipeId } = route.params || {};

  const [recipe, setRecipe] = useState<RecipeDetailResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ingredientMatches, setIngredientMatches] = useState<Map<string, boolean>>(new Map());
  const [matchPercentage, setMatchPercentage] = useState({ matched: 0, total: 0, percentage: 0 });
  const [steps, setSteps] = useState<Step[]>([]);
  const [showFullDescription, setShowFullDescription] = useState(false);
  const [selectedIngredients, setSelectedIngredients] = useState<string[]>([]);
  const [addingToShoppingList, setAddingToShoppingList] = useState(false);
  const [shoppingFeedback, setShoppingFeedback] = useState<{ message: string; type: 'success' | 'info' | 'error' } | null>(null);

  // Animations
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(30)).current;
  const scaleAnim = useRef(new Animated.Value(0.95)).current;
  const favoriteScaleAnim = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    loadRecipeDetail();
    
    // Start animations
    Animated.parallel([
      Animated.timing(fadeAnim, { toValue: 1, duration: 400, useNativeDriver: true }),
      Animated.timing(slideAnim, { toValue: 0, duration: 450, useNativeDriver: true }),
      Animated.timing(scaleAnim, { toValue: 1, duration: 400, useNativeDriver: true }),
    ]).start();
  }, [recipeId]);

  useEffect(() => {
    if (!shoppingFeedback) return;

    const timeoutId = setTimeout(() => {
      setShoppingFeedback(null);
    }, 2500);

    return () => clearTimeout(timeoutId);
  }, [shoppingFeedback]);

  const loadRecipeDetail = async () => {
    try {
      setLoading(true);
      setError(null);

      // Fetch recipe and pantry items in parallel
      const [recipeData, pantryItems] = await Promise.all([
        fetchRecipeDetail(recipeId.toString()),
        fetchUserPantryItems(),
      ]);

      console.log('📚 Recipe Data:', recipeData);
      console.log('🛒 Pantry Items:', pantryItems);

      // Validate recipe data
      if (!recipeData) {
        throw new Error('Tarif verisi alınamadı');
      }

      setSelectedIngredients([]);
      setShoppingFeedback(null);
      setRecipe(recipeData);

      // Safely check ingredient matches
      if (recipeData.ingredients && Array.isArray(recipeData.ingredients)) {
        console.log('🎯 Starting ingredient matching...');
        const matches = checkIngredientMatch(recipeData.ingredients, pantryItems || []);
        setIngredientMatches(matches);
        const matchStats = calculateMatchPercentage(matches);
        console.log('✅ Match stats:', matchStats);
        setMatchPercentage(matchStats);
      } else {
        // No ingredients found
        console.warn('⚠️ No ingredients found in recipe');
        setIngredientMatches(new Map());
        setMatchPercentage({ matched: 0, total: 0, percentage: 0 });
      }

      // Parse steps from markdown or instructions array
      if (recipeData.instructions && Array.isArray(recipeData.instructions)) {
        const stepsFromInstructions = recipeData.instructions.map((instruction, idx) => ({
          number: idx + 1,
          text: instruction,
        }));
        setSteps(stepsFromInstructions);
        console.log('📝 Steps from instructions:', stepsFromInstructions);
      } else {
        parseSteps(recipeData.stepsMarkdown);
      }
    } catch (err) {
      setError('Tarif yüklenirken bir hata oluştu. Lütfen tekrar deneyin.');
      console.error('Error loading recipe:', err);
    } finally {
      setLoading(false);
    }
  };

  const parseSteps = (markdownText?: string) => {
    if (!markdownText) {
      setSteps([]);
      return;
    }

    // Parse markdown steps (assuming format: "1. Step one\n2. Step two\n...")
    const stepRegex = /^\s*(?:\d+\.|[-*])\s+(.+)$/gm;
    const parsedSteps: Step[] = [];
    let match;
    let stepNumber = 1;

    while ((match = stepRegex.exec(markdownText)) !== null) {
      parsedSteps.push({
        number: stepNumber++,
        text: match[1].trim(),
      });
    }

    // If no numbered steps found, split by newlines
    if (parsedSteps.length === 0) {
      const lines = markdownText
        .split('\n')
        .map(line => line.trim())
        .filter(line => line.length > 0);

      lines.forEach((line, index) => {
        parsedSteps.push({
          number: index + 1,
          text: line,
        });
      });
    }

    setSteps(parsedSteps);
  };

  const handleToggleFavorite = async () => {
    if (!recipe) return;

    const authCheck = await canUseProtectedFeatures();
    if (!authCheck.allowed) {
      Alert.alert('Giris gerekli', authCheck.message);
      return;
    }

    Animated.sequence([
      Animated.spring(favoriteScaleAnim, {
        toValue: 1.25,
        useNativeDriver: true,
        speed: 22,
        bounciness: 10,
      }),
      Animated.spring(favoriteScaleAnim, {
        toValue: 1,
        useNativeDriver: true,
        speed: 20,
        bounciness: 8,
      }),
    ]).start();

    try {
      // Optimistic update
      const newRecipe = { ...recipe, isFavorite: !recipe.isFavorite };
      setRecipe(newRecipe);

      // API call
      await toggleRecipeFavorite(recipeId.toString());
    } catch (error) {
      // Revert on error
      setRecipe(recipe);
      console.error('Error toggling favorite:', error);
    }
  };

  const getImageUrl = (imagePath?: string): string | null => {
    if (!imagePath || typeof imagePath !== 'string' || imagePath.trim() === '') return null;
    const path = imagePath.trim();
    if (path.includes('data:image') || path.includes(';base64')) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    return path.startsWith('/') ? `${MEDIA_URL}${path}` : `${MEDIA_URL}/${path}`;
  };

  const normalizeText = (value: string) =>
    value
      .toLocaleLowerCase('tr-TR')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim();

  const toggleIngredientSelection = (ingredientName: string) => {
    setSelectedIngredients((prev) =>
      prev.includes(ingredientName)
        ? prev.filter((name) => name !== ingredientName)
        : [...prev, ingredientName]
    );
  };

  const handleAddSelectedToShoppingList = async (
    selectedNames: string[],
    ingredientMap: Map<string, { amount?: string }>
  ) => {
    if (selectedNames.length === 0 || addingToShoppingList) return;

    const authCheck = await canUseProtectedFeatures();
    if (!authCheck.allowed) {
      setShoppingFeedback({ message: authCheck.message, type: 'error' });
      return;
    }

    setAddingToShoppingList(true);
    try {
      const existingResponse = await api.get('/ShoppingListApi');
      const existingNames = new Set<string>(
        (existingResponse.data || [])
          .map((item: any) => item?.name)
          .filter((name: string) => typeof name === 'string' && name.trim().length > 0)
          .map((name: string) => normalizeText(name))
      );

      const toAdd = selectedNames.filter((name) => !existingNames.has(normalizeText(name)));

      if (toAdd.length === 0) {
        setShoppingFeedback({ message: 'Seçtiğiniz malzemeler zaten alışveriş listesinde.', type: 'info' });
        return;
      }

      await Promise.all(
        toAdd.map((name) => {
          const amount = ingredientMap.get(name)?.amount;
          return api.post('/ShoppingListApi', {
            name,
            quantity: amount,
            category: 'Recipe Ingredients',
          });
        })
      );

      setSelectedIngredients((prev) => prev.filter((name) => !toAdd.includes(name)));
      setShoppingFeedback({ message: `${toAdd.length} malzeme alışveriş listesine eklendi.`, type: 'success' });
    } catch (e) {
      console.error('Error adding selected ingredients to shopping list:', e);
      setShoppingFeedback({ message: 'Malzemeler alışveriş listesine eklenemedi.', type: 'error' });
    } finally {
      setAddingToShoppingList(false);
    }
  };

  const renderHeader = () => {
    if (!recipe) return null;

    return (
      <Animated.View style={{ opacity: fadeAnim, transform: [{ translateY: slideAnim }, { scale: scaleAnim }] }}>
        {/* Image */}
        <View style={styles.imageContainer}>
          {getImageUrl(recipe.image) ? (
            <Image
              source={{ uri: getImageUrl(recipe.image)! }}
              style={styles.image}
              resizeMode="cover"
            />
          ) : (
            <View style={[styles.image, styles.imagePlaceholder]}>
              <Text style={styles.imagePlaceholderEmoji}>🍽️</Text>
            </View>
          )}

          {/* Favorite Button */}
          <TouchableOpacity
            style={styles.favoriteButton}
            onPress={handleToggleFavorite}
            activeOpacity={0.7}
          >
            <Animated.Text style={[styles.favoriteEmoji, { transform: [{ scale: favoriteScaleAnim }] }]}>
              {recipe.isFavorite ? '❤️' : '🤍'}
            </Animated.Text>
          </TouchableOpacity>

          {/* Back Button */}
          <TouchableOpacity
            style={styles.backButton}
            onPress={() => navigation.goBack()}
            activeOpacity={0.7}
          >
            <Text style={styles.backButtonText}>‹</Text>
          </TouchableOpacity>
        </View>

        {/* Title & Meta */}
        <View style={styles.titleSection}>
          <Text style={styles.title}>{recipe.title || 'Tarif'}</Text>

          {/* Categories */}
          {recipe.categories && Array.isArray(recipe.categories) && recipe.categories.length > 0 && (
            <View style={styles.categoriesRow}>
              {recipe.categories.map((cat, idx) => (
                <View key={idx} style={styles.categoryBadge}>
                  <Text style={styles.categoryBadgeText}>{cat}</Text>
                </View>
              ))}
            </View>
          )}

          {/* Meta Info */}
          <View style={styles.metaRow}>
            <View style={styles.metaItem}>
              <Text style={styles.metaIcon}>👥</Text>
              <Text style={styles.metaText}>{recipe.servings || '-'} kişi</Text>
            </View>
            <View style={styles.metaItem}>
              <Text style={styles.metaIcon}>⏱️</Text>
              <Text style={styles.metaText}>{recipe.prepTime || '- dk'}</Text>
            </View>
            {recipe.calories && typeof recipe.calories === 'number' && (
              <View style={styles.metaItem}>
                <Text style={styles.metaIcon}>🔥</Text>
                <Text style={styles.metaText}>{recipe.calories.toFixed(0)} kcal</Text>
              </View>
            )}
          </View>

          {/* Description */}
          {recipe.description && typeof recipe.description === 'string' && (
            <View style={styles.descriptionContainer}>
              <Text
                style={styles.description}
                numberOfLines={showFullDescription ? undefined : 2}
              >
                {recipe.description}
              </Text>
              {recipe.description.length > 100 && (
                <TouchableOpacity onPress={() => setShowFullDescription(!showFullDescription)}>
                  <Text style={styles.readMoreText}>
                    {showFullDescription ? 'Daha az göster' : 'Daha fazla göster'}
                  </Text>
                </TouchableOpacity>
              )}
            </View>
          )}
        </View>
      </Animated.View>
    );
  };

  const renderIngredientSection = () => {
    if (!recipe || !recipe.ingredients || recipe.ingredients.length === 0) return null;

    // Filter valid ingredients
    const validIngredients = recipe.ingredients.filter(ing => ing && ing.name);

    if (validIngredients.length === 0) return null;

    const ingredientMap = new Map<string, { amount?: string }>(
      validIngredients.map((ing) => [ing.name, { amount: ing.amount }])
    );

    return (
      <View style={styles.section}>
        <View style={styles.sectionHeader}>
          <Text style={styles.sectionTitle}>📦 Malzemeler</Text>
          <TouchableOpacity
            style={[
              styles.inlineAddButton,
              (selectedIngredients.length === 0 || addingToShoppingList) && styles.inlineAddButtonDisabled,
            ]}
            onPress={() => handleAddSelectedToShoppingList(selectedIngredients, ingredientMap)}
            activeOpacity={0.85}
            disabled={selectedIngredients.length === 0 || addingToShoppingList}
          >
            <Text style={styles.inlineAddButtonText}>
              {addingToShoppingList
                ? 'Ekleniyor...'
                : `Alışverişe Ekle (${selectedIngredients.length})`}
            </Text>
          </TouchableOpacity>
        </View>

        {/* Match Indicator */}
        <View style={styles.progressBar}>
          <View
            style={[
              styles.progressFill,
              {
                width: `${matchPercentage.percentage}%`,
                backgroundColor: getMatchColor(matchPercentage.percentage),
              },
            ]}
          />
        </View>

        <Text style={styles.matchText}>
          {matchPercentage.matched} / {matchPercentage.total} malzeme pantryinizde var
        </Text>

        {shoppingFeedback && (
          <View
            style={[
              styles.feedbackChip,
              shoppingFeedback.type === 'success' && styles.feedbackChipSuccess,
              shoppingFeedback.type === 'info' && styles.feedbackChipInfo,
              shoppingFeedback.type === 'error' && styles.feedbackChipError,
            ]}
          >
            <Text style={styles.feedbackChipText}>{shoppingFeedback.message}</Text>
          </View>
        )}

        {/* Ingredients List */}
        <View style={styles.ingredientsList}>
          {validIngredients.map((ingredient, index) => {
            const ingredientName = ingredient.name || 'Bilinmeyen Malzeme';
            const hasMatch = ingredientMatches.get(ingredientName) || false;

            const isSelected = selectedIngredients.includes(ingredientName);

            return (
              <TouchableOpacity
                key={`${ingredientName}-${index}`}
                style={[styles.ingredientItem, isSelected && styles.ingredientItemSelected]}
                onPress={() => toggleIngredientSelection(ingredientName)}
                activeOpacity={0.8}
              >
                <View style={[styles.ingredientIndicator, hasMatch && styles.ingredientIndicatorMatched]}>
                  <Text style={styles.ingredientIndicatorText}>{hasMatch ? '✓' : '○'}</Text>
                </View>

                <View style={styles.ingredientContent}>
                  <Text
                    style={[
                      styles.ingredientName,
                      hasMatch && styles.ingredientNameMatched,
                    ]}
                  >
                    {ingredientName}
                  </Text>
                </View>

                {(ingredient.amount || ingredient.quantity) && (
                  <Text style={styles.ingredientQuantity}>
                    {ingredient.amount || `${ingredient.quantity} ${ingredient.unit || ''}`}
                  </Text>
                )}

                <Text style={[styles.selectionMark, isSelected && styles.selectionMarkSelected]}>
                  {isSelected ? '☑' : '☐'}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      </View>
    );
  };

  const renderStepsSection = () => {
    if (steps.length === 0) return null;

    return (
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>👨‍🍳 Hazırlama Adımları</Text>

        <View style={styles.stepsList}>
          {steps.map((step) => (
            <View key={step.number} style={styles.stepItem}>
              <View style={styles.stepNumber}>
                <Text style={styles.stepNumberText}>{step.number}</Text>
              </View>
              <Text style={styles.stepText}>{step.text}</Text>
            </View>
          ))}
        </View>
      </View>
    );
  };

  const renderNutritionSection = () => {
    if (!recipe) return null;
    
    const hasNutrition = 
      (recipe.calories && typeof recipe.calories === 'number') ||
      (recipe.protein && typeof recipe.protein === 'number') ||
      (recipe.fat && typeof recipe.fat === 'number') ||
      (recipe.carbs && typeof recipe.carbs === 'number');

    if (!hasNutrition) {
      return null;
    }

    return (
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>🥗 Besin Bilgileri</Text>

        <View style={styles.nutritionGrid}>
          {recipe.calories && typeof recipe.calories === 'number' && (
            <View style={styles.nutritionItem}>
              <Text style={styles.nutritionValue}>{recipe.calories.toFixed(0)}</Text>
              <Text style={styles.nutritionLabel}>Kalori</Text>
            </View>
          )}
          {recipe.protein && typeof recipe.protein === 'number' && (
            <View style={styles.nutritionItem}>
              <Text style={styles.nutritionValue}>{recipe.protein.toFixed(1)}g</Text>
              <Text style={styles.nutritionLabel}>Protein</Text>
            </View>
          )}
          {recipe.fat && typeof recipe.fat === 'number' && (
            <View style={styles.nutritionItem}>
              <Text style={styles.nutritionValue}>{recipe.fat.toFixed(1)}g</Text>
              <Text style={styles.nutritionLabel}>Yağ</Text>
            </View>
          )}
          {recipe.carbs && typeof recipe.carbs === 'number' && (
            <View style={styles.nutritionItem}>
              <Text style={styles.nutritionValue}>{recipe.carbs.toFixed(1)}g</Text>
              <Text style={styles.nutritionLabel}>Karbohidrat</Text>
            </View>
          )}
        </View>
      </View>
    );
  };

  const getMatchColor = (percentage: number): string => {
    if (percentage >= 80) return '#4CAF50'; // Green
    if (percentage >= 50) return '#FF9800'; // Orange
    return '#F44336'; // Red
  };

  if (loading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="large" color="#FF6B35" style={{ marginTop: 40 }} />
      </View>
    );
  }

  if (error || !recipe) {
    return (
      <View style={[styles.container, styles.centerContent]}>
        <Text style={styles.errorEmoji}>😞</Text>
        <Text style={styles.errorTitle}>Hata</Text>
        <Text style={styles.errorText}>{error || 'Tarif bulunamadı'}</Text>
        <TouchableOpacity
          style={styles.retryButton}
          onPress={() => loadRecipeDetail()}
          activeOpacity={0.8}
        >
          <Text style={styles.retryButtonText}>Tekrar Dene</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView
        showsVerticalScrollIndicator={false}
        scrollEventThrottle={16}
        contentContainerStyle={styles.scrollContent}
      >
        {renderHeader()}
        {renderIngredientSection()}
        {renderStepsSection()}
        {renderNutritionSection()}

        {/* Action Buttons */}
        <View style={styles.actionsContainer}>
          <TouchableOpacity
            style={styles.actionButton}
            onPress={() => navigation.navigate('AddEditRecipe', { recipeId: recipeId.toString() })}
            activeOpacity={0.8}
          >
            <Text style={styles.actionButtonIcon}>✏️</Text>
            <Text style={styles.actionButtonText}>Düzenle</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.actionButton, styles.actionButtonSecondary]}
            onPress={() => navigation.goBack()}
            activeOpacity={0.8}
          >
            <Text style={styles.actionButtonIcon}>←</Text>
            <Text style={[styles.actionButtonText, styles.actionButtonTextSecondary]}>Geri Dön</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.spacer} />
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FAFAFA',
  },
  centerContent: {
    justifyContent: 'center',
    alignItems: 'center',
  },
  scrollContent: {
    paddingBottom: 40,
  },

  /* Image Section */
  imageContainer: {
    position: 'relative',
    width: '100%',
    height: 280,
    backgroundColor: '#F0F0F0',
  },
  image: {
    width: '100%',
    height: '100%',
  },
  imagePlaceholder: {
    backgroundColor: '#F0F0F0',
    justifyContent: 'center',
    alignItems: 'center',
  },
  imagePlaceholderEmoji: {
    fontSize: 60,
  },
  backButton: {
    position: 'absolute',
    top: 10,
    left: 16,
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255, 255, 255, 0.9)',
    justifyContent: 'center',
    alignItems: 'center',
    elevation: 3,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
  },
  backButtonText: {
    fontSize: 24,
    color: '#1A1A1A',
    fontWeight: 'bold',
  },
  favoriteButton: {
    position: 'absolute',
    top: 10,
    right: 16,
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255, 255, 255, 0.9)',
    justifyContent: 'center',
    alignItems: 'center',
    elevation: 3,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
  },
  favoriteEmoji: {
    fontSize: 20,
  },

  /* Title Section */
  titleSection: {
    paddingHorizontal: 16,
    paddingVertical: 16,
    backgroundColor: '#FFFFFF',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
    marginBottom: 12,
    elevation: 2,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
  },
  title: {
    fontSize: 24,
    fontWeight: '800',
    color: '#1A1A1A',
    marginBottom: 12,
    lineHeight: 32,
  },
  categoriesRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 12,
  },
  categoryBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: '#F0F0F0',
    borderRadius: 12,
  },
  categoryBadgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#1A1A1A',
  },
  metaRow: {
    flexDirection: 'row',
    gap: 16,
    marginBottom: 12,
  },
  metaItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  metaIcon: {
    fontSize: 16,
  },
  metaText: {
    fontSize: 13,
    color: '#666',
    fontWeight: '500',
  },
  descriptionContainer: {
    marginTop: 8,
  },
  description: {
    fontSize: 14,
    color: '#555',
    lineHeight: 20,
  },
  readMoreText: {
    fontSize: 13,
    color: '#FF6B35',
    fontWeight: '600',
    marginTop: 8,
  },

  /* Section */
  section: {
    marginHorizontal: 16,
    marginBottom: 20,
    paddingHorizontal: 16,
    paddingVertical: 16,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    elevation: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1A1A1A',
  },

  /* Ingredient Matching */
  inlineAddButton: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 10,
    backgroundColor: '#1A1A1A',
  },
  inlineAddButtonDisabled: {
    backgroundColor: '#BDBDBD',
  },
  inlineAddButtonText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#FFFFFF',
  },
  progressBar: {
    width: '100%',
    height: 6,
    backgroundColor: '#E8E8E8',
    borderRadius: 3,
    overflow: 'hidden',
    marginBottom: 8,
  },
  progressFill: {
    height: '100%',
    borderRadius: 3,
  },
  matchText: {
    fontSize: 12,
    color: '#666',
    fontWeight: '500',
    marginBottom: 16,
  },
  feedbackChip: {
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginBottom: 14,
  },
  feedbackChipSuccess: {
    backgroundColor: '#E8F5E9',
  },
  feedbackChipInfo: {
    backgroundColor: '#E3F2FD',
  },
  feedbackChipError: {
    backgroundColor: '#FDECEA',
  },
  feedbackChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#1A1A1A',
  },

  /* Ingredients List */
  ingredientsList: {
    gap: 12,
  },
  ingredientItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: 12,
    backgroundColor: '#F9F9F9',
    borderRadius: 12,
    borderLeftWidth: 4,
    borderLeftColor: '#F0F0F0',
  },
  ingredientItemSelected: {
    borderLeftColor: '#FF6B35',
    backgroundColor: '#FFF5F0',
  },
  ingredientIndicator: {
    width: 24,
    height: 24,
    borderRadius: 12,
    borderWidth: 2,
    borderColor: '#DDD',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  ingredientIndicatorMatched: {
    borderColor: '#4CAF50',
    backgroundColor: '#E8F5E9',
  },
  ingredientIndicatorText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#555',
  },
  ingredientContent: {
    flex: 1,
  },
  ingredientName: {
    fontSize: 14,
    fontWeight: '500',
    color: '#1A1A1A',
  },
  ingredientNameMatched: {
    color: '#4CAF50',
    fontWeight: '600',
  },
  ingredientQuantity: {
    fontSize: 13,
    color: '#999',
    fontWeight: '500',
  },
  selectionMark: {
    fontSize: 16,
    color: '#777',
    marginLeft: 8,
  },
  selectionMarkSelected: {
    color: '#FF6B35',
  },

  /* Steps */
  stepsList: {
    gap: 12,
  },
  stepItem: {
    flexDirection: 'row',
    gap: 12,
    paddingVertical: 12,
  },
  stepNumber: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#FF6B35',
    justifyContent: 'center',
    alignItems: 'center',
    paddingTop: 1,
  },
  stepNumberText: {
    fontSize: 14,
    fontWeight: '800',
    color: '#FFFFFF',
  },
  stepText: {
    flex: 1,
    fontSize: 14,
    color: '#1A1A1A',
    lineHeight: 20,
    paddingTop: 8,
  },

  /* Nutrition */
  nutritionGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  nutritionItem: {
    flex: 1,
    minWidth: 100,
    paddingVertical: 16,
    paddingHorizontal: 12,
    backgroundColor: '#F9F9F9',
    borderRadius: 12,
    alignItems: 'center',
  },
  nutritionValue: {
    fontSize: 18,
    fontWeight: '700',
    color: '#FF6B35',
    marginBottom: 4,
  },
  nutritionLabel: {
    fontSize: 12,
    color: '#666',
    fontWeight: '500',
  },

  /* Actions */
  actionsContainer: {
    flexDirection: 'row',
    gap: 12,
    marginHorizontal: 16,
    marginTop: 20,
  },
  actionButton: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 14,
    paddingHorizontal: 16,
    backgroundColor: '#FF6B35',
    borderRadius: 12,
    gap: 8,
    elevation: 2,
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
  },
  actionButtonSecondary: {
    backgroundColor: '#F0F0F0',
  },
  actionButtonIcon: {
    fontSize: 16,
  },
  actionButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#FFFFFF',
  },
  actionButtonTextSecondary: {
    color: '#1A1A1A',
  },

  /* Error */
  errorEmoji: {
    fontSize: 64,
    marginBottom: 16,
  },
  errorTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1A1A1A',
    marginBottom: 8,
  },
  errorText: {
    fontSize: 14,
    color: '#666',
    marginBottom: 24,
    textAlign: 'center',
  },
  retryButton: {
    paddingHorizontal: 24,
    paddingVertical: 12,
    backgroundColor: '#FF6B35',
    borderRadius: 12,
  },
  retryButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#FFFFFF',
  },

  /* Spacing */
  spacer: {
    height: 20,
  },
});
