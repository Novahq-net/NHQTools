using System;
using System.Collections.Generic;

namespace NHQTools.Extensions
{
    public static class ListExtensions
    {
        // Determines if a specified index is within the valid range of list indices.
        public static bool IndexExists<T>(this IList<T> list, int index) => index >= 0 && index < list.Count;
  

        // Attempts to retrieve the element at the specified index in the list
        public static bool TryGetIndex<T>(this IList<T> list, int index, out T result)
        {
            if (index >= 0 && index < list.Count)
            {
                result = list[index];
                return true;
            }

            result = default;
            return false;
        }

        // Executes a specified action for each element in the enumerable collection.
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
                action(item);

        }

    }

}