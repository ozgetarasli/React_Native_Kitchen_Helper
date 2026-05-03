import React, { useState } from 'react';
import { 
  View, 
  Text, 
  StyleSheet, 
  TextInput, 
  TouchableOpacity, 
  KeyboardAvoidingView, 
  Platform, 
  ScrollView,
  Alert,
  ActivityIndicator,
  useWindowDimensions
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { SafeAreaView } from 'react-native-safe-area-context';
import { RootStackParamList } from '../types/navigation';
import api from '../services/api';

type Props = NativeStackScreenProps<RootStackParamList, 'Register'>;

export default function RegisterScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const isCompact = width < 360;

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleRegister = async () => {
    if (!email || !password || !confirmPassword) {
      Alert.alert('Hata', 'Lütfen tüm alanları doldurun.');
      return;
    }

    if (password !== confirmPassword) {
      Alert.alert('Hata', 'Şifreler eşleşmiyor.');
      return;
    }

    try {
      setLoading(true);
      await api.post('/Auth/register', {
        email,
        password
      });

      Alert.alert('Başarılı', 'Kayıt başarılı, lütfen giriş yapın.', [
        { text: 'Tamam', onPress: () => navigation.navigate('Login') }
      ]);
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Kayıt sırasında bir hata oluştu.';
      Alert.alert('Kayıt Başarısız', msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.safeArea}>
      <KeyboardAvoidingView 
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.container}
      >
        <ScrollView
          contentContainerStyle={[
            styles.scrollContent,
            { paddingHorizontal: isCompact ? 16 : 24 },
          ]}
          showsVerticalScrollIndicator={false}
          keyboardShouldPersistTaps="handled"
        >
          <View style={[styles.card, { maxWidth: 460, padding: isCompact ? 20 : 28 }]}>
            <View style={styles.brandRow}>
              <View style={styles.brandIconWrap}>
                <Text style={styles.brandGlyph}>✨</Text>
              </View>
              <Text style={[styles.logo, { fontSize: isCompact ? 26 : 32 }]}>KitchenHelper</Text>
            </View>

            <Text style={styles.title}>Hesap olustur</Text>
            <Text style={styles.subtitle}>Kisisel tariflerinizi yonetin ve favorilerinizi hemen kaydedin.</Text>

            <View style={styles.inputWrap}>
              <TextInput 
                placeholder="Email" 
                style={styles.input} 
                keyboardType="email-address"
                autoCapitalize="none"
                placeholderTextColor="#9AA2B1"
                value={email}
                onChangeText={setEmail}
              />
            </View>

            <View style={styles.inputWrap}>
              <TextInput 
                placeholder="Sifre" 
                style={styles.input} 
                secureTextEntry 
                placeholderTextColor="#9AA2B1"
                value={password}
                onChangeText={setPassword}
              />
            </View>

            <View style={styles.inputWrap}>
              <TextInput 
                placeholder="Sifreyi onayla" 
                style={styles.input} 
                secureTextEntry 
                placeholderTextColor="#9AA2B1"
                value={confirmPassword}
                onChangeText={setConfirmPassword}
              />
            </View>

            <TouchableOpacity 
              style={[styles.registerButton, loading && styles.disabledButton]} 
              onPress={handleRegister}
              disabled={loading}
              activeOpacity={0.9}
            >
              {loading ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.registerButtonText}>Kayit ol</Text>
              )}
            </TouchableOpacity>

            <TouchableOpacity onPress={() => navigation.navigate('Login')}>
              <Text style={styles.linkText}>Zaten hesabiniz var mi? Giris yapin</Text>
            </TouchableOpacity>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
    backgroundColor: '#FFFDF9',
  },
  container: {
    flex: 1,
  },
  scrollContent: {
    justifyContent: 'center',
    flexGrow: 1,
    alignItems: 'center',
  },
  card: {
    width: '100%',
    backgroundColor: '#FFFFFF',
    borderRadius: 24,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.08,
    shadowRadius: 18,
    elevation: 5,
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 14,
    gap: 10,
  },
  brandIconWrap: {
    width: 38,
    height: 38,
    borderRadius: 19,
    backgroundColor: '#FFF1E9',
    alignItems: 'center',
    justifyContent: 'center',
  },
  brandGlyph: {
    fontSize: 20,
  },
  logo: {
    fontWeight: 'bold',
    color: '#E85D2A',
  },
  title: {
    fontSize: 22,
    fontWeight: '700',
    color: '#1C2636',
    textAlign: 'center',
    marginBottom: 6,
  },
  subtitle: {
    fontSize: 14,
    color: '#6F7785',
    textAlign: 'center',
    marginBottom: 24,
    lineHeight: 20,
  },
  inputWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F4F6FA',
    borderRadius: 14,
    height: 56,
    paddingHorizontal: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#ECEFF4',
  },
  input: {
    flex: 1,
    height: '100%',
    fontSize: 16,
    color: '#1C2636',
  },
  registerButton: {
    backgroundColor: '#E85D2A',
    height: 56,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 10,
    shadowColor: '#E85D2A',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.28,
    shadowRadius: 8,
    elevation: 5,
  },
  disabledButton: {
    opacity: 0.75,
  },
  registerButtonText: {
    color: '#fff',
    fontSize: 17,
    fontWeight: '700',
  },
  linkText: {
    color: '#E85D2A',
    textAlign: 'center',
    marginTop: 18,
    fontWeight: '600',
  },
});
