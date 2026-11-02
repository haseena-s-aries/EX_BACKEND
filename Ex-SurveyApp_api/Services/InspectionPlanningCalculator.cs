using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Services;

public static class InspectionPlanningCalculator
{
    public static int Round(decimal value, AllocationRoundingRule rule) => rule switch
    {
        AllocationRoundingRule.Ceiling => (int)decimal.Ceiling(value),
        AllocationRoundingRule.Floor => (int)decimal.Floor(value),
        AllocationRoundingRule.Nearest => (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero),
        _ => throw new ArgumentException("Explicit rounding requires a manually supplied count.")
    };

    public static (int Close, int Detailed, int Visual) Allocate(int count, decimal closePercent, decimal detailedPercent, decimal minimumDetailedCreditPercent, AllocationRoundingRule rounding)
    {
        if (count < 0 || closePercent < 0 || detailedPercent < 0 || minimumDetailedCreditPercent < 0 || closePercent + detailedPercent > 100 || detailedPercent > 100 || minimumDetailedCreditPercent > 100)
            throw new ArgumentException("Allocation values must be valid percentages totaling no more than 100%.");
        var detailed = Math.Max(Round(count * detailedPercent / 100m, rounding), Round(count * minimumDetailedCreditPercent / 100m, rounding));
        var close = Round(count * closePercent / 100m, rounding);
        if (close + detailed > count) throw new ArgumentException("Rounded Close and Detailed counts exceed the annual equipment list.");
        return (close, detailed, count - close - detailed);
    }

    public static Guid[] SelectRandom(IEnumerable<Guid> ids, int count, int seed)
    {
        var random = new Random(seed);
        var ordered = ids.OrderBy(x => x).ToArray();
        if (count < 0 || count > ordered.Length) throw new ArgumentOutOfRangeException(nameof(count));
        for (var i = ordered.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
        }
        return ordered.Take(count).ToArray();
    }
}
