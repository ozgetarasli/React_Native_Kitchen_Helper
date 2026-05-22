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
import { setAuthenticatedSession } from '../services/authSession';

type Props = NativeStackScreenProps<RootStackParamList, 'Login'>;

export default function LoginScreen({ navigation }: Props) {
  const { width } = useWindowDimensions();
  const isCompact = width < 360;

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    if (!email || !password) {
      Alert.alert('Hata', 'Lütfen email ve şifre giriniz.');
      return;
    }

    try {
      setLoading(true);
      const response = await api.post('/Auth/login', {
        email,
        password
      });

      if (response.data && response.data.token) {
        await setAuthenticatedSession(response.data.token, response.data.user);
        navigation.replace('MainApp');
      }
    } catch (error: any) {
      const msg = error.response?.data?.message || 'Giriş sırasında bir hata oluştu.';
      Alert.alert('Giriş Başarısız', msg);
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
                <Text style={styles.brandGlyph}>🍳</Text>
              </View>
              <Text style={[styles.logo, { fontSize: isCompact ? 28 : 34 }]}>KitchenHelper</Text>
            </View>

            <Text style={styles.title}>Tekrar hos geldiniz</Text>
            <Text style={styles.subtitle}>Tariflerinize ve listelerinize kaldiginiz yerden devam edin.</Text>

            <View style={styles.inputWrap}>
              <TextInput
                placeholder="Email"
                style={styles.input}
                placeholderTextColor="#9AA2B1"
                keyboardType="email-address"
                autoCapitalize="none"
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

            <TouchableOpacity
              style={[styles.loginButton, loading && styles.disabledButton]}
              onPress={handleLogin}
              disabled={loading}
              activeOpacity={0.9}
            >
              {loading ? (
                <ActivityIndicator color="#fff" />
              ) : (
                <Text style={styles.loginButtonText}>Giris Yap</Text>
              )}
            </TouchableOpacity>

            <TouchableOpacity onPress={() => navigation.navigate('Register')}>
              <Text style={styles.linkText}>Hesabiniz yok mu? Kayit Ol</Text>
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
    flexGrow: 1,
    justifyContent: 'center',
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
    lineHeight: 20,
    marginBottom: 24,
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
  loginButton: {
    backgroundColor: '#E85D2A',
    height: 56,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 8,
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
  loginButtonText: {
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

