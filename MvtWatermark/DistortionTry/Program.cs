using DistortionTry;
using System.Collections;


var parameterSetsStp = new List<CoordinateSet>()
{
    new CoordinateSet(10, 658, 332),
    new CoordinateSet(10, 658, 333),
    new CoordinateSet(10, 658, 334),
    new CoordinateSet(10, 658, 338),
            //new ZxySet(10, 658, 335), // кривой тайл, не считывается
    //new CoordinateSet(10, 658, 337),
};
var parameterSetsTegola = new List<CoordinateSet>();


var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, true, false, true,
        true, false, true, false, true, true, false, true, false, true, true, false, true, true, false, true, false, true, false, true, true, true, false};
var message = new BitArray(boolArr);

var taskLsLf = new Task(() => {
    Console.WriteLine("taskLsLf started");
    TestLsLf(parameterSetsStp, parameterSetsTegola, message);
    Console.WriteLine("taskLsLf completed");
});
var taskMNb = new Task(() => {
    Console.WriteLine("taskMNb started");
    TestsMNb(parameterSetsStp, parameterSetsTegola, message);
    Console.WriteLine("taskMNb completed");
});

taskLsLf.Start();
taskMNb.Start();

Console.WriteLine("tasks started");

taskLsLf.Wait();
taskMNb.Wait();

Console.WriteLine("tasks completed");

/*
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
*/


/*
    var boolArr = new bool[] { true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false,
        true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, false, true, true, true, false};
    var message = new BitArray(boolArr);
*/

//optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 3, Mmax = 3, Nbmin = 3, Nbmax = 3, Lfmin = 10, Lfmax = 10, Lsmin = 1, Lsmax = 15 };
//DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Ls\\");
/*
var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 2, Mmax = 6, Nbmin = 3, Nbmax = 3, Lfmin = 15, Lfmax = 15, Lsmin = 3, Lsmax = 7 };
DistortionTester.DiffWatermarkParametersTest(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_M\\");
*/

//ErrorsWmTester.Test();

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

void TestLsLf(List<CoordinateSet> parameterSetsStp, List<CoordinateSet> parameterSetsTegola, BitArray message)
{
    var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 3, Mmax = 3, Nbmin = 3, Nbmax = 3, Lfmin = 1, Lfmax = 15, Lsmin = 1, Lsmax = 15 };
    var distortionTester = new DistortionTester();
    distortionTester.DiffWatermarkParametersTest_Ls_Lf(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_Lf_Ls\\");

}

void TestsMNb(List<CoordinateSet> parameterSetsStp, List<CoordinateSet> parameterSetsTegola, BitArray message)
{
    var optionsParamRanges = new DistortionTester.OptionsParamRanges() { Mmin = 1, Mmax = 10, Nbmin = 2, Nbmax = 10, Lfmin = 10, Lfmax = 10, Lsmin = 10, Lsmax = 10 };
    var distortionTester = new DistortionTester();
    distortionTester.DiffWatermarkParametersTest_M_Nb(parameterSetsStp, parameterSetsTegola, optionsParamRanges, message, "testing_M_Nb\\");
}