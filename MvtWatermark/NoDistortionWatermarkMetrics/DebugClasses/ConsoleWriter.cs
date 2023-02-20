using System.Collections;

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

    public static void WriteBitArrayStr(BitArray bitArr)
    {
        Console.WriteLine(GetBitArrayStr(bitArr));
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

    public static string GetBitArrayStr(BitArray bitArr)
    {
        var str = "";
        foreach (var elem in bitArr)
        {
            str += $"{elem} ";
        }
        return str;
    }
}
