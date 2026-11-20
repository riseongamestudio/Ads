using System;
using System.Collections.Generic;

namespace RiseOn.Ads {
    internal static class AdRandom {
        /// <summary>
        /// One item drawn with odds proportional to its weight.<br/>
        /// A negative weight is an error in the weights, so every item then gets even odds and a warning says so.
        /// </summary>
        public static T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weightOf) {
            if (items == null || items.Count == 0) throw new ArgumentException("No items to pick from", nameof(items));

            var total = 0d;
            foreach (var item in items) {
                var weight = weightOf(item);
                if (weight < 0) {
                    UnityEngine.Debug.LogWarning($"{nameof(AdRandom)}: a negative weight ({weight}), drawing at even odds");
                    return items[UnityEngine.Random.Range(0, items.Count)];
                }

                total += weight;
            }

            if (total <= 0) return items[UnityEngine.Random.Range(0, items.Count)];

            var draw = UnityEngine.Random.value * total;
            foreach (var item in items) {
                draw -= weightOf(item);
                if (draw < 0) return item;
            }

            return items[items.Count - 1];
        }
    }
}