using DistortionTry;
using System.Collections;

/*
var parameterSetsStp = new List<CoordinateSet>()
{
    new CoordinateSet(10, 658, 332),
    new CoordinateSet(10, 658, 333),
    new CoordinateSet(10, 658, 334),
    new CoordinateSet(10, 658, 338),
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
    new CoordinateSet(10, 658, 337),
};
var parameterSetsTegola = new List<CoordinateSet>();
*/


var parameterSetsStp = new List<CoordinateSet>()
{
    new CoordinateSet(10, 653, 333),
    new CoordinateSet(10, 653, 334)
};
var parameterSetsTegola = new List<CoordinateSet>()
{
    new CoordinateSet(10, 292, 385),
    new CoordinateSet(10, 293, 385)
};

var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false,
        true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false, true, true, true, false};
var message = new BitArray(boolArr);


//var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmax = 5, Nbmax = 4, Lfmax = 10, Lsmax = 5 };

////var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 2, Mmax = 2, Nbmin = 3, Nbmax = 3, Lfmin = 1, Lfmax = 15, Lsmin = 5, Lsmax = 5 };

//DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Lf\\");
////DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_DeletingByPerimeter_Magnifier\\");
//optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 2, Mmax = 3, Nbmin = 3, Nbmax = 3, Lfmin = 10, Lfmax = 10, Lsmin = 1, Lsmax = 6 };
//DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Ls\\");
/*
var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 2, Mmax = 6, Nbmin = 3, Nbmax = 3, Lfmin = 15, Lfmax = 15, Lsmin = 3, Lsmax = 7 };
DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_M\\");

boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, true, false, true,
        true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, false, true, false, true, true, true, false};
message = new BitArray(boolArr);

optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 2, Mmax = 2, Nbmin = 2, Nbmax = 10, Lfmin = 15, Lfmax = 15, Lsmin = 3, Lsmax = 3 };
DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Nb\\");
*/

ErrorsWmTester.Test();

/*
var optionsParamRanges = new ExtractionTester.OptionsParamRanges() { Mmin = 2, Mmax = 2, Nbmin = 3, Nbmax = 3, Lfmin = 1, Lfmax = 15, Lsmin = 5, Lsmax = 5 };
ExtractionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Lf\\");
optionsParamRanges = new ExtractionTester.OptionsParamRanges() { Mmin = 2, Mmax = 3, Nbmin = 3, Nbmax = 3, Lfmin = 10, Lfmax = 10, Lsmin = 1, Lsmax = 6 };
ExtractionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Ls\\");

optionsParamRanges = new ExtractionTester.OptionsParamRanges() { Mmin = 2, Mmax = 6, Nbmin = 3, Nbmax = 3, Lfmin = 15, Lfmax = 15, Lsmin = 3, Lsmax = 7 };
ExtractionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_M\\");

boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, true, false, true,
        true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, false, true, false, true, true, true, false};
message = new BitArray(boolArr);

optionsParamRanges = new ExtractionTester.OptionsParamRanges() { Mmin = 2, Mmax = 2, Nbmin = 2, Nbmax = 10, Lfmin = 15, Lfmax = 15, Lsmin = 3, Lsmax = 3 };
ExtractionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Nb\\");
*/
