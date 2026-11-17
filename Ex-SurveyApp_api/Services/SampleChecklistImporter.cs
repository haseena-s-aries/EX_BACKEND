using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

public sealed record SampleSubCheck(string Label, string[] ApplicableGrades);
public sealed record SampleChecklistPoint(string Section, int ItemNumber, string Text,
    string ProtectionApplicability, string[] ApplicableGrades, SampleSubCheck[] SubChecks);
public sealed record SampleChecklistSource(string SourceRevision, string SourceSha256, SampleChecklistPoint[] Items);

public static class SampleChecklistImporter
{
    public static string ControlledSourceReference(SampleChecklistSource source) =>
        $"Sample checklist.pdf; {source.SourceRevision}; SHA256:{source.SourceSha256}";

    public static SampleChecklistSource ReadSource()
    {
        using var stream = typeof(SampleChecklistImporter).Assembly.GetManifestResourceStream("ExSurvey.SampleChecklist")
            ?? throw new InvalidOperationException("Bundled sample checklist is missing.");
        return JsonSerializer.Deserialize<SampleChecklistSource>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    public static async Task<IReadOnlyList<ChecklistTemplate>> ImportDraftsAsync(
        SurveyDbContext db, Guid actor, CancellationToken ct = default)
    {
        var source = ReadSource();
        var result = new List<ChecklistTemplate>();
        foreach (var grade in Enum.GetValues<InspectionType>())
        {
            var templateId = StableId($"{source.SourceRevision}/{grade}");
            var existing = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == templateId, ct);
            if (existing is not null) { result.Add(existing); continue; }
            // Continue the default family so technical approval retires only its previous version.
            // Never edit the previous template, question text or historical answers during import.
            var previous = await db.ChecklistTemplates.Where(x => x.InspectionType == grade && x.Name.StartsWith("Default "))
                .OrderByDescending(x => x.Version).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
            var family = previous?.FamilyId ?? previous?.Id ?? templateId;
            var maxVersion = await db.ChecklistTemplates.Where(x => x.Id == family || x.FamilyId == family)
                .Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
            var template = new ChecklistTemplate
            {
                Id = templateId, FamilyId = family, Name = $"Sample PDF - {grade} Inspection",
                InspectionType = grade, Version = maxVersion + 1, IsActive = false,
                LifecycleStatus = ChecklistLifecycleStatus.Draft, CreatedByUserId = actor,
                StandardReference = "User-supplied sample; not verified against a current standard",
                ControlledSourceReference = ControlledSourceReference(source),
                ApplicableProtectionTypes = "d,e,n,i,p"
            };
            db.ChecklistTemplates.Add(template);
            foreach (var point in source.Items.Where(x => x.ApplicableGrades.Contains(grade.ToString())))
            {
                var reference = $"{source.SourceRevision}/{point.Section}/{point.ItemNumber}";
                var sectionOrder = point.Section switch { "Apparatus" => 1, "Installation" => 2, "Environment" => 3, _ => throw new InvalidOperationException("Unmapped source section.") };
                db.ChecklistQuestions.Add(new ChecklistQuestion
                {
                    Id = StableId($"{reference}/{grade}"), ChecklistTemplateId = template.Id,
                    Text = point.Text, SortOrder = sectionOrder * 100 + point.ItemNumber,
                    SectionCode = point.Section, SourceItemNumber = point.ItemNumber, SourceItemReference = reference,
                    ApplicableProtectionTypes = point.ProtectionApplicability,
                    ApplicableGrades = string.Join(',', point.ApplicableGrades),
                    SubChecksJson = point.SubChecks.Length == 0 ? null : JsonSerializer.Serialize(point.SubChecks, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    ResponseScaleCode = "BooleanChecked", IsRequired = true
                });
            }
            db.AuditLogs.Add(new AuditLog { ActorId = actor, EntityType = "ChecklistTemplate", EntityId = template.Id,
                Action = "Sample Checklist Imported", Comments = $"{source.SourceRevision}; draft only; technical review required." });
            result.Add(template);
        }
        await db.SaveChangesAsync(ct);
        return result;
    }
}
