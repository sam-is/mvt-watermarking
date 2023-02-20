using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoDistortionWatermarkMetrics.Additional;
/// <summary>
/// Набор диапазона параметров NoDistortionWatermarkOptions для проверки валидности встроенных/извлеченных ЦВЗ
/// </summary>
public class ParameterRangeSet
{
    public int Mmax { get; set; }
    public int Nbmax { get; set; }
    public int Lfmax { get; set; }
    public int Lsmax { get; set; }
    private int _wmMin;
    private int _wmMax;
    public int WmMin
    {
        get { return _wmMin; }
        set
        {
            if (value < 1)
                throw new ArgumentException("WmMin cannot be smaller then 1");
            else if (value > _wmMax)
                throw new ArgumentException("WmMin cannot be bigger then WmMax");
            _wmMin = value;
        }
    }
    public int WmMax
    {
        get { return _wmMax; }
        set
        {
            if (value < 1)
                throw new ArgumentException("WmMax cannot be smaller then 1");
            else if (value < _wmMin)
                throw new ArgumentException("WmMax cannot be smaller then WmMin");
            _wmMax = value;
        }
    }

    public ParameterRangeSet(int mMax, int nbMax, int lfMax, int lsMax, int wmMin, int wmMax)
    {
        if (wmMin > wmMax)
            throw new ArgumentException("WmMin cannot be bigger then WmMax");
        else if (wmMin < 1 || wmMax < 1)
        {
            throw new ArgumentException("WmMin and WmMax cannot be smaller then 1");
        }

        Mmax = mMax;
        Nbmax = nbMax;
        Lfmax = lfMax;
        Lsmax = lsMax;
        _wmMin = wmMin;
        _wmMax = wmMax;
    }
}

