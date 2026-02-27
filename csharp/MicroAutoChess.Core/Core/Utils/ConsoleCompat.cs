#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
using System;

namespace MicroAutoChess.Core
{
    public static class ConsoleCompat
    {
        public static void Write(string s)
        {
#if UNITY_5_3_OR_NEWER
            Debug.Log(s);
#else
            Console.Write(s);
#endif
        }

        public static void WriteLine(string s = "")
        {
#if UNITY_5_3_OR_NEWER
            Debug.Log(s);
#else
            Console.WriteLine(s);
#endif
        }

        public static string? ReadLine()
        {
#if UNITY_5_3_OR_NEWER
            // In Unity builds there is no Console.ReadLine; return empty string.
            return string.Empty;
#else
            return Console.ReadLine();
#endif
        }
    }
}
