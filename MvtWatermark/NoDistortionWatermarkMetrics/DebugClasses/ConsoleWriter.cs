using System;
using System.Collections.Generic;

namespace NoDistortionWatermarkMetrics.DebugClasses;

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
        var str = "";
        foreach (var elem in arr)
        {
            str += $"{elem} ";
        }
        return str;
    }

    public static string GetIEnumerableStr<T>(IEnumerable<T> arr)
    {
        var str = "";
        foreach (var elem in arr)
        {
            str += $"{elem} ";
        }
        return str;
    }
}
