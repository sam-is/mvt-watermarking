using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MvtWatermark.BufferWatermark.Auxiliary;

public class Sector
{
    public double LowerBorder { get; set; }
    public double UpperBorder { get; set; }
    public bool EtaK { get; set; } // Эта катая. Элемент последовательности {Eta_k}
}
public class SectorManager
{
    public static List<Sector> DivideBySectors(uint d, int key)
    {
        //var sectorSize = (double)360/d;
        List<int> etaSequence = GenerateEtaSequence(key, d);

        var sectorSize = 2 * Math.PI / d;
        var sectorsList = new List<Sector>();
        var currentLowerBorder = 0.0;
        for (var i = 0; i < d-1; i++) // учитывая неточности с вещественными числами при использовании
                                      // двоичной системы счисления, последний сектор отдельно верхней границей
                                      // накрываем. Чтобы потом не было например сравнения с тангенсом угла 361 градус
        {
            var sector = new Sector { LowerBorder = currentLowerBorder, UpperBorder = currentLowerBorder + sectorSize, 
                EtaK = Convert.ToBoolean(etaSequence[i]) };
            currentLowerBorder = sector.UpperBorder;
            sectorsList.Add(sector);
        }
        //sectorsList.Add(new Sector { LowerBorder = currentLowerBorder, UpperBorder = 360.0 });
        sectorsList.Add(new Sector { LowerBorder = currentLowerBorder, UpperBorder = 2 * Math.PI });

        return sectorsList;
    }

    public static List<int> GenerateEtaSequence(int key, uint d)
    {
        var sequence = new List<int>(Convert.ToInt32(d));
        var rand = new Random(key);
        for (var i = 0; i < d; i++)
        {
            sequence.Add(rand.Next(0, 2));
        }
        return sequence;
    }
}
