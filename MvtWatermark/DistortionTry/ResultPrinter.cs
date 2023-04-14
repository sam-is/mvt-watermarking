using Distortion;
using MvtWatermark.NoDistortionWatermark.Auxiliary;
using System.Collections;

namespace DistortionTry;
public static class ResultPrinter
{
    public static void PrintDistortion(IDistortion distortion, BitArray originalMessage, BitArray extractedMessageNoDistortion, BitArray extractedMessageWithDistortion)
    {
        Console.BackgroundColor = ConsoleColor.DarkGray;
        //Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n\nИскажение: {distortion.GetType()}");
        Console.WriteLine($"Сообщение перед проверкой искажения: \t{ResultPrinter.GetWatermarkString(originalMessage)}"); // отладка

        Console.BackgroundColor = ConsoleColor.Gray;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.WriteLine($"Watermark from original tree: \t\t{GetWatermarkString(extractedMessageNoDistortion)}");
        Console.WriteLine($"Watermark from distorted tree: \t\t{GetWatermarkString(extractedMessageWithDistortion)}");

        var areEqual = extractedMessageNoDistortion.AreEqual(extractedMessageWithDistortion);
        Console.BackgroundColor = areEqual == true? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Both extracted messages (with and without distortion) are equal? - " +
            $"{areEqual}");

        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void PrintExtraction(BitArray originalMessage, BitArray embededMessage, BitArray extractedMessage)
    {
        Console.BackgroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Сообщение перед проверкой искажения: \t{ResultPrinter.GetWatermarkString(originalMessage)}"); // отладка

        Console.BackgroundColor = ConsoleColor.Gray;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.WriteLine($"Embeded message: \t\t{GetWatermarkString(embededMessage)}");
        Console.WriteLine($"Extracted message: \t\t{GetWatermarkString(extractedMessage)}");

        var areEqual = embededMessage.AreEqual(extractedMessage);
        Console.BackgroundColor = areEqual == true ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Both extracted messages (with and without distortion) are equal? - " +
            $"{areEqual}");

        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static string GetExtractionString(BitArray originalMessage, BitArray embededMessage, BitArray extractedMessage)
    {
        var resultString = "";
        resultString += $"\nEmbeded message: \t\t{GetWatermarkString(embededMessage)}";
        resultString += $"\nExtracted message: \t\t{GetWatermarkString(extractedMessage)}";

        var areEqual = embededMessage.AreEqual(extractedMessage);
        resultString += $"\nBoth extracted messages (with and without distortion) are equal? - " +
            $"{areEqual}";

        return resultString;
    }

    public static async Task Log(IDistortion distortion, BitArray originalMessage, BitArray extractedMessageNoDistortion, 
        BitArray extractedMessageWithDistortion, string filePath, double? param)
    {
        //Console.WriteLine($"\n\n[Log to file] Искажение: {distortion.GetType()}");
        var fileName = $"{distortion.GetType()}".Replace('.', '_');
        //fileName = fileName.Replace('.', '_');
        //Console.WriteLine(fileName);
        if (param == 0 || param is null)
        {
            //Console.WriteLine($"\n\nИскажение: {distortion.GetType()}, param: {param}");
            using var writerStream = new FileStream($"{filePath}\\{fileName}.txt", FileMode.Create);
        }

        //Console.WriteLine($"\n\n[Log to file] Создаём writer...");
        using (var writer = new StreamWriter($"{filePath}\\{fileName}.txt", true)) {
            await writer.WriteLineAsync($"\n\tСообщение перед проверкой искажения: \t{GetWatermarkString(originalMessage)}"); // отладка

            await writer.WriteLineAsync($"Watermark from original tree: \t\t{GetWatermarkString(extractedMessageNoDistortion)}");
            await writer.WriteLineAsync($"Watermark from distorted tree: \t\t{GetWatermarkString(extractedMessageWithDistortion)}");
            await writer.WriteLineAsync($"\tparameter: {param}");

            var areEqual = extractedMessageNoDistortion.AreEqual(extractedMessageWithDistortion);
            await writer.WriteLineAsync($"\tAre equal? {areEqual}");
        }
    }

    public static string GetWatermarkString(BitArray message)
    {
        var messageStr = "";
        foreach (var bit in message)
        {
            messageStr += $"{bit} ";
        }
        return messageStr;
    }
}
