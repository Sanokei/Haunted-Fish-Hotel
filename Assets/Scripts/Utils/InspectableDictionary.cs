using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingBlocks.DataTypes {

    /// <summary>Same usage as generic dictionary. But will create serialized fields in the inspector for this dictionary.<br> Will synchronize dictionary and inspector during play mode for inspection. No manual adding in editor during play mode.</br></summary>
    [Serializable]
    public class InspectableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IEnumerable {

        //---
        // Inspectable Lists

        [SerializeField, Tooltip("Items can be added in the inspector, only prior to play mode not at runtime, and will be exposed through a dictionary.")]
        private List<TKey> ItemKeys = new List<TKey>();

        [SerializeField, Tooltip("Items can be added in the inspector, only prior to play mode not at runtime, and will be exposed through a dictionary.")]
        private List<TValue> ItemValues = new List<TValue>();

        //---
        // Internal Use

        private bool inspectableSync;
        private readonly Dictionary<TKey, TValue> lookup = new Dictionary<TKey, TValue>();

        public TValue this[TKey key] {
            get {
                InspectorToDictionary();
                return lookup[key];
            }
            set {
                lookup[key] = value;
                DictionaryToInspectorAdditive(key, value);
            }
        }

        public TValue this[int index] {
            get {
                DictionaryToInspector();
                return ItemValues[index];
            }
            set {
                ItemValues[index] = value;
                inspectableSync = false;
                InspectorToDictionary();
            }
        }

        //---
        // Synchronization Methods

        /// <summary>Copies in full the dictionary as lists to the inspector lists.</summary>
        private void DictionaryToInspector() {
            ItemKeys.AddRange(lookup.Keys.ToList());
            ItemValues.AddRange(lookup.Values.ToList());
        }

        /// <summary>Adds a Key-Value pair to the inspector lists.</summary>
        private void DictionaryToInspectorAdditive(TKey key, TValue value) {
            ItemKeys.Add(key);
            ItemValues.Add(value);
        }

        /// <summary>Removes a Key-Value pair to the inspector lists.</summary>
        private void DictionaryToInspectorNegative(TKey key, TValue value) {
            ItemKeys.Remove(key);
            ItemValues.Remove(value);
        }

        /// <summary>Clears dictionary, rebuilds from serialized inspector lists. Removes duplicates by copying back to lists.</summary>
        private void InspectorToDictionary() {
            if (inspectableSync is false) {
                lookup.Clear();
                for (int i = ItemKeys.Count - 1; i >= 0; i--) {
                    var item = ItemKeys[i];
                    var value = ItemValues[i];
                    if (item != null && value != null) {
                        lookup[item] = value;
                    }
                    else {
                        ItemKeys.RemoveAt(i); ItemValues.RemoveAt(i);
                    }
                }
                ItemKeys = lookup.Keys.ToList();
                ItemValues = lookup.Values.ToList();
                inspectableSync = true;
            }
        }

        //---
        // IDictionary, ICollection, IEnumerable

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public ICollection<TKey> Keys => lookup.Keys;

        public ICollection<TValue> Values => lookup.Values;

        public int Count => lookup.Count;

        public void Add(TKey key, TValue value) {
            InspectorToDictionary();
            lookup.Add(key, value);
            DictionaryToInspectorAdditive(key, value);
        }

        public void Clear() {
            lookup.Clear();
            ItemKeys.Clear();
            ItemValues.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            InspectorToDictionary();
            return lookup.Contains(item);
        }

        public bool ContainsKey(TKey key) {
            InspectorToDictionary();
            return lookup.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            InspectorToDictionary();
            var exists = lookup.TryGetValue(key, out TValue backer);
            value = backer;
            return exists;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            InspectorToDictionary();
            return lookup.GetEnumerator();
        }

        public bool Remove(TKey key) {
            var success = lookup.Remove(key);
            DictionaryToInspectorNegative(key, lookup[key]);
            return success;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            InspectorToDictionary();
            return lookup.GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            lookup.Add(item.Key, item.Value);
            DictionaryToInspectorAdditive(item.Key, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            foreach (var kvp in lookup) {
                array[arrayIndex++] = kvp;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            var success = lookup.Remove(item.Key);
            DictionaryToInspectorNegative(item.Key, item.Value);
            return success;
        }
    }
}