import React from 'react';
import { View, Text, StyleSheet, Button, TextInput } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { RootStackParamList } from '../types/navigation';

type Props = NativeStackScreenProps<RootStackParamList, 'AddEditRecipe'>;

export default function AddEditRecipeScreen({ route, navigation }: Props) {
  const { recipeId } = route.params || {};

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{recipeId ? 'Edit Recipe' : 'Add New Recipe'}</Text>
      <TextInput placeholder="Recipe Title" style={styles.input} />
      <TextInput placeholder="Ingredients" style={[styles.input, { height: 80 }]} multiline />
      <Button title="Save" onPress={() => navigation.goBack()} />
      <Button title="Cancel" onPress={() => navigation.goBack()} />
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
  input: {
    height: 40,
    width: '100%',
    borderColor: 'gray',
    borderWidth: 1,
    marginBottom: 10,
    paddingHorizontal: 10,
  },
});
