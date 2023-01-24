using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvtWatermark.DebugClasses
{
    public static class ConsoleWriter
    {
        public static void WriteArray<T>(T[] arr)
        {
            Console.WriteLine(GetArrayStr(arr));
        }

        public static void WriteIEnumerable<T>(IEnumerable<T> arr)
        {
            Console.WriteLine(GetIEnumerableStr(arr));
        }

        public static string GetArrayStr<T>(T[] arr)
        {
            //Console.WriteLine($"Arr = {arr}");
            string str = "";
            foreach (var elem in arr)
            {
                str += $"{elem} ";
            }
            return str;
        }

        public static string GetIEnumerableStr<T>(IEnumerable<T> arr)
        {
            string str = "";
            foreach (var elem in arr)
            {
                str += $"{elem} ";
            }
            return str;
        }
    }
}
