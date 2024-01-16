using MvtWatermark.QimMvtWatermark;

namespace MvtWatermarkingService.Services;

public class OptionsBuilder(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;

    public QimMvtWatermarkOptions GetOptions()
    {
        var section = Configuration.GetSection("WatermarkingOptions");
        var k = section.GetValue<double>("K");
        var t2 = section.GetValue<double>("T2");
        var t1 = section.GetValue<int>("T1");
        var extent = section.GetValue<int>("Extent");
        var distance = section.GetValue<int>("Distance");
        var nb = section.GetValue<int?>("Nb");
        var r = section.GetValue<int?>("R");
        var m = section.GetValue<int?>("M");
        var isGeneralExtractionMethod = section.GetValue<bool>("IsGeneralExtractionMethod");
        var mode = section.GetValue<string>("Mode");

        mode ??= "WithTilesMajorityVote";

        return new QimMvtWatermarkOptions(k, t2, t1, extent, distance, nb, r, m, isGeneralExtractionMethod, Enum.Parse<Mode>(mode));
    }
}
