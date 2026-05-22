import AsyncStorage from '@react-native-async-storage/async-storage';

export const AUTH_TOKEN_KEY = 'auth_token';
export const USER_INFO_KEY = 'user_info';

export const setAuthenticatedSession = async (token: string, user?: unknown): Promise<void> => {
  await AsyncStorage.setItem(AUTH_TOKEN_KEY, token);

  if (user != null) {
    await AsyncStorage.setItem(USER_INFO_KEY, JSON.stringify(user));
  }
};

export const clearSession = async (): Promise<void> => {
  await AsyncStorage.multiRemove([AUTH_TOKEN_KEY, USER_INFO_KEY]);
};

export const hasAuthToken = async (): Promise<boolean> => {
  const token = await AsyncStorage.getItem(AUTH_TOKEN_KEY);
  return !!token;
};

export const canUseProtectedFeatures = async (): Promise<{ allowed: boolean; message: string }> => {
  const token = await AsyncStorage.getItem(AUTH_TOKEN_KEY);

  if (!token) {
    return {
      allowed: false,
      message: 'Bu islem icin giris yapmaniz gerekiyor.',
    };
  }

  return { allowed: true, message: '' };
};
