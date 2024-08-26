using CommandLine;
using CommandLine.Text;
using MvtWatermark.QimMvtWatermark;
using MvtWatermarkConsole.Model;
using MvtWatermarkConsole.Readers;
using MvtWatermarkConsole.Writers;
using Spectre.Console;
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
            AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsLines(errorMessages));
            return;
        }

        var options = res.Value;

        if (options.Mode == Model.Mode.Embed)
        {
            if (options.OutputPath == null)
            {
                if (!TypeChecker.IsMbtiles(options.Source))
                {
                    AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText($"[red]Output parameter must be exist for embeding mode[/]"));
                    return;
                }

                options.OutputPath = options.Source;
            }
            else
            {
                if (TypeChecker.IsMbtiles(options.OutputPath))
                {
                    if(TypeChecker.IsMbtiles(options.Source))
                        File.Copy(options.Source, options.OutputPath, true);
                    else
                    {
                        AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText($"[red]Source parameter must be mbtiles if output parameter is mbtiles[/]"));
                        return;
                    }
                }
            }
        }

        if (options.Mode == Model.Mode.Embed && (options.Watermark == null))
        {
            var message = new StringBuilder();
            if (options.Watermark == null)
                message.Append("[red]Watermark parameter must be exist for embeding mode[/]");

            AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\n{message}"));
            return;
        }

        if (!File.Exists(options.Source) && !Directory.Exists(options.Source))
        {
            AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\n[red]Not exist source: {options.Source}[/]"));
            return;
        }

        if (options.ConfigPath != null && !options.IsGenerateConfig && !File.Exists(options.ConfigPath))
        {
            AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText($"\n[red]Not exist config file, but select: {options.Source}[/]"));
            return;
        }

        try
        {
            Run(res.Value);
        }
        catch (Exception ex)
        {
            AnsiConsole.Markup(GenerateHelpText(res, headingInfo).AddPreOptionsText("\n[red]Exception[/]"));
            AnsiConsole.WriteException(ex);
        }
    }

    private static void Run(Options options)
    {
        var data = DataReader.Read(options.Source, options.IsNoCompression, options.MinZ ?? 0, options.MaxZ ?? 22);

        var qimWatermarkOptions = options.IsGenerateConfig || options.ConfigPath == null ? new QimMvtWatermarkOptions() : MvtWatermarkOptionsReader.Read(options.ConfigPath);

        var watermark = new QimMvtWatermark(qimWatermarkOptions);

        switch (options.Mode)
        {
            case Model.Mode.Embed:

                var bits = MessageTransformer.GetBitArray(options.Watermark!);

                if (options.IsGenerateConfig || options.IsUpdateConfig)
                {
                    if (qimWatermarkOptions.Mode == MvtWatermark.QimMvtWatermark.Mode.WithTilesMajorityVote)
                        qimWatermarkOptions.MessageLength = bits.Length;

                    MvtWatermarkOptionsWriter.Write(qimWatermarkOptions, options.ConfigPath ?? "config.json");
                }

                var watermarked = watermark.Embed(data, options.Key, bits);

                AnsiConsole.Markup("[green]Watermark is embeded[/]\n");
                DataWriter.Write(watermarked, options.OutputPath!, options.IsNoCompression);
                AnsiConsole.Markup("[green]Tiles are written[/]");
                break;

            case Model.Mode.Extract:
                var message = watermark.Extract(data, options.Key);
                if (options.OutputPath != null)
                    MessageWriters.Write(options.OutputPath, MessageTransformer.GetMessage(message));
                else
                    AnsiConsole.Markup($"[green]Watermark: [/]{MessageTransformer.GetMessage(message)}");
                break;
        }
    }

    private static HelpText GenerateHelpText(ParserResult<Options> result, HeadingInfo headingInfo)
    {
        return HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = true;
            h.Heading = headingInfo;
            h.Copyright = "Copyright (c) [blue]Samara-Informsputnik[/]";
            return h;
        }, e => e);
    }
}