using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public static class DefaultChecklistSeeder
{
    public static async Task SeedAsync(SurveyDbContext db)
    {
        var checklistSeed = new Dictionary<InspectionType, string[]>
        {
            [InspectionType.Visual] =
            [
                "Enclosure is intact with no cracks or damage",
                "Gaskets are in good condition",
                "No excessive corrosion on enclosure",
                "Certification and rating plate is legible"
            ],
            [InspectionType.Close] =
            [
                "All bolts and fasteners are tight",
                "Cable entries are properly sealed",
                "Covers and lids properly fitted",
                "Grounding/bonding connections intact"
            ],
            [InspectionType.Detailed] =
            [
                "No unauthorized modifications",
                "Flame paths clean and undamaged",
                "No excessive paint buildup",
                "Internal components properly mounted"
            ]
        };

        foreach (var (inspectionType, questions) in checklistSeed)
        {
            var templateName = $"Default {inspectionType} Inspection";
            // Names are shared by checklist versions. Never rewrite existing controlled
            // or historical questions, or reactivate an inactive version during startup.
            if (await db.ChecklistTemplates.AnyAsync(x => x.InspectionType == inspectionType &&
                    (x.Name == templateName || x.IsActive)))
                continue;

            var template = new ChecklistTemplate { Name = templateName, InspectionType = inspectionType };
            db.ChecklistTemplates.Add(template);
            for (var index = 0; index < questions.Length; index++)
                db.ChecklistQuestions.Add(new ChecklistQuestion
                {
                    ChecklistTemplateId = template.Id,
                    Text = questions[index],
                    SortOrder = index + 1,
                    IsRequired = true
                });
        }

        await db.SaveChangesAsync();
    }
}
