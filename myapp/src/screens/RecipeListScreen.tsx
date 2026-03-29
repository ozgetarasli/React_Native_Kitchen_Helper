import React from 'react';
import { View, Text, StyleSheet, Button, FlatList } from 'react-native';
import { CompositeScreenProps } from '@react-navigation/native';
import { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { MainTabParamList, RootStackParamList } from '../types/navigation';

type Props = CompositeScreenProps<
  BottomTabScreenProps<MainTabParamList, 'RecipeList'>,
  NativeStackScreenProps<RootStackParamList>
>;

export default function RecipeListScreen({ navigation }: Props) {
  // Dummy data
  const recipes = [{ id: '1', title: 'Pasta' }, { id: '2', title: 'Salad' }];

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Recipe List</Text>
      <FlatList
        data={recipes}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <View style={styles.item}>
            <Text>{item.title}</Text>
            <Button title="Details" onPress={() => navigation.navigate('RecipeDetail', { recipeId: item.id })} />
          </View>
        )}
      />
      <Button title="Add New Recipe" onPress={() => navigation.navigate('AddEditRecipe')} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 20,
  },
  title: {
    fontSize: 24,
    marginBottom: 20,
    textAlign: 'center',
  },
  item: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    padding: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#ccc',
    marginBottom: 10,
  },
});
