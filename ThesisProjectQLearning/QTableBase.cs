using System.Collections;
using System.Collections.Generic;

namespace ThesisProjectQLearning
{
    public abstract class QTableBase<T> : IDictionary<T, double>
    {
        protected Dictionary<T, double> dict_internal;

        public double this[T key]
        {
            get { return dict_internal[key]; }
            set { dict_internal[key] = value; }
        }

        public ICollection<T> Keys { get { return dict_internal.Keys; } }

        public ICollection<double> Values { get { return dict_internal.Values; } }

        public int Count { get { return dict_internal.Count; } }

        public bool IsReadOnly { get { return false; } }

        protected double Maximum { get; protected set; }


        public void Add(T key, double value)
        {
            dict_internal.Add(key, value);
            if (value > Maximum)
                Maximum = value;
        }

        public void Add(KeyValuePair<T, double> item)
        {
            dict_internal.Add(item.Key, item.Value);
            if (item.Value > Maximum)
                Maximum = item.Value;
        }

        public void Clear()
        {
            dict_internal.Clear();
        }

        public bool Contains(KeyValuePair<T, double> item)
        {
            return dict_internal.ContainsKey(item.Key) && dict_internal[item.Key] == item.Value;
        }

        public bool ContainsKey(T key)
        {
            return dict_internal.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<T, double>[] array, int arrayIndex)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerator<KeyValuePair<T, double>> GetEnumerator()
        {
            return dict_internal.GetEnumerator();
        }

        public bool Remove(T key)
        {
            return dict_internal.Remove(key);
        }

        /// <summary>
        /// removes if both key and value matches
        /// </summary>
        public bool Remove(KeyValuePair<T, double> item)
        {
            if (!Contains(item))
                return false;
            return dict_internal.Remove(item.Key);
        }

        public bool TryGetValue(T key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out double value)
        {
            if (dict_internal.ContainsKey(key))
            {
                value = dict_internal[key];
                return true;
            }
            value = 0.0f;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dict_internal.GetEnumerator();
        }
    }
}
