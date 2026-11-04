using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.EntityFrameworkCore;

internal static class ChecklistSeedChecks
{
    public static async Task Run()
    {
        await using var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var oldVersion = new ChecklistTemplate { Name = "Default Visual Inspection", InspectionType = InspectionType.Visual, IsActive = false };
        var newVersion = new ChecklistTemplate { Name = oldVersion.Name, InspectionType = InspectionType.Visual, Version = 2 };
        var custom = new ChecklistTemplate { Name = "Custom close checklist", InspectionType = InspectionType.Close };
        var question = new ChecklistQuestion { ChecklistTemplateId = newVersion.Id, Text = "Controlled question", SortOrder = 7, IsRequired = false };
        db.ChecklistTemplates.AddRange(oldVersion, newVersion, custom);
        db.ChecklistQuestions.Add(question);
        await db.SaveChangesAsync();

        await DefaultChecklistSeeder.SeedAsync(db);
        await DefaultChecklistSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();
        if (await db.ChecklistTemplates.CountAsync() != 4 || await db.ChecklistQuestions.CountAsync() != 5)
            throw new Exception("Seeding must tolerate duplicate version names, preserve custom templates, and be repeatable.");
        var preserved = await db.ChecklistQuestions.SingleAsync(x => x.Id == question.Id);
        if (preserved.Text != "Controlled question" || preserved.SortOrder != 7 || preserved.IsRequired ||
            (await db.ChecklistTemplates.SingleAsync(x => x.Id == oldVersion.Id)).IsActive)
            throw new Exception("Seeding modified an existing checklist.");

        await using var empty = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await DefaultChecklistSeeder.SeedAsync(empty);
        await DefaultChecklistSeeder.SeedAsync(empty);
        if (await empty.ChecklistTemplates.CountAsync() != 3 || await empty.ChecklistQuestions.CountAsync() != 12)
            throw new Exception("An empty database must receive the three default checklists exactly once.");
        Console.WriteLine("Checklist seed checks passed: versioned names, existing content, custom templates, fresh database, repeated startup.");
    }
}
