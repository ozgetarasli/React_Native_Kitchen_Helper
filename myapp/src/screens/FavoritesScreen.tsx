import React, { useCallback, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  Image,
  Animated,
  Alert,
  useWindowDimensions,
} from 'react-native';
import { CompositeScreenProps, useFocusEffect } from '@react-navigation/native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';
import api, { MEDIA_URL } from '../services/api';
import { canUseProtectedFeatures } from '../services/authSession';

type Props = CompositeScreenProps<
  BottomTabScreenProps<MainTabParamList, 'Favorites'>,
  NativeStackScreenProps<RootStackParamList>
>;

type RecipeItem = {
  id: number;
  title: string;
  image?: string;
  categories?: string[];
  servings?: number;
  prepTime?: string;
  isFavorite: boolean;
};

function FavoriteHeartButton({
  isFavorite,
  onPress,
}: {
  isFavorite: boolean;
  onPress: () => void;
}) {
  const scaleAnim = useRef(new Animated.Value(1)).current;

  const animateHeart = () => {
    Animated.sequence([
      Animated.spring(scaleAnim, {
        toValue: 1.28,
        useNativeDriver: true,
        speed: 22,
        bounciness: 10,
      }),
      Animated.spring(scaleAnim, {
        toValue: 1,
        useNativeDriver: true,
        speed: 20,
        bounciness: 8,
      }),
    ]).start();
  };

  const handlePress = () => {
    animateHeart();
    onPress();
  };

  return (
    <TouchableOpacity style={styles.favoriteBtn} onPress={handlePress} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
      <Animated.Text style={[styles.favoriteEmoji, { transform: [{ scale: scaleAnim }] }]}>
        {isFavorite ? '❤️' : '🤍'}
      </Animated.Text>
    </TouchableOpacity>
  );
}

