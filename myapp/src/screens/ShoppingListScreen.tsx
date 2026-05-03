import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  Animated,
  Alert,
  StatusBar,
  Modal,
  KeyboardAvoidingView,
  Platform,
  useWindowDimensions,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';
import api from '../services/api';

type Props = CompositeScreenProps<
  BottomTabScreenProps<MainTabParamList, 'ShoppingList'>,
  NativeStackScreenProps<RootStackParamList>
>;

/* ── Interfaces ── */
interface PantryItem {
  id: string;
  name: string;
  quantity: string;
  unit: string;
  category: string;
}

interface ShoppingItem {
  id: string;
  name: string;
  purchased: boolean;
}

/* ── Constants ── */
const CATEGORIES = [
  { label: 'Tümü', value: 'all', emoji: '📦' },
  { label: 'Sebze', value: 'Sebze', emoji: '🥬' },
  { label: 'Meyve', value: 'Meyve', emoji: '🍎' },
  { label: 'Et', value: 'Et', emoji: '🥩' },
  { label: 'Süt Ürünleri', value: 'Süt Ürünleri', emoji: '🧀' },
  { label: 'Baklagil', value: 'Baklagil', emoji: '🫘' },
  { label: 'Baharat', value: 'Baharat', emoji: '🌶️' },
  { label: 'Diğer', value: 'Diğer', emoji: '🍽️' },
];

const UNIT_OPTIONS = ['adet', 'kg', 'g', 'lt', 'ml', 'su bardağı', 'çay bardağı', 'yemek kaşığı', 'tatlı kaşığı', 'demet', 'paket'];

const CATEGORY_COLORS: Record<string, string> = {
  'Sebze': '#E8F5E9',
  'Meyve': '#FFF3E0',
  'Et': '#FCE4EC',
  'Süt Ürünleri': '#E3F2FD',
  'Baklagil': '#FFF8E1',
  'Baharat': '#F3E5F5',
  'Diğer': '#F5F5F5',
};

const CATEGORY_EMOJIS: Record<string, string> = {
  'Sebze': '🥬',
  'Meyve': '🍎',
  'Et': '🥩',
  'Süt Ürünleri': '🧀',
  'Baklagil': '🫘',
  'Baharat': '🌶️',
  'Diğer': '🍽️',
};

const INITIAL_SHOPPING: ShoppingItem[] = [
  { id: '1', name: 'Zeytinyağı', purchased: false },
  { id: '2', name: 'Makarna', purchased: false },
  { id: '3', name: 'Tuz', purchased: true },
];

type TabKey = 'shopping' | 'pantry';

type ToastType = 'success' | 'warning' | 'info';
interface ToastState {
  visible: boolean;
  message: string;
  type: ToastType;
  emoji: string;
}

