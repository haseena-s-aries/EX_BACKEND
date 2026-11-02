using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;

internal static class InspectionPlanningChecks
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new InvalidOperationException($"Inspection planning check failed: {name}");}
    public static void Run()
    {
        var a=InspectionPlanningCalculator.Allocate(3223,90,10,5,AllocationRoundingRule.Nearest);
        var b=InspectionPlanningCalculator.Allocate(2354,90,10,5,AllocationRoundingRule.Nearest);
        Check(a.Close==2901&&a.Detailed==322&&a.Visual==0,"nearest rounding for 2026 example list");
        Check(b.Close==2119&&b.Detailed==235&&b.Visual==0,"nearest rounding for 2027 example list");
        Check(a.Close+a.Detailed+a.Visual+b.Close+b.Detailed+b.Visual==5577,"two annual examples retain total equipment population");
        var floor=InspectionPlanningCalculator.Allocate(3223,90,10,5,AllocationRoundingRule.Floor);
        Check(floor.Close==2900&&floor.Detailed==322&&floor.Visual==1,"floor rounding assigns remainder to Visual");
        var ids=Enumerable.Range(0,500).Select(_=>Guid.NewGuid()).ToArray();
        var sampleOne=InspectionPlanningCalculator.SelectRandom(ids,50,98234);
        var sampleTwo=InspectionPlanningCalculator.SelectRandom(ids.Reverse(),50,98234);
        Check(sampleOne.SequenceEqual(sampleTwo),"sampling is reproducible regardless of source ordering");
        Check(sampleOne.Distinct().Count()==50,"random sample contains no duplicate equipment");
        Check(InspectionPlanningCalculator.SelectRandom(ids,50,98235).Length==50,"different seed retains requested sample size");
        try{InspectionPlanningCalculator.Allocate(10,90,20,0,AllocationRoundingRule.Nearest);Check(false,"allocation total over one hundred rejected");}catch(ArgumentException){Check(true,"allocation total over one hundred rejected");}
        Console.WriteLine($"Inspection planning calculations passed: {checks} checks.");
    }
}
