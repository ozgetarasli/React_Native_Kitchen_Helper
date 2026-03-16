import React from 'react';
import { View, Text, StyleSheet, Button } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { RootStackParamList } from '../types/navigation';

type Props = NativeStackScreenProps<RootStackParamList, 'RecipeDetail'>;

export default function RecipeDetailScreen({ route, navigation }: Props) {
  const { recipeId } = route.params || {};

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Recipe Detail (ID: {recipeId})</Text>
      <Text style={styles.text}>Here are the recipe details...</Text>
      <Button title="Edit Recipe" onPress={() => navigation.navigate('AddEditRecipe', { recipeId })} />
      <Button title="Go Back" onPress={() => navigation.goBack()} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
  },
  title: {
    fontSize: 24,
    marginBottom: 20,
  },
  text: {
    marginBottom: 20,
  },
});