export default function ShoppingListScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const isCompact = width < 380;
  const horizontalPadding = isCompact ? 12 : 16;

  const [activeTab, setActiveTab] = useState<TabKey>('shopping');

  /* ── Toast State ── */
  const [toast, setToast] = useState<ToastState>({ visible: false, message: '', type: 'success', emoji: '' });
  const toastAnim = useRef(new Animated.Value(0)).current;
  const toastTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  const showToast = useCallback((message: string, type: ToastType = 'success', emoji: string = '✅') => {
    if (toastTimeout.current) clearTimeout(toastTimeout.current);
    setToast({ visible: true, message, type, emoji });
    toastAnim.setValue(0);
    Animated.spring(toastAnim, { toValue: 1, useNativeDriver: true, tension: 80, friction: 10 }).start();
    toastTimeout.current = setTimeout(() => {
      Animated.timing(toastAnim, { toValue: 0, duration: 250, useNativeDriver: true }).start(() => {
        setToast((t) => ({ ...t, visible: false }));
      });
    }, 2500);
  }, [toastAnim]);

  /* ── Shopping State ── */
  const [shoppingItems, setShoppingItems] = useState<ShoppingItem[]>([]);
  const [newShoppingName, setNewShoppingName] = useState('');

  const fetchShoppingList = async () => {
    try {
      const { data } = await api.get('/ShoppingListApi');
      const formatted = data.map((i: any) => ({
        id: i.id.toString(),
        name: i.name,
        purchased: i.purchased,
      }));
      setShoppingItems(formatted);
    } catch (e) {
      console.error('Error fetching shopping list:', e);
      // Fallback
      setShoppingItems(INITIAL_SHOPPING);
    }
  };

  /* ── Pantry State ── */
  const [pantryItems, setPantryItems] = useState<PantryItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState('all');
  const [searchText, setSearchText] = useState('');
  const [modalVisible, setModalVisible] = useState(false);
  const [editingItem, setEditingItem] = useState<PantryItem | null>(null);

  // Form state
  const [formName, setFormName] = useState('');
  const [formQuantity, setFormQuantity] = useState('');
  const [formUnit, setFormUnit] = useState('adet');
  const [formCategory, setFormCategory] = useState('Sebze');

  const fadeAnim = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(24)).current;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(fadeAnim, { toValue: 1, duration: 500, useNativeDriver: true }),
      Animated.timing(slideAnim, { toValue: 0, duration: 450, useNativeDriver: true }),
    ]).start();
    
    fetchShoppingList();
    fetchPantryItems();
  }, []);

  const fetchPantryItems = async () => {
    try {
      const { data } = await api.get('/PantryApi');
      setPantryItems(data);
    } catch (e) {
      console.error('Error fetching pantry items:', e);
      setPantryItems([]);
    }
  };

  /* ── Shopping Handlers ── */
  const handleAddShopping = async () => {
    if (!newShoppingName.trim()) return;
    const name = newShoppingName.trim();
    try {
      await api.post('/ShoppingListApi', { name });
      fetchShoppingList();
      setNewShoppingName('');
    } catch (e) {
      console.error('Add failed', e);
      showToast('Ürün eklenemedi', 'warning', '⚠️');
    }
  };

  const togglePurchased = async (id: string) => {
    const item = shoppingItems.find(i => i.id === id);
    if (!item) return;
    
    // optimistic update
    setShoppingItems((prev) =>
      prev.map((i) => (i.id === id ? { ...i, purchased: !i.purchased } : i))
    );

    try {
      await api.put(`/ShoppingListApi/${id}/toggle`, { isChecked: !item.purchased });
    } catch (e) {
      console.error('Toggle failed', e);
      // revert optimistic 
      setShoppingItems((prev) =>
        prev.map((i) => (i.id === id ? { ...i, purchased: item.purchased } : i))
      );
    }
  };

  const removeShopping = async (id: string) => {
    const previousItems = [...shoppingItems];
    // optimistic update
    setShoppingItems((prev) => prev.filter((i) => i.id !== id));

    try {
      await api.delete(`/ShoppingListApi/${id}`);
    } catch (e) {
      console.error('Delete failed', e);
      setShoppingItems(previousItems);
    }
  };

  const addToShoppingList = async (pantryItem: PantryItem) => {
    const alreadyExists = shoppingItems.some(
      (s) => s.name.toLowerCase() === pantryItem.name.toLowerCase() && !s.purchased
    );
    if (alreadyExists) {
      showToast(`"${pantryItem.name}" zaten alışveriş listenizde.`, 'info', 'ℹ️');
      return;
    }
    const name = `${pantryItem.name} (${pantryItem.quantity} ${pantryItem.unit})`;
    try {
      await api.post('/ShoppingListApi', { name });
      fetchShoppingList();
      showToast(`"${pantryItem.name}" alışveriş listesine eklendi.`, 'success', '🛒');
    } catch (e) {
      console.error(e);
      showToast('Ekleme başarısız', 'warning', '⚠️');
    }
  };

  /* ── Pantry Handlers ── */
  const filteredPantry = pantryItems.filter((item) => {
    const matchCat = selectedCategory === 'all' || item.category === selectedCategory;
    const matchSearch = item.name.toLowerCase().includes(searchText.toLowerCase());
    return matchCat && matchSearch;
  });

  const openAddModal = () => {
    setEditingItem(null);
    setFormName('');
    setFormQuantity('');
    setFormUnit('adet');
    setFormCategory('Sebze');
    setModalVisible(true);
  };

  const openEditModal = (item: PantryItem) => {
    setEditingItem(item);
    setFormName(item.name);
    setFormQuantity(item.quantity);
    setFormUnit(item.unit);
    setFormCategory(item.category);
    setModalVisible(true);
  };

  const handleSavePantry = async () => {
    if (!formName.trim() || !formQuantity.trim()) {
      showToast('Malzeme adı ve miktar boş olamaz.', 'warning', '⚠️');
      return;
    }

    const payload = {
      name: formName.trim(),
      quantity: formQuantity.trim(),
      unit: formUnit,
      category: formCategory,
    };

    try {
      if (editingItem) {
        await api.put(`/PantryApi/${editingItem.id}`, payload);
      } else {
        await api.post('/PantryApi', payload);
      }
      fetchPantryItems();
      setModalVisible(false);
      showToast(editingItem ? 'Malzeme güncellendi' : 'Malzeme eklendi', 'success', '🧊');
    } catch (e) {
      console.error('Error saving pantry item:', e);
      showToast('Kayıt başarısız', 'warning', '⚠️');
    }
  };

  const handleDeletePantry = (id: string) => {
    Alert.alert('Malzemeyi Sil', 'Bu malzemeyi silmek istediğinize emin misiniz?', [
      { text: 'İptal', style: 'cancel' },
      { 
        text: 'Sil', 
        style: 'destructive', 
        onPress: async () => {
          try {
            await api.delete(`/PantryApi/${id}`);
            setPantryItems((prev) => prev.filter((i) => i.id !== id));
            showToast('Malzeme silindi', 'success', '🗑️');
          } catch (e) {
            console.error('Error deleting pantry item:', e);
            showToast('Silme başarısız', 'warning', '⚠️');
          }
        } 
      },
    ]);
  };

  const categoryCount = (cat: string) => {
    if (cat === 'all') return pantryItems.length;
    return pantryItems.filter((i) => i.category === cat).length;
  };

  /* ── Computed ── */
  const purchasedCount = shoppingItems.filter((i) => i.purchased).length;
  const totalShopping = shoppingItems.length;

  /* ── Render ── */
  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="dark-content" backgroundColor="#FAFAFA" />

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        {/* Header */}
        <Animated.View
          style={[
            styles.headerSection,
            {
              opacity: fadeAnim,
              transform: [{ translateY: slideAnim }],
              marginHorizontal: horizontalPadding,
              marginTop: 12,
              padding: isCompact ? 16 : 20,
            },
          ]}
        >
          <View style={styles.headerRow}>
            <View style={styles.headerIconCircle}>
              <Text style={styles.headerEmoji}>🛒</Text>
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.headerTitle}>Alışveriş Listesi</Text>
              <Text style={styles.headerSubtitle}>
                {activeTab === 'shopping'
                  ? `${purchasedCount}/${totalShopping} tamamlandı`
                  : `${pantryItems.length} malzeme mevcut`}
              </Text>
            </View>
            {activeTab === 'pantry' && (
              <TouchableOpacity style={styles.addButton} onPress={openAddModal} activeOpacity={0.85}>
                <Text style={styles.addButtonText}>+ Ekle</Text>
              </TouchableOpacity>
            )}
          </View>

          {/* Tab Switcher */}
          <View style={styles.tabSwitcher}>
            <TouchableOpacity
              style={[styles.tabBtn, activeTab === 'shopping' && styles.tabBtnActive]}
              onPress={() => setActiveTab('shopping')}
              activeOpacity={0.8}
            >
              <Text style={styles.tabBtnEmoji}>🛒</Text>
              <Text style={[styles.tabBtnText, activeTab === 'shopping' && styles.tabBtnTextActive, isCompact && { fontSize: 12 }]}>
                Alışveriş
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.tabBtn, activeTab === 'pantry' && styles.tabBtnActive]}
              onPress={() => setActiveTab('pantry')}
              activeOpacity={0.8}
            >
              <Text style={styles.tabBtnEmoji}>🧊</Text>
              <Text style={[styles.tabBtnText, activeTab === 'pantry' && styles.tabBtnTextActive, isCompact && { fontSize: 12 }]}>
                Mutfağım
              </Text>
            </TouchableOpacity>
          </View>
        </Animated.View>

        {/* ════════ SHOPPING TAB ════════ */}
        {activeTab === 'shopping' && (
          <>
            {/* Add Shopping Item */}
            <View style={[styles.addShoppingCard, { marginHorizontal: horizontalPadding }]}>
              <View style={[styles.addShoppingRow, isCompact && { flexDirection: 'column' }]}>
                <TextInput
                  style={styles.shoppingInput}
                  placeholder="Yeni ürün ekle..."
                  placeholderTextColor="#999"
                  value={newShoppingName}
                  onChangeText={setNewShoppingName}
                  onSubmitEditing={handleAddShopping}
                  returnKeyType="done"
                />
                <TouchableOpacity
                  style={[styles.addShoppingBtn, isCompact && { width: '100%', paddingVertical: 12, alignItems: 'center' }]}
                  onPress={handleAddShopping}
                  activeOpacity={0.85}
                >
                  <Text style={styles.addShoppingBtnText}>+ Ekle</Text>
                </TouchableOpacity>
              </View>
            </View>

            {/* Progress */}
            {totalShopping > 0 && (
              <View style={[styles.progressCard, { marginHorizontal: horizontalPadding }]}>
                <View style={styles.progressHeader}>
                  <Text style={styles.progressLabel}>İlerleme</Text>
                  <Text style={styles.progressPercent}>
                    {Math.round((purchasedCount / totalShopping) * 100)}%
                  </Text>
                </View>
                <View style={styles.progressBarBg}>
                  <View
                    style={[styles.progressBarFill, { width: `${(purchasedCount / totalShopping) * 100}%` }]}
                  />
                </View>
              </View>
            )}

            {/* Shopping List */}
            {totalShopping === 0 ? (
              <View style={[styles.emptyState, { marginHorizontal: horizontalPadding }]}>
                <Text style={styles.emptyEmoji}>🛒</Text>
                <Text style={styles.emptyTitle}>Liste boş</Text>
                <Text style={styles.emptyDesc}>Alışveriş listenize ürün ekleyerek başlayın.</Text>
              </View>
            ) : (
              <View style={[styles.listContainer, { marginHorizontal: horizontalPadding }]}>
                {[...shoppingItems]
                  .sort((a, b) => (a.purchased === b.purchased ? 0 : a.purchased ? 1 : -1))
                  .map((item) => (
                    <TouchableOpacity
                      key={item.id}
                      style={[styles.shoppingCard, item.purchased && styles.shoppingCardDone]}
                      onPress={() => togglePurchased(item.id)}
                      activeOpacity={0.8}
                    >
                      <View style={styles.shoppingLeft}>
                        <View style={[styles.checkbox, item.purchased && styles.checkboxDone]}>
                          {item.purchased && <Text style={styles.checkmark}>✓</Text>}
                        </View>
                        <Text style={[styles.shoppingName, item.purchased && styles.shoppingNameDone]}>
                          {item.name}
                        </Text>
                      </View>
                      <View style={styles.shoppingRight}>
                        {item.purchased && (
                          <View style={styles.completedBadge}>
                            <Text style={styles.completedBadgeText}>Alındı</Text>
                          </View>
                        )}
                        <TouchableOpacity
                          style={styles.removeBtn}
                          onPress={() => removeShopping(item.id)}
                          activeOpacity={0.7}
                        >
                          <Text style={styles.removeBtnText}>🗑️</Text>
                        </TouchableOpacity>
                      </View>
                    </TouchableOpacity>
                  ))}
              </View>
            )}
          </>
        )}

        {/* ════════ PANTRY TAB ════════ */}
        {activeTab === 'pantry' && (
          <>
            {/* Search */}
            <View style={[styles.searchCard, { marginHorizontal: horizontalPadding }]}>
              <View style={styles.searchContainer}>
                <Text style={styles.searchIcon}>🔍</Text>
                <TextInput
                  style={styles.searchInput}
                  placeholder="Malzeme ara..."
                  placeholderTextColor="#999"
                  value={searchText}
                  onChangeText={setSearchText}
                />
                {searchText.length > 0 && (
                  <TouchableOpacity onPress={() => setSearchText('')}>
                    <Text style={styles.clearSearch}>✕</Text>
                  </TouchableOpacity>
                )}
              </View>
            </View>

            {/* Category Filter */}
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={[styles.categoryRow, { paddingHorizontal: horizontalPadding }]}
              style={styles.categoryScroll}
            >
              {CATEGORIES.map((cat) => {
                const isActive = selectedCategory === cat.value;
                const count = categoryCount(cat.value);
                return (
                  <TouchableOpacity
                    key={cat.value}
                    style={[styles.categoryChip, isActive && styles.categoryChipActive]}
                    onPress={() => setSelectedCategory(cat.value)}
                    activeOpacity={0.8}
                  >
                    <Text style={styles.categoryChipEmoji}>{cat.emoji}</Text>
                    <Text style={[styles.categoryChipText, isActive && styles.categoryChipTextActive]}>
                      {cat.label}
                    </Text>
                    <View style={[styles.categoryBadge, isActive && styles.categoryBadgeActive]}>
                      <Text style={[styles.categoryBadgeText, isActive && styles.categoryBadgeTextActive]}>
                        {count}
                      </Text>
                    </View>
                  </TouchableOpacity>
                );
              })}
            </ScrollView>

            {/* Pantry Item List */}
            {filteredPantry.length === 0 ? (
              <View style={[styles.emptyState, { marginHorizontal: horizontalPadding }]}>
                <Text style={styles.emptyEmoji}>🍽️</Text>
                <Text style={styles.emptyTitle}>Malzeme bulunamadı</Text>
                <Text style={styles.emptyDesc}>
                  {searchText ? 'Arama kriterlerinize uygun malzeme yok.' : 'Mutfağınıza malzeme ekleyerek başlayın.'}
                </Text>
                {!searchText && (
                  <TouchableOpacity style={styles.emptyAddBtn} onPress={openAddModal} activeOpacity={0.85}>
                    <Text style={styles.emptyAddBtnText}>+ Malzeme Ekle</Text>
                  </TouchableOpacity>
                )}
              </View>
            ) : (
              <View style={[styles.listContainer, { marginHorizontal: horizontalPadding }]}>
                {filteredPantry.map((item) => (
                  <View key={item.id} style={[styles.pantryCard, { backgroundColor: CATEGORY_COLORS[item.category] || '#F5F5F5' }]}>
                    <View style={styles.pantryLeft}>
                      <Text style={styles.pantryEmoji}>{CATEGORY_EMOJIS[item.category] || '🍽️'}</Text>
                      <View style={styles.pantryInfo}>
                        <Text style={styles.pantryName}>{item.name}</Text>
                        <Text style={styles.pantryDetail}>
                          {item.quantity} {item.unit} • {item.category}
                        </Text>
                      </View>
                    </View>
                    <View style={styles.pantryActions}>
                      <TouchableOpacity style={styles.cartBtn} onPress={() => addToShoppingList(item)} activeOpacity={0.7}>
                        <Text style={styles.actionBtnText}>🛒</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={styles.editBtn} onPress={() => openEditModal(item)} activeOpacity={0.7}>
                        <Text style={styles.actionBtnText}>✏️</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={styles.deleteBtn} onPress={() => handleDeletePantry(item.id)} activeOpacity={0.7}>
                        <Text style={styles.actionBtnText}>🗑️</Text>
                      </TouchableOpacity>
                    </View>
                  </View>
                ))}
              </View>
            )}
          </>
        )}

        <View style={{ height: 100 }} />
      </ScrollView>

      {/* Add/Edit Pantry Modal */}
      <Modal visible={modalVisible} animationType="slide" transparent>
        <KeyboardAvoidingView
          style={styles.modalOverlay}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          <View style={styles.modalContent}>
            <View style={styles.modalHandle} />
            <Text style={styles.modalTitle}>
              {editingItem ? 'Malzeme Düzenle' : 'Yeni Malzeme Ekle'}
            </Text>

            <Text style={styles.inputLabel}>Malzeme Adı</Text>
            <TextInput
              style={styles.modalInput}
              placeholder="örn. Domates"
              placeholderTextColor="#999"
              value={formName}
              onChangeText={setFormName}
            />

            <Text style={styles.inputLabel}>Miktar</Text>
            <View style={styles.quantityRow}>
              <TextInput
                style={[styles.modalInput, { flex: 1 }]}
                placeholder="örn. 500"
                placeholderTextColor="#999"
                keyboardType="numeric"
                value={formQuantity}
                onChangeText={setFormQuantity}
              />
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                contentContainerStyle={styles.unitChipsRow}
                style={{ flex: 2, marginLeft: 10 }}
              >
                {UNIT_OPTIONS.map((u) => (
                  <TouchableOpacity
                    key={u}
                    style={[styles.unitChip, formUnit === u && styles.unitChipActive]}
                    onPress={() => setFormUnit(u)}
                  >
                    <Text style={[styles.unitChipText, formUnit === u && styles.unitChipTextActive]}>{u}</Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            </View>

            <Text style={styles.inputLabel}>Kategori</Text>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.unitChipsRow}
            >
              {CATEGORIES.filter((c) => c.value !== 'all').map((cat) => (
                <TouchableOpacity
                  key={cat.value}
                  style={[styles.unitChip, formCategory === cat.value && styles.unitChipActive]}
                  onPress={() => setFormCategory(cat.value)}
                >
                  <Text style={[styles.unitChipText, formCategory === cat.value && styles.unitChipTextActive]}>
                    {cat.emoji} {cat.label}
                  </Text>
                </TouchableOpacity>
              ))}
            </ScrollView>

            <View style={styles.modalButtonRow}>
              <TouchableOpacity style={styles.cancelBtn} onPress={() => setModalVisible(false)} activeOpacity={0.8}>
                <Text style={styles.cancelBtnText}>İptal</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.saveBtn} onPress={handleSavePantry} activeOpacity={0.85}>
                <Text style={styles.saveBtnText}>{editingItem ? 'Güncelle' : 'Ekle'}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </KeyboardAvoidingView>
      </Modal>

      {/* Toast Notification */}
      {toast.visible && (
        <Animated.View
          style={[
            styles.toastContainer,
            toast.type === 'success' && styles.toastSuccess,
            toast.type === 'warning' && styles.toastWarning,
            toast.type === 'info' && styles.toastInfo,
            {
              opacity: toastAnim,
              transform: [
                { translateY: toastAnim.interpolate({ inputRange: [0, 1], outputRange: [-30, 0] }) },
                { scale: toastAnim.interpolate({ inputRange: [0, 1], outputRange: [0.9, 1] }) },
              ],
            },
          ]}
        >
          <Text style={styles.toastEmoji}>{toast.emoji}</Text>
          <Text style={styles.toastText}>{toast.message}</Text>
          <TouchableOpacity
            onPress={() => {
              Animated.timing(toastAnim, { toValue: 0, duration: 150, useNativeDriver: true }).start(() =>
                setToast((t) => ({ ...t, visible: false }))
              );
            }}
            style={styles.toastCloseBtn}
          >
            <Text style={styles.toastCloseText}>✕</Text>
          </TouchableOpacity>
        </Animated.View>
      )}
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

  /* ── Header ── */
  headerSection: {
    backgroundColor: '#FFFFFF',
    borderRadius: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.07,
    shadowRadius: 8,
    elevation: 3,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  headerIconCircle: {
    width: 52,
    height: 52,
    borderRadius: 26,
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
  headerEmoji: {
    fontSize: 26,
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: '800',
    color: '#1A1A1A',
    letterSpacing: -0.5,
  },
  headerSubtitle: {
    fontSize: 13,
    fontWeight: '600',
    color: '#FF6B35',
    marginTop: 2,
  },
  addButton: {
    backgroundColor: '#FF6B35',
    paddingHorizontal: 18,
    paddingVertical: 10,
    borderRadius: 14,
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.35,
    shadowRadius: 6,
    elevation: 4,
  },
  addButtonText: {
    color: '#FFF',
    fontWeight: '700',
    fontSize: 14,
  },

  /* ── Tab Switcher ── */
  tabSwitcher: {
    flexDirection: 'row',
    backgroundColor: '#F5F5F5',
    borderRadius: 14,
    padding: 4,
  },
  tabBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    borderRadius: 11,
    gap: 6,
  },
  tabBtnActive: {
    backgroundColor: '#FFFFFF',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  tabBtnEmoji: {
    fontSize: 16,
  },
  tabBtnText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#999',
  },
  tabBtnTextActive: {
    color: '#1A1A1A',
  },

  /* ── Shopping Tab ── */
  addShoppingCard: {
    marginHorizontal: 16,
    marginTop: 16,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 6,
    elevation: 2,
  },
  addShoppingRow: {
    flexDirection: 'row',
    gap: 10,
  },
  shoppingInput: {
    flex: 1,
    backgroundColor: '#F5F5F5',
    borderRadius: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
    fontSize: 15,
    color: '#1A1A1A',
  },
  addShoppingBtn: {
    backgroundColor: '#FF6B35',
    borderRadius: 12,
    paddingHorizontal: 18,
    justifyContent: 'center',
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 3,
  },
  addShoppingBtnText: {
    color: '#FFF',
    fontWeight: '700',
    fontSize: 14,
  },

  /* Progress */
  progressCard: {
    marginHorizontal: 16,
    marginTop: 12,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 6,
    elevation: 2,
  },
  progressHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  progressLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: '#666',
  },
  progressPercent: {
    fontSize: 13,
    fontWeight: '700',
    color: '#FF6B35',
  },
  progressBarBg: {
    height: 8,
    backgroundColor: '#F0F0F0',
    borderRadius: 4,
    overflow: 'hidden',
  },
  progressBarFill: {
    height: '100%',
    backgroundColor: '#FF6B35',
    borderRadius: 4,
  },

  /* Shopping Items */
  shoppingCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 4,
    elevation: 1,
  },
  shoppingCardDone: {
    backgroundColor: '#F9F9F9',
  },
  shoppingLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  checkbox: {
    width: 24,
    height: 24,
    borderRadius: 7,
    borderWidth: 2,
    borderColor: '#DDD',
    marginRight: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  checkboxDone: {
    backgroundColor: '#4CAF50',
    borderColor: '#4CAF50',
  },
  checkmark: {
    color: '#FFF',
    fontSize: 14,
    fontWeight: '800',
  },
  shoppingName: {
    fontSize: 15,
    fontWeight: '600',
    color: '#1A1A1A',
    flex: 1,
  },
  shoppingNameDone: {
    textDecorationLine: 'line-through',
    color: '#AAA',
  },
  shoppingRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  completedBadge: {
    backgroundColor: '#E8F5E9',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  completedBadgeText: {
    fontSize: 10,
    fontWeight: '700',
    color: '#4CAF50',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  removeBtn: {
    width: 34,
    height: 34,
    borderRadius: 10,
    backgroundColor: '#FFF0F0',
    alignItems: 'center',
    justifyContent: 'center',
  },
  removeBtnText: {
    fontSize: 15,
  },

  /* ── Pantry Tab: Search ── */
  searchCard: {
    marginHorizontal: 16,
    marginTop: 16,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 6,
    elevation: 2,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F5F5F5',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  searchIcon: {
    fontSize: 16,
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    fontSize: 15,
    color: '#1A1A1A',
    padding: 0,
  },
  clearSearch: {
    fontSize: 16,
    color: '#999',
    paddingLeft: 8,
  },

  /* Category Filter */
  categoryScroll: {
    marginTop: 12,
  },
  categoryRow: {
    paddingHorizontal: 16,
    gap: 8,
  },
  categoryChip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#EBEBEB',
    gap: 6,
  },
  categoryChipActive: {
    backgroundColor: '#FF6B35',
    borderColor: '#FF6B35',
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
    color: '#FFF',
  },
  categoryBadge: {
    backgroundColor: '#F0F0F0',
    borderRadius: 10,
    minWidth: 20,
    height: 20,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 6,
  },
  categoryBadgeActive: {
    backgroundColor: 'rgba(255,255,255,0.3)',
  },
  categoryBadgeText: {
    fontSize: 11,
    fontWeight: '700',
    color: '#777',
  },
  categoryBadgeTextActive: {
    color: '#FFF',
  },

  /* Pantry Items */
  pantryCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderRadius: 16,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 1,
  },
  pantryLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  pantryEmoji: {
    fontSize: 28,
    marginRight: 12,
  },
  pantryInfo: {
    flex: 1,
  },
  pantryName: {
    fontSize: 15,
    fontWeight: '700',
    color: '#1A1A1A',
    marginBottom: 3,
  },
  pantryDetail: {
    fontSize: 12,
    color: '#666',
    fontWeight: '500',
  },
  pantryActions: {
    flexDirection: 'row',
    gap: 6,
  },
  cartBtn: {
    width: 36,
    height: 36,
    borderRadius: 12,
    backgroundColor: 'rgba(255,107,53,0.15)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  editBtn: {
    width: 36,
    height: 36,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.8)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  deleteBtn: {
    width: 36,
    height: 36,
    borderRadius: 12,
    backgroundColor: 'rgba(255,255,255,0.8)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionBtnText: {
    fontSize: 16,
  },

  /* ── Shared ── */
  listContainer: {
    marginHorizontal: 16,
    marginTop: 16,
    gap: 10,
  },
  emptyState: {
    alignItems: 'center',
    justifyContent: 'center',
    marginHorizontal: 16,
    marginTop: 40,
    backgroundColor: '#FFFFFF',
    borderRadius: 20,
    padding: 40,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  emptyEmoji: {
    fontSize: 48,
    marginBottom: 16,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1A1A1A',
    marginBottom: 8,
  },
  emptyDesc: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    lineHeight: 20,
    marginBottom: 20,
  },
  emptyAddBtn: {
    backgroundColor: '#FF6B35',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 14,
  },
  emptyAddBtnText: {
    color: '#FFF',
    fontWeight: '700',
    fontSize: 15,
  },

  /* ── Modal ── */
  modalOverlay: {
    flex: 1,
    justifyContent: 'flex-end',
    backgroundColor: 'rgba(0,0,0,0.4)',
  },
  modalContent: {
    backgroundColor: '#FFFFFF',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    padding: 24,
    paddingBottom: 40,
  },
  modalHandle: {
    width: 40,
    height: 4,
    borderRadius: 2,
    backgroundColor: '#DDD',
    alignSelf: 'center',
    marginBottom: 20,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: '800',
    color: '#1A1A1A',
    marginBottom: 20,
  },
  inputLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: '#666',
    marginBottom: 8,
    marginTop: 12,
  },
  modalInput: {
    backgroundColor: '#F5F5F5',
    borderRadius: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
    fontSize: 15,
    color: '#1A1A1A',
  },
  quantityRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  unitChipsRow: {
    gap: 8,
    paddingVertical: 4,
  },
  unitChip: {
    backgroundColor: '#F5F5F5',
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#EBEBEB',
  },
  unitChipActive: {
    backgroundColor: '#FF6B35',
    borderColor: '#FF6B35',
  },
  unitChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#444',
  },
  unitChipTextActive: {
    color: '#FFF',
  },
  modalButtonRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: 28,
  },
  cancelBtn: {
    flex: 1,
    backgroundColor: '#F5F5F5',
    borderRadius: 14,
    paddingVertical: 14,
    alignItems: 'center',
  },
  cancelBtnText: {
    fontSize: 15,
    fontWeight: '700',
    color: '#666',
  },
  saveBtn: {
    flex: 1,
    backgroundColor: '#FF6B35',
    borderRadius: 14,
    paddingVertical: 14,
    alignItems: 'center',
    shadowColor: '#FF6B35',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.35,
    shadowRadius: 6,
    elevation: 4,
  },
  saveBtnText: {
    fontSize: 15,
    fontWeight: '700',
    color: '#FFF',
  },

  /* ── Toast ── */
  toastContainer: {
    position: 'absolute',
    top: 55,
    left: 20,
    right: 20,
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 14,
    paddingHorizontal: 18,
    borderRadius: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 12,
    elevation: 8,
    zIndex: 999,
  },
  toastSuccess: {
    backgroundColor: '#1B5E20',
  },
  toastWarning: {
    backgroundColor: '#E65100',
  },
  toastInfo: {
    backgroundColor: '#1565C0',
  },
  toastEmoji: {
    fontSize: 20,
    marginRight: 10,
  },
  toastText: {
    flex: 1,
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '600',
    lineHeight: 19,
  },
  toastCloseBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: 'rgba(255,255,255,0.2)',
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 8,
  },
  toastCloseText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
});
