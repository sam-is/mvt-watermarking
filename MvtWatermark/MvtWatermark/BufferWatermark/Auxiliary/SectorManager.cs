using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvtWatermark.BufferWatermark.Auxiliary;

internal struct Sector
{
    internal double LowerBorder { get; set; }
    internal double UpperBorder { get; set; }
}
internal class SectorManager
{
    internal static List<Sector> DivideBySectors(uint d)
    {
        var sectorSize = (double)360/d;
        var sectorsList = new List<Sector>();
        var currentLowerBorder = 0.0;
        for (var i = 0; i < d-1; i++) // учитывая неточности с вещественными числами при использовании
                                      // двоичной системы счисления, последний сектор отдельно верхней границей
                                      // накрываем. Чтобы потом не было например сравнения с тангенсом угла 361 градус
        {
            var sector = new Sector { LowerBorder = currentLowerBorder, UpperBorder = currentLowerBorder + sectorSize};
            currentLowerBorder = sector.UpperBorder;
            sectorsList.Add(sector);
        }
        sectorsList.Add(new Sector { LowerBorder = currentLowerBorder, UpperBorder = 360.0 });

        return sectorsList;
    }
}
