import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';
import Constants from 'expo-constants';
import { AUTH_TOKEN_KEY } from './authSession';
import { navigateTo } from './navigationRef';

// Geliştirme cihazının yerel IP adresini otomatik bul
let HOST_IP = '192.168.1.3'; // Başlangıç / Fallback değeri
const debuggerHost = Constants.expoConfig?.hostUri;

if (debuggerHost) {
  // Expo'nun kendi Metro sunucusunun IP adresini alıyoruz (ör. "192.168.0.15:8081" -> "192.168.0.15")
  HOST_IP = debuggerHost.split(':')[0];
} else if (Platform.OS === 'android') {
  // Emülatör kullanılıyorsa, Expo Go uygulaması dışındaysa varsayılan android loopback ip
  HOST_IP = '10.0.2.2';
} else {
  HOST_IP = 'localhost';
}

console.log('🔗 Dinamik Backend IP Adresi:', HOST_IP);
export const API_URL = `http://${HOST_IP}:5039/api`;
export const MEDIA_URL = `http://${HOST_IP}:5039`;

const api = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use(
  async (config) => {
    const token = await AsyncStorage.getItem(AUTH_TOKEN_KEY);

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    } else if (config.headers?.Authorization) {
      delete config.headers.Authorization;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await AsyncStorage.multiRemove([AUTH_TOKEN_KEY, 'user_info']);
      navigateTo('Login');
    }
    return Promise.reject(error);
  }
);

export default api;
