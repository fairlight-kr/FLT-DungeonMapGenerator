using System;
using System.Collections.Generic;

namespace FLT_DungeonMapGenerator
{
    public static class CopyStack
    {
        public static Stack<T> Clone<T>(this Stack<T> original)
        {
            var arr = new T[original.Count];
            original.CopyTo(arr, 0);
            Array.Reverse(arr);
            return new Stack<T>(arr);
        }
    }
}
