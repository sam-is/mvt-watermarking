using CommandLine;
using CommandLine.Text;
using MvtWatermark.QimMvtWatermark;
using MvtWatermarkConsole.Model;
using MvtWatermarkConsole.Readers;
using System.Text;

namespace MvtWatermarkConsole;

internal class Program
{
    private static void Main(string[] args)
    {
        var parser = new Parser(with =>
        {
            with.CaseInsensitiveEnumValues = true;
            with.AutoHelp = true;
            with.HelpWriter = Console.Out;
        });

        var res = parser.ParseArguments<Options>(args);

        if (res.Errors.Any())
            return;

        var options = res.Value;

        if (options.Mode == Model.Mode.Embed && (options.Message == null || options.OutputPath == null))
        {
            var message = new StringBuilder();
            if (options.Message == null)
                message.Append("watermark parameter must be exist for embeding mode");

            if (options.OutputPath == null)
                message.Append($"{(message.Length == 0 ? "" : "\n")} output parameter must be exist for embeding mode");

            Console.WriteLine(HelpText.AutoBuild(res, _ => _, _ => _).AddPreOptionsText($"\n{message}"));
            return;
        }

        if (!File.Exists(options.Source) && !Directory.Exists(options.Source))
        {
            Console.WriteLine(HelpText.AutoBuild(res, _ => _, _ => _).AddPreOptionsText($"\nnot exist source: {options.Source} "));
            return;
        }

        if (options.ConfigPath != null && !File.Exists(options.ConfigPath))
        {
            Console.WriteLine(HelpText.AutoBuild(res, _ => _, _ => _).AddPreOptionsText($"\nnot exist config file, but select: {options.Source} "));
            return;
        }

        try
        {
            Run(res.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine(HelpText.AutoBuild(res, _ => _, _ => _).AddPreOptionsText($"exception: {ex.Message}"));
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
                var watermarked = watermark.Embed(data, options.Key, MessageTransform.GetBitArray(options.Message!));
                Writers.DataWriter.Write(watermarked, options.OutputPath!);
                break;

            case Model.Mode.Extract:
                var message = watermark.Extract(data, options.Key);
                Console.WriteLine(MessageTransform.GetMessage(message));
                break;
        }
    }
}