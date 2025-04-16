using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MapExporterNew.Generation
{
    /// <summary>
    /// Texture cache made by Aissurtievos
    /// </summary>
    /// <typeparam name="TKey">The type to reference the textures with</typeparam>
    /// <param name="capacity">The number of elements to initialize with</param>
    public class TextureCache<TKey>(int capacity)
    {
        private readonly LinkedList<KeyValuePair<TKey, Texture2D>> list = [];
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, Texture2D>>> map = new(capacity);

        public bool Contains(TKey key)
        {
            return map.ContainsKey(key);
        }

        private void Insert(TKey key, Texture2D value)
        {
            if (map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, Texture2D>> node))
            {
                list.Remove(node);
                Object.Destroy(node.Value.Value);
            }
            else if (list.Count >= capacity)
            {
                var lruNode = list.Last;
                map.Remove(lruNode.Value.Key);
                list.RemoveLast();

                Object.Destroy(lruNode.Value.Value);
            }

            var newNode = new LinkedListNode<KeyValuePair<TKey, Texture2D>>(new KeyValuePair<TKey, Texture2D>(key, value));
            list.AddFirst(newNode);

            map[key] = newNode;
        }

        private bool TryGetValue(TKey key, out Texture2D value)
        {
            if (map.TryGetValue(key, out var node))
            {
                list.Remove(node);
                list.AddFirst(node);

                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        public Texture2D this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out var value))
                    throw new KeyNotFoundException($"Key '{key}' not found.");

                return value;
            }
            set => Insert(key, value);
        }

        public void Destroy()
        {
            foreach (var texture in list)
                Object.Destroy(texture.Value);
        }
    }
}