export default function FavoritesScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const isCompact = width < 380;
  const isTablet = width >= 768;
  const numColumns = width >= 1080 ? 3 : width >= 640 ? 2 : 1;
  const basePadding = isCompact ? 12 : isTablet ? 24 : 16;
  const contentMaxWidth = 1160;
  const horizontalPadding = width > contentMaxWidth + basePadding * 2
    ? Math.floor((width - contentMaxWidth) / 2)
    : basePadding;
  const contentWidth = width - horizontalPadding * 2;
  const cardGap = 14;
  const rawCardWidth = (contentWidth - cardGap * (numColumns - 1)) / numColumns;
  const cardWidth = Math.min(rawCardWidth, 420);
  const imageHeight = Math.max(150, Math.min(230, Math.round(cardWidth * 0.52)));

  const [recipes, setRecipes] = useState<RecipeItem[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchFavorites = async () => {
    try {
      setLoading(true);
      const response = await api.get('/RecipesApi/favorites');
      setRecipes(Array.isArray(response.data) ? response.data : []);
    } catch (error) {
      console.error('Error fetching favorite recipes:', error);
      if ((error as any)?.response?.status === 401) {
        Alert.alert('Giris gerekli', 'Favori tarifleri gormek icin giris yapmaniz gerekiyor.');
      }
      setRecipes([]);
    } finally {
      setLoading(false);
    }
  };

  useFocusEffect(
    useCallback(() => {
      fetchFavorites();
    }, [])
  );

  const handleToggleFavorite = async (id: number) => {
    const authCheck = await canUseProtectedFeatures();
    if (!authCheck.allowed) {
      Alert.alert('Giris gerekli', authCheck.message);
      return;
    }

    const previous = recipes;
    setRecipes((prev) => prev.filter((item) => item.id !== id));

    try {
      await api.post(`/RecipesApi/${id}/toggle-favorite`);
    } catch (error) {
      console.error('Error toggling favorite from favorites screen:', error);
      setRecipes(previous);
    }
  };

  const getImageUrl = (imagePath?: string): string | null => {
    if (!imagePath || typeof imagePath !== 'string' || imagePath.trim() === '') return null;
    const path = imagePath.trim();
    if (path.includes('data:image') || path.includes(';base64')) return null;
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    return path.startsWith('/') ? `${MEDIA_URL}${path}` : `${MEDIA_URL}/${path}`;
  };

  const renderFavoriteRecipe = ({ item }: { item: RecipeItem }) => {
    const imageUrl = getImageUrl(item.image);

    return (
      <TouchableOpacity
        style={[styles.recipeCard, { width: cardWidth }]}
        onPress={() => navigation.navigate('RecipeDetail', { recipeId: item.id.toString() })}
        activeOpacity={0.85}
      >
        <View style={styles.imageWrap}>
          {imageUrl ? (
            <Image source={{ uri: imageUrl }} style={[styles.recipeImage, { height: imageHeight }]} resizeMode="cover" />
          ) : (
            <View style={[styles.recipeImage, styles.recipeImagePlaceholder, { height: imageHeight }]}>
              <Text style={styles.placeholderEmoji}>🍽️</Text>
            </View>
          )}

          <FavoriteHeartButton isFavorite={item.isFavorite} onPress={() => handleToggleFavorite(item.id)} />
        </View>

        <View style={styles.cardContent}>
          <Text style={styles.recipeTitle} numberOfLines={2}>{item.title}</Text>

          <View style={styles.metaRow}>
            <Text style={styles.metaText}>👥 {item.servings || '-'} kişi</Text>
            <Text style={styles.metaText}>⏱ {item.prepTime || '- dk'}</Text>
          </View>

          {item.categories && item.categories.length > 0 && (
            <View style={styles.categoryBadge}>
              <Text style={styles.categoryBadgeText}>{item.categories[0]}</Text>
            </View>
          )}
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={[styles.header, { paddingHorizontal: horizontalPadding, paddingTop: 12 }]}> 
        <Text style={styles.title}>Favori Tariflerim</Text>
        <Text style={styles.subtitle}>Beğendiğiniz tariflere buradan hızlıca ulaşın</Text>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#FF6B35" style={{ marginTop: 40 }} />
      ) : (
        <FlatList
          key={`favorites-grid-${numColumns}`}
          data={recipes}
          keyExtractor={(item) => item.id.toString()}
          renderItem={renderFavoriteRecipe}
          numColumns={numColumns}
          columnWrapperStyle={numColumns > 1 ? styles.row : undefined}
          showsVerticalScrollIndicator={false}
          contentContainerStyle={[styles.listContent, { paddingHorizontal: horizontalPadding }]}
          ListEmptyComponent={
            <View style={styles.emptyWrap}>
              <Text style={styles.emptyEmoji}>🤍</Text>
              <Text style={styles.emptyTitle}>Henüz favori tarifiniz yok</Text>
              <Text style={styles.emptyDesc}>Tarif detayındaki kalp butonuna basarak favorilere ekleyebilirsiniz.</Text>
            </View>
          }
        />
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFDF9',
  },
  header: {
    paddingTop: 12,
    paddingHorizontal: 16,
    paddingBottom: 12,
    backgroundColor: '#FFFFFF',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  title: {
    fontSize: 24,
    fontWeight: '800',
    color: '#1A1A1A',
  },
  subtitle: {
    marginTop: 4,
    marginBottom: 4,
    fontSize: 14,
    color: '#666',
  },
  listContent: {
    padding: 16,
    paddingBottom: 110,
    gap: 14,
  },
  row: {
    justifyContent: 'space-between',
    marginBottom: 14,
  },
  recipeCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 18,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 8,
    elevation: 2,
  },
  imageWrap: {
    position: 'relative',
  },
  recipeImage: {
    width: '100%',
    height: 180,
  },
  recipeImagePlaceholder: {
    backgroundColor: '#F1F1F1',
    alignItems: 'center',
    justifyContent: 'center',
  },
  placeholderEmoji: {
    fontSize: 48,
  },
  favoriteBtn: {
    position: 'absolute',
    right: 12,
    top: 12,
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255,255,255,0.92)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  favoriteEmoji: {
    fontSize: 20,
  },
  cardContent: {
    paddingHorizontal: 14,
    paddingVertical: 12,
    gap: 10,
  },
  recipeTitle: {
    fontSize: 17,
    color: '#1A1A1A',
    fontWeight: '700',
    lineHeight: 23,
  },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  metaText: {
    fontSize: 13,
    color: '#666',
    fontWeight: '600',
  },
  categoryBadge: {
    alignSelf: 'flex-start',
    backgroundColor: '#F2F2F2',
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 5,
  },
  categoryBadgeText: {
    fontSize: 12,
    color: '#444',
    fontWeight: '600',
  },
  emptyWrap: {
    marginTop: 90,
    alignItems: 'center',
    paddingHorizontal: 26,
  },
  emptyEmoji: {
    fontSize: 44,
  },
  emptyTitle: {
    marginTop: 14,
    fontSize: 19,
    color: '#1A1A1A',
    fontWeight: '700',
  },
  emptyDesc: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: '#666',
    textAlign: 'center',
  },
});
