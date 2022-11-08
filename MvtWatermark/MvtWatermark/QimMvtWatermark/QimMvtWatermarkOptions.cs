﻿using System;

namespace MvtWatermark.QimMvtWatermark
{
    public class QimMvtWatermarkOptions
    {
        public double Delta2 { get; set; }
        public double T2 { get; set; }
        public int Extent { get; set; }
        public int CountPoints { get; set; }
        public int Distance { get; set; }
        public int M { get; set; }

        public QimMvtWatermarkOptions(double delta2, double t2, int extent, int countPoints, int distance, int m)
        {
            Delta2 = delta2;
            T2 = t2;
            Extent = extent;
            CountPoints = countPoints;
            Distance = distance;
            M = m;
        }
    }
}