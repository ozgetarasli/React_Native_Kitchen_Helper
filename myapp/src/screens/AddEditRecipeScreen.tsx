import React, { useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Image,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import * as ImagePicker from 'expo-image-picker';
import { RootStackParamList } from '../types/navigation';
import api from '../services/api';
import { fetchRecipeDetail } from '../services/recipeService';

type Props = NativeStackScreenProps<RootStackParamList, 'AddEditRecipe'>;

type IngredientField = {
  name: string;
  amount: string;
};

const CATEGORY_OPTIONS = [
  'Kahvalti',
  'Ana Yemek',
  'Corba',
  'Tatli',
  'Vegan',
  'Fit',
  'Pratik',
  'Aksam Yemegi',
];

const createEmptyIngredient = (): IngredientField => ({ name: '', amount: '' });

const parseAmount = (value: string) => {
  const trimmedValue = value.trim();
  const match = trimmedValue.match(/^(\d*[.,]?\d+)\s*(.*)$/);

  if (!match) {
    return { quantity: null as number | null, unit: trimmedValue || null };
  }

  const numericValue = Number(match[1].replace(',', '.'));
  return {
    quantity: Number.isNaN(numericValue) ? null : numericValue,
    unit: match[2].trim() || null,
  };
};

const formatAmount = (quantity?: number, unit?: string) => {
  if (quantity == null && !unit) {
    return '';
  }

  if (quantity == null) {
    return unit ?? '';
  }

  return `${quantity} ${unit ?? ''}`.trim();
};

export default function AddEditRecipeScreen({ route, navigation }: Props) {
  const { recipeId } = route.params || {};
  const isEditMode = Boolean(recipeId);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [prepTime, setPrepTime] = useState('');
  const [servings, setServings] = useState('');
  const [sourceUrl, setSourceUrl] = useState('');
  const [imageUrl, setImageUrl] = useState('');
  const [categories, setCategories] = useState<string[]>([]);
  const [ingredients, setIngredients] = useState<IngredientField[]>([createEmptyIngredient()]);
  const [instructions, setInstructions] = useState<string[]>(['']);
  const [isFavorite, setIsFavorite] = useState(false);
  const [loading, setLoading] = useState(isEditMode);
  const [saving, setSaving] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imagePreview, setImagePreview] = useState<string | null>(null);

  const screenTitle = useMemo(
    () => (isEditMode ? 'Tarifi Duzenle' : 'Yeni Tarif Olustur'),
    [isEditMode],
  );

  useEffect(() => {
    navigation.setOptions({ title: isEditMode ? 'Tarif Duzenle' : 'Tarif Ekle' });
  }, [isEditMode, navigation]);

  useEffect(() => {
    if (!isEditMode || !recipeId) {
      return;
    }

    const loadRecipe = async () => {
      try {
        setLoading(true);
        const recipe = await fetchRecipeDetail(recipeId.toString());

        setTitle(recipe.title ?? '');
        setDescription(recipe.description ?? '');
        setPrepTime(recipe.prepTime ?? '');
        setServings(recipe.servings != null ? String(recipe.servings) : '');
        setSourceUrl(recipe.sourceUrl ?? '');
        setImageUrl(recipe.image ?? '');
        setCategories(Array.isArray(recipe.categories) ? recipe.categories : []);
        setIsFavorite(Boolean(recipe.isFavorite));

        const mappedIngredients = Array.isArray(recipe.ingredients)
          ? recipe.ingredients.map((ingredient) => ({
              name: ingredient.name ?? '',
              amount: formatAmount(ingredient.quantity, ingredient.unit) || ingredient.amount || '',
            }))
          : [];

        setIngredients(mappedIngredients.length > 0 ? mappedIngredients : [createEmptyIngredient()]);
        setInstructions(
          recipe.instructions && recipe.instructions.length > 0 ? recipe.instructions : [''],
        );
      } catch (error) {
        console.error('Error loading recipe form:', error);
        Alert.alert('Hata', 'Tarif bilgileri yuklenemedi.');
      } finally {
        setLoading(false);
      }
    };

    loadRecipe();
  }, [isEditMode, recipeId]);

  const updateIngredient = (index: number, field: keyof IngredientField, value: string) => {
    setIngredients((current) =>
      current.map((ingredient, ingredientIndex) =>
        ingredientIndex === index ? { ...ingredient, [field]: value } : ingredient,
      ),
    );
  };

  const addIngredient = () => {
    setIngredients((current) => [...current, createEmptyIngredient()]);
  };

  const removeIngredient = (index: number) => {
    setIngredients((current) => {
      const next = current.filter((_, ingredientIndex) => ingredientIndex !== index);
      return next.length > 0 ? next : [createEmptyIngredient()];
    });
  };

  const updateInstruction = (index: number, value: string) => {
    setInstructions((current) =>
      current.map((instruction, instructionIndex) =>
        instructionIndex === index ? value : instruction,
      ),
    );
  };

  const addInstruction = () => {
    setInstructions((current) => [...current, '']);
  };

  const removeInstruction = (index: number) => {
    setInstructions((current) => {
      const next = current.filter((_, instructionIndex) => instructionIndex !== index);
      return next.length > 0 ? next : [''];
    });
  };

  const toggleCategory = (category: string) => {
    setCategories((current) =>
      current.includes(category)
        ? current.filter((item) => item !== category)
        : [...current, category],
    );
  };

  const handlePickImage = async () => {
    try {
      const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!permission.granted) {
        Alert.alert('İzin gerekli', 'Resim seçmek için galeri izni gerekli.');
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaType.Images,
        allowsEditing: true,
        aspect: [16, 9],
        quality: 0.8,
      });

      if (result.canceled) {
        return;
      }

      const selectedAsset = result.assets[0];
      if (!selectedAsset.uri) {
        Alert.alert('Hata', 'Resim seçilemedi.');
        return;
      }

      setImagePreview(selectedAsset.uri);
      setUploadingImage(true);

      const formData = new FormData();
      const uriParts = selectedAsset.uri.split('/');
      const fileName = uriParts[uriParts.length - 1];
      const mimeType = selectedAsset.mimeType || 'image/jpeg';

      formData.append('file', {
        uri: selectedAsset.uri,
        name: fileName,
        type: mimeType,
      } as any);

      const uploadResponse = await api.post('/RecipesApi/upload-image', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });

      const uploadedImageUrl = uploadResponse.data?.imageUrl;
      if (uploadedImageUrl) {
        setImageUrl(uploadedImageUrl);
        Alert.alert('Başarılı', 'Resim yüklendi.');
      } else {
        throw new Error('Upload yanıtı geçersiz');
      }
    } catch (error: any) {
      console.error('Error uploading image:', error);
      setImagePreview(null);
      Alert.alert(
        'Yükleme hatası',
        error?.response?.data || 'Resim yüklenemedi. Lütfen tekrar deneyin.',
      );
    } finally {
      setUploadingImage(false);
    }
  };

  const handleRemoveImage = () => {
    setImageUrl('');
    setImagePreview(null);
  };

  const handleSave = async () => {
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      Alert.alert('Eksik bilgi', 'Tarif basligi bos olamaz.');
      return;
    }

    const recipeIngredients = ingredients
      .filter((ingredient) => ingredient.name.trim().length > 0)
      .map((ingredient) => {
        const parsedAmount = parseAmount(ingredient.amount);
        return {
          ingredient: { name: ingredient.name.trim() },
          quantity: parsedAmount.quantity,
          unit: parsedAmount.unit,
        };
      });

    const cleanedInstructions = instructions
      .map((instruction) => instruction.trim())
      .filter((instruction) => instruction.length > 0);

    const payload = {
      title: trimmedTitle,
      description: description.trim() || null,
      prepTime: prepTime.trim() || null,
      servings: servings.trim() ? Number(servings) : null,
      sourceUrl: sourceUrl.trim() || null,
      image: imageUrl.trim() || null,
      categories,
      isFavorite,
      instructions: cleanedInstructions,
      recipeIngredients,
    };

    try {
      setSaving(true);

      if (isEditMode && recipeId) {
        await api.put(`/RecipesApi/${recipeId}`, payload);
      } else {
        await api.post('/RecipesApi', payload);
      }

      Alert.alert('Basarili', isEditMode ? 'Tarif guncellendi.' : 'Tarif kaydedildi.', [
        {
          text: 'Tamam',
          onPress: () => navigation.goBack(),
        },
      ]);
    } catch (error: any) {
      console.error('Error saving recipe:', error);
      const message =
        error?.response?.data && typeof error.response.data === 'string'
          ? error.response.data
          : isEditMode
            ? 'Tarif guncellenemedi.'
            : 'Tarif kaydedilemedi.';
      Alert.alert('Hata', message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <SafeAreaView style={styles.loadingScreen}>
        <ActivityIndicator size="large" color="#FF6B35" />
        <Text style={styles.loadingText}>Tarif formu yukleniyor...</Text>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safeArea} edges={['bottom']}>
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.heroCard}>
            <Text style={styles.eyebrow}>Kisisel Tarif Defteri</Text>
            <Text style={styles.title}>{screenTitle}</Text>
            <Text style={styles.subtitle}>
              Baslik, malzemeler ve adimlari ekleyin. Tarifiniz hesabiniza ozel olarak
              kaydedilir.
            </Text>
          </View>

          <View style={styles.sectionCard}>
            <Text style={styles.sectionTitle}>Tarif Resmi</Text>
            {imagePreview || imageUrl ? (
              <View style={styles.imagePreviewContainer}>
                <Image
                  source={{ uri: imagePreview || imageUrl }}
                  style={styles.imagePreview}
                  resizeMode="cover"
                />
                <TouchableOpacity
                  style={styles.removeImageButton}
                  onPress={handleRemoveImage}
                  activeOpacity={0.85}
                >
                  <Text style={styles.removeImageButtonText}>✕</Text>
                </TouchableOpacity>
              </View>
            ) : (
              <View style={styles.imagePlaceholder}>
                <Text style={styles.imagePlaceholderText}>📷</Text>
                <Text style={styles.imagePlaceholderLabel}>Resim seçilmedi</Text>
              </View>
            )}
            <TouchableOpacity
              style={[styles.imageButton, uploadingImage && styles.buttonDisabled]}
              onPress={handlePickImage}
              activeOpacity={0.85}
              disabled={uploadingImage}
            >
              {uploadingImage ? (
                <ActivityIndicator color="#CC4A1A" />
              ) : (
                <Text style={styles.imageButtonText}>📱 Galeriden Seç</Text>
              )}
            </TouchableOpacity>
          </View>

          <View style={styles.sectionCard}>
            <Text style={styles.sectionTitle}>Temel Bilgiler</Text>
            <TextInput
              placeholder="Orn. Firinda Sebzeli Makarna"
              placeholderTextColor="#6b7280"
              style={styles.primaryInput}
              value={title}
              onChangeText={setTitle}
            />
            <TextInput
              placeholder="Tarifi kisaca anlatin"
              placeholderTextColor="#6b7280"
              style={[styles.input, styles.textArea]}
              value={description}
              onChangeText={setDescription}
              multiline
              textAlignVertical="top"
            />
            <View style={styles.row}>
              <TextInput
                placeholder="Hazirlama suresi"
                placeholderTextColor="#6b7280"
                style={[styles.input, styles.halfInput]}
                value={prepTime}
                onChangeText={setPrepTime}
              />
              <TextInput
                placeholder="Porsiyon"
                placeholderTextColor="#6b7280"
                style={[styles.input, styles.halfInput]}
                value={servings}
                onChangeText={setServings}
                keyboardType="number-pad"
              />
            </View>

            <TextInput
              placeholder="Kaynak linki (opsiyonel)"
              placeholderTextColor="#6b7280"
              style={styles.input}
              value={sourceUrl}
              onChangeText={setSourceUrl}
              autoCapitalize="none"
            />
            <View style={styles.favoriteRow}>
              <View>
                <Text style={styles.favoriteTitle}>Favorilere ekle</Text>
                <Text style={styles.favoriteHint}>Kaydederken tarifi favori olarak isle</Text>
              </View>
              <Switch
                value={isFavorite}
                onValueChange={setIsFavorite}
                trackColor={{ false: '#E0E0E0', true: '#FF6B35' }}
                thumbColor="#ffffff"
              />
            </View>
          </View>

          <View style={styles.sectionCard}>
            <Text style={styles.sectionTitle}>Kategori Secimi</Text>
            <View style={styles.tagWrap}>
              {CATEGORY_OPTIONS.map((category) => {
                const selected = categories.includes(category);
                return (
                  <TouchableOpacity
                    key={category}
                    activeOpacity={0.85}
                    style={[styles.tag, selected && styles.tagSelected]}
                    onPress={() => toggleCategory(category)}
                  >
                    <Text style={[styles.tagText, selected && styles.tagTextSelected]}>
                      {category}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>

          <View style={styles.sectionCard}>
            <View style={styles.sectionHeaderRow}>
              <View>
                <Text style={styles.sectionTitle}>Malzemeler</Text>
                <Text style={styles.sectionHint}>Miktar ve isim birlikte daha iyi sonuc verir.</Text>
              </View>
              <TouchableOpacity style={styles.inlineButton} onPress={addIngredient} activeOpacity={0.85}>
                <Text style={styles.inlineButtonText}>+ Malzeme</Text>
              </TouchableOpacity>
            </View>

            {ingredients.map((ingredient, index) => (
              <View key={`ingredient-${index}`} style={styles.listCard}>
                <View style={styles.listHeader}>
                  <Text style={styles.listIndex}>#{index + 1}</Text>
                  {ingredients.length > 1 ? (
                    <TouchableOpacity onPress={() => removeIngredient(index)}>
                      <Text style={styles.removeText}>Sil</Text>
                    </TouchableOpacity>
                  ) : null}
                </View>
                <TextInput
                  placeholder="Orn. 2 yemek kasigi"
                  placeholderTextColor="#6b7280"
                  style={styles.input}
                  value={ingredient.amount}
                  onChangeText={(value) => updateIngredient(index, 'amount', value)}
                />
                <TextInput
                  placeholder="Malzeme adi"
                  placeholderTextColor="#6b7280"
                  style={styles.input}
                  value={ingredient.name}
                  onChangeText={(value) => updateIngredient(index, 'name', value)}
                />
              </View>
            ))}
          </View>

          <View style={styles.sectionCard}>
            <View style={styles.sectionHeaderRow}>
              <View>
                <Text style={styles.sectionTitle}>Hazirlama Adimlari</Text>
                <Text style={styles.sectionHint}>Her adimi ayri kutuda yazin.</Text>
              </View>
              <TouchableOpacity style={styles.inlineButton} onPress={addInstruction} activeOpacity={0.85}>
                <Text style={styles.inlineButtonText}>+ Adim</Text>
              </TouchableOpacity>
            </View>

            {instructions.map((instruction, index) => (
              <View key={`instruction-${index}`} style={styles.stepRow}>
                <View style={styles.stepBadge}>
                  <Text style={styles.stepBadgeText}>{index + 1}</Text>
                </View>
                <TextInput
                  placeholder="Bu adimda ne yapiliyor?"
                  placeholderTextColor="#6b7280"
                  style={[styles.input, styles.stepInput]}
                  value={instruction}
                  onChangeText={(value) => updateInstruction(index, value)}
                  multiline
                  textAlignVertical="top"
                />
                {instructions.length > 1 ? (
                  <TouchableOpacity onPress={() => removeInstruction(index)} style={styles.stepRemoveButton}>
                    <Text style={styles.removeText}>Sil</Text>
                  </TouchableOpacity>
                ) : null}
              </View>
            ))}
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity
              style={[styles.secondaryButton, saving && styles.buttonDisabled]}
              onPress={() => navigation.goBack()}
              activeOpacity={0.85}
              disabled={saving}
            >
              <Text style={styles.secondaryButtonText}>Vazgec</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.primaryButton, saving && styles.buttonDisabled]}
              onPress={handleSave}
              activeOpacity={0.9}
              disabled={saving}
            >
              {saving ? (
                <ActivityIndicator color="#f8fafc" />
              ) : (
                <Text style={styles.primaryButtonText}>
                  {isEditMode ? '💾 Değişiklikleri Kaydet' : '✅ Tarifi Kaydet'}
                </Text>
              )}
            </TouchableOpacity>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  flex: {
    flex: 1,
  },
  safeArea: {
    flex: 1,
    backgroundColor: '#FFFDF9',
  },
  loadingScreen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 14,
    backgroundColor: '#FFFDF9',
  },
  loadingText: {
    color: '#1A1A1A',
    fontSize: 15,
    fontWeight: '600',
  },
  scrollContent: {
    padding: 20,
    paddingBottom: 36,
    gap: 18,
  },
  heroCard: {
    backgroundColor: '#FF6B35',
    borderRadius: 20,
    padding: 22,
    shadowColor: '#FF6B35',
    shadowOpacity: 0.25,
    shadowRadius: 14,
    shadowOffset: { width: 0, height: 6 },
    elevation: 5,
  },
  eyebrow: {
    color: '#FFE0D0',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1.2,
    textTransform: 'uppercase',
    marginBottom: 10,
  },
  title: {
    fontSize: 28,
    lineHeight: 34,
    color: '#FFFFFF',
    fontWeight: '800',
    marginBottom: 10,
  },
  subtitle: {
    color: '#FFE0D0',
    fontSize: 14,
    lineHeight: 21,
  },
  sectionCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 18,
    gap: 14,
    borderWidth: 1,
    borderColor: '#ECEFF4',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 1,
  },
  sectionTitle: {
    color: '#1A1A1A',
    fontSize: 18,
    fontWeight: '700',
  },
  sectionHint: {
    color: '#888',
    fontSize: 13,
    marginTop: 4,
  },
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: 12,
  },
  row: {
    flexDirection: 'row',
    gap: 12,
  },
  halfInput: {
    flex: 1,
  },
  primaryInput: {
    minHeight: 54,
    borderRadius: 14,
    borderWidth: 1.5,
    borderColor: '#FFB399',
    backgroundColor: '#FFF9F7',
    paddingHorizontal: 16,
    color: '#1A1A1A',
    fontSize: 18,
    fontWeight: '700',
  },
  input: {
    minHeight: 52,
    width: '100%',
    borderColor: '#E7EAF0',
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    color: '#1A1A1A',
    backgroundColor: '#F7F8FA',
    fontSize: 15,
  },
  textArea: {
    minHeight: 112,
  },
  favoriteRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#FFF3E0',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    borderWidth: 1,
    borderColor: '#FFD4B2',
  },
  favoriteTitle: {
    color: '#1A1A1A',
    fontSize: 15,
    fontWeight: '700',
  },
  favoriteHint: {
    color: '#888',
    fontSize: 12,
    marginTop: 2,
  },
  tagWrap: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  tag: {
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: '#E0E0E0',
    backgroundColor: '#F0F0F0',
  },
  tagSelected: {
    backgroundColor: '#FF6B35',
    borderColor: '#FF6B35',
  },
  tagText: {
    color: '#555',
    fontSize: 13,
    fontWeight: '700',
  },
  tagTextSelected: {
    color: '#FFFFFF',
  },
  inlineButton: {
    backgroundColor: '#FFF3E0',
    borderRadius: 999,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  inlineButtonText: {
    color: '#CC4A1A',
    fontSize: 13,
    fontWeight: '800',
  },
  listCard: {
    backgroundColor: '#F7F8FA',
    borderRadius: 12,
    padding: 14,
    gap: 10,
    borderWidth: 1,
    borderColor: '#E7EAF0',
  },
  listHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  listIndex: {
    color: '#FF6B35',
    fontSize: 13,
    fontWeight: '800',
  },
  removeText: {
    color: '#dc2626',
    fontSize: 13,
    fontWeight: '700',
  },
  stepRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 10,
  },
  stepBadge: {
    width: 34,
    height: 34,
    borderRadius: 17,
    backgroundColor: '#FF6B35',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
  },
  stepBadgeText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '800',
  },
  stepInput: {
    flex: 1,
    minHeight: 92,
  },
  stepRemoveButton: {
    paddingTop: 12,
  },
  actionRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: 4,
  },
  primaryButton: {
    flex: 1.3,
    minHeight: 56,
    borderRadius: 14,
    backgroundColor: '#FF6B35',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 4,
  },
  secondaryButton: {
    flex: 1,
    minHeight: 56,
    borderRadius: 14,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#E0E0E0',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
  },
  primaryButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '800',
  },
  secondaryButtonText: {
    color: '#555',
    fontSize: 15,
    fontWeight: '700',
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  imagePreviewContainer: {
    position: 'relative',
    width: '100%',
    height: 200,
    borderRadius: 18,
    overflow: 'hidden',
    marginBottom: 12,
  },
  imagePreview: {
    width: '100%',
    height: '100%',
  },
  removeImageButton: {
    position: 'absolute',
    top: 8,
    right: 8,
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#dc2626',
    alignItems: 'center',
    justifyContent: 'center',
  },
  removeImageButtonText: {
    color: '#FFFFFF',
    fontSize: 18,
    fontWeight: 'bold',
  },
  imagePlaceholder: {
    width: '100%',
    height: 140,
    borderRadius: 12,
    borderWidth: 2,
    borderColor: '#FFD4B2',
    borderStyle: 'dashed',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#FFF9F7',
    marginBottom: 12,
  },
  imagePlaceholderText: {
    fontSize: 48,
    marginBottom: 8,
  },
  imagePlaceholderLabel: {
    color: '#64748b',
    fontSize: 14,
    fontWeight: '600',
  },
  imageButton: {
    width: '100%',
    minHeight: 48,
    borderRadius: 12,
    backgroundColor: '#FFF3E0',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#FFD4B2',
  },
  imageButtonText: {
    color: '#CC4A1A',
    fontSize: 15,
    fontWeight: '800',
  },
});
