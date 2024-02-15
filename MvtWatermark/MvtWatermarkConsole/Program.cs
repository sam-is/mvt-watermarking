using CommandLine;
using CommandLine.Text;
using MvtWatermark.QimMvtWatermark;
using MvtWatermarkConsole.Model;
using MvtWatermarkConsole.Readers;
using MvtWatermarkConsole.Writers;
using System.Reflection;
using System.Text;

namespace MvtWatermarkConsole;

internal class Program
{
    private static void Main(string[] args)
    {
        var parser = new Parser(with =>
        {
            with.HelpWriter = null;
        });

        var res = parser.ParseArguments<Options>(args);

        var headingInfo = new HeadingInfo(programName: "MvtWatermark", version: Assembly.GetExecutingAssembly().GetName().Version?.ToString());

        if (res.Errors.Any())
        {
            var builder = SentenceBuilder.Create();
            var errorMessages = HelpText.RenderParsingErrorsTextAsLines(res, builder.FormatError, builder.FormatMutuallyExclusiveSetErrors, 1);
            Console.WriteLine(GenerateHelpText(res, headingInfo).AddPreOptionsLines(errorMessages));
            return;
        }

        var options = res.Value;

        if (options.Mode == Model.Mode.Embed && (options.Watermark == null || options.OutputPath == null))
        {
            var message = new StringBuilder();
            if (options.Watermark == null)
                message.Append("Watermark parameter must be exist for embeding mode");

            if (options.OutputPath == null)
                message.Append($"{(message.Length == 0 ? "" : "\n")} Output parameter must be exist for embeding mode");

            Console.WriteLine(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\n{message}"));
            return;
        }

        if (!File.Exists(options.Source) && !Directory.Exists(options.Source))
        {
            Console.WriteLine(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\nNot exist source: {options.Source} "));
            return;
        }

        if (options.ConfigPath != null && !File.Exists(options.ConfigPath))
        {
            Console.WriteLine(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\nNot exist config file, but select: {options.Source} "));
            return;
        }

        try
        {
            Run(res.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine(GenerateHelpText(res, headingInfo).AddPreOptionsText($"Exception: {ex.Message}"));
        }
    }

    private static void Run(Options options)
    {
        var data = DataReader.Read(options.Source, options.MinZ == null ? 0 : (int)options.MinZ, options.MaxZ == null ? 22 : (int)options.MaxZ);

        var qimWatermarkOptions = options.ConfigPath == null ? new QimMvtWatermarkOptions() : MvtWatermarkOptionsReader.Read(options.ConfigPath);
        var watermark = new QimMvtWatermark(qimWatermarkOptions);

        switch (options.Mode)
        {
            case Model.Mode.Embed:
                var watermarked = watermark.Embed(data, options.Key, MessageTransform.GetBitArray(options.Watermark!));
                DataWriter.Write(watermarked, options.OutputPath!);
                break;

            case Model.Mode.Extract:
                var message = watermark.Extract(data, options.Key);
                if (options.OutputPath != null)
                    MessageWriters.Write(options.OutputPath, MessageTransform.GetMessage(message));
                else
                    Console.WriteLine(MessageTransform.GetMessage(message));
                break;
        }
    }

    private static HelpText GenerateHelpText(ParserResult<Options> result, HeadingInfo headingInfo)
    {
        return HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = true;
            h.Heading = headingInfo;
            h.Copyright = "Copyright (c) Samara-Informsputnik";
            return h;
        }, e => e);
    }
}