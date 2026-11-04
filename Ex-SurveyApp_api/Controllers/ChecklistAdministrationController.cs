using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Ex_SurveyApp_api.Attributes.EndpointScopeInventory(Ex_SurveyApp_api.Attributes.EndpointScopeKind.TenantResource)]
[Authorize]
[Route("api/v1/checklist-admin")]
public sealed class ChecklistAdministrationController(SurveyDbContext db) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpPost("imports/sample-pdf")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ImportSample(CancellationToken ct)
    {
        var templates = await SampleChecklistImporter.ImportDraftsAsync(db, Actor, ct);
        return Ok(new { message = "Sample checklist imported as drafts. Review and submit each grade for independent technical approval.",
            templates = templates.Select(x => new { x.Id, x.InspectionType, x.Version, x.LifecycleStatus }) });
    }

    [HttpGet("templates")]
    [Authorize(Roles = "Admin,Manager,Qc")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await db.ChecklistTemplates.AsNoTracking().OrderByDescending(x => x.FamilyId).ThenByDescending(x => x.Version).ToListAsync(ct);
        var ids = templates.Select(x => x.Id).ToArray();
        var questions = await db.ChecklistQuestions.AsNoTracking().Where(x => ids.Contains(x.ChecklistTemplateId)).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(templates.Select(x => new {x.Id, x.FamilyId, x.Name, x.InspectionType, x.Version, x.DraftRevision, x.LifecycleStatus, x.IsActive, x.StandardReference, x.StandardEdition, x.ControlledSourceReference, x.ApplicableProtectionTypes, x.CreatedByUserId, x.SubmittedAt, x.SubmittedByUserId, x.ApprovedAt, x.ApprovedByUserId, Questions = questions.Where(q => q.ChecklistTemplateId == x.Id)}));
    }

    [HttpGet("templates/{id:guid}/preview")]
    [Authorize(Roles = "Admin,Manager,Qc")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        var questions = await db.ChecklistQuestions.AsNoTracking().Where(x => x.ChecklistTemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(new {x = template, Sections = new[] {"Apparatus", "Installation", "Environment"}.Select(s => new {Name = s, Questions = questions.Where(q => q.SectionCode == s)})});
    }

    [HttpPost("templates")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create(CreateChecklistRequest request, CancellationToken ct)
    {
        if (!Valid(request.Name, 250) || !Enum.IsDefined(request.Grade) || !Valid(request.StandardReference, 100) || !Valid(request.ApplicableProtectionTypes, 1000)) return BadRequest(new {error = "Template name, inspection grade, standard reference and applicable Ex protection type(s) are required."});
        var template = new ChecklistTemplate {Name = request.Name.Trim(), InspectionType = request.Grade, Version = 1, IsActive = false,
            FamilyId = Guid.NewGuid(), LifecycleStatus = ChecklistLifecycleStatus.Draft, StandardReference = request.StandardReference.Trim(),
            StandardEdition = Trim(request.StandardEdition, 50), ControlledSourceReference = Trim(request.ControlledSourceReference, 500),
            ApplicableProtectionTypes = NormalizeList(request.ApplicableProtectionTypes), CreatedByUserId = Actor};
        db.ChecklistTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Preview), new {id = template.Id}, new {template.Id, template.Name, template.InspectionType, template.Version, template.LifecycleStatus, template.FamilyId});
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Edit(Guid id, EditChecklistRequest request, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        if (template.LifecycleStatus != ChecklistLifecycleStatus.Draft) return Conflict(new {error = "Only a draft checklist can be edited. Clone a published version to create its next draft."});
        if (template.DraftRevision != request.ExpectedRevision) return Conflict(new {error = "Checklist draft changed. Reload before editing."});
        if (!Valid(request.Name, 250) || !Valid(request.StandardReference, 100) || !Valid(request.ApplicableProtectionTypes, 1000) || request.Questions.Count == 0) return BadRequest(new {error = "Provide a name, standard reference, Ex applicability and at least one checkpoint."});
        var old = await db.ChecklistQuestions.Where(x => x.ChecklistTemplateId == id).ToListAsync(ct);
        var oldQuestionIds = old.Select(x => x.Id).ToArray();
        if (await db.SurveySessions.AnyAsync(x => x.ChecklistTemplateId == id, ct) || (oldQuestionIds.Length > 0 && await db.SurveyAnswers.AnyAsync(x => oldQuestionIds.Contains(x.ChecklistQuestionId), ct)))
            return Conflict(new {error = "This draft is referenced by historical surveys and its checkpoint identifiers cannot be replaced. Create a new template from the approved controlled source."});
        var questions = new List<ChecklistQuestion>();
        foreach (var q in request.Questions.OrderBy(x => x.SortOrder))
        {
            if (!ValidSourceMetadata(q, template.InspectionType) || !Valid(q.Text, 4000) || q.SortOrder < 1 || q.SectionCode is not ("Apparatus" or "Installation" or "Environment") || q.ResponseScaleCode is not ("PassFailNA" or "BooleanChecked") ||
                (q.NumericReadingEnabled && (!Valid(q.MeasurementUnit, 40) || (q.MinimumReading.HasValue && q.MaximumReading.HasValue && q.MinimumReading > q.MaximumReading))) ||
                (!q.NumericReadingEnabled && (q.NumericReadingRequired || q.MeasurementUnit is not null || q.MinimumReading.HasValue || q.MaximumReading.HasValue)))
                return BadRequest(new {error = "Checkpoint has invalid section, response scale, measurement unit or limits."});
            questions.Add(new ChecklistQuestion {Id = q.SourceItemReference is null ? Guid.NewGuid() : old.FirstOrDefault(x => x.SourceItemReference == q.SourceItemReference)?.Id ?? Guid.NewGuid(), ChecklistTemplateId = id, Text = q.Text.Trim(), SortOrder = q.SortOrder, IsRequired = q.IsRequired,
                SourceItemReference = q.SourceItemReference, SourceItemNumber = q.SourceItemNumber, ApplicableGrades = q.ApplicableGrades, SubChecksJson = q.SubChecksJson,
                SectionCode = q.SectionCode, ApplicableProtectionTypes = Trim(q.ApplicableProtectionTypes, 1000) is { } types ? NormalizeList(types) : null,
                RequireRemarkOnFail = q.RequireRemarkOnFail, ResponseScaleCode = q.ResponseScaleCode, NumericReadingEnabled = q.NumericReadingEnabled,
                MeasurementUnit = q.NumericReadingEnabled ? q.MeasurementUnit!.Trim() : null, MinimumReading = q.NumericReadingEnabled ? q.MinimumReading : null,
                MaximumReading = q.NumericReadingEnabled ? q.MaximumReading : null, NumericReadingRequired = q.NumericReadingEnabled && q.NumericReadingRequired});
        }
        var sourceReferences = questions.Where(x => x.SourceItemReference != null).Select(x => x.SourceItemReference).ToArray();
        if (sourceReferences.Distinct().Count() != sourceReferences.Length) return BadRequest(new {error = "Source item references must be unique within a checklist version."});
        if (questions.Select(x => x.SortOrder).Distinct().Count() != questions.Count) return BadRequest(new {error = "Checkpoint order values must be unique."});
        template.Name = request.Name.Trim(); template.StandardReference = request.StandardReference.Trim(); template.StandardEdition = Trim(request.StandardEdition, 50);
        template.ControlledSourceReference = Trim(request.ControlledSourceReference, 500); template.ApplicableProtectionTypes = NormalizeList(request.ApplicableProtectionTypes);
        var retainedIds = questions.Select(x => x.Id).ToHashSet();
        db.ChecklistQuestions.RemoveRange(old.Where(x => !retainedIds.Contains(x.Id)));
        foreach (var question in questions)
        {
            var original = old.FirstOrDefault(x => x.Id == question.Id);
            if (original is null) db.ChecklistQuestions.Add(question);
            else db.Entry(original).CurrentValues.SetValues(question);
        }
        template.DraftRevision = checked(template.DraftRevision + 1);
        db.AuditLogs.Add(new AuditLog {EntityType = "ChecklistTemplate", EntityId = id, ActorId = Actor, Action = "Checklist Draft Edited", Comments = $"Draft version {template.Version}; {questions.Count} checkpoint(s)."});
        await db.SaveChangesAsync(ct);
        return Ok(new {template.Id, template.Name, template.Version, template.DraftRevision, template.LifecycleStatus, QuestionCount = questions.Count});
    }

    [HttpPost("templates/{id:guid}/clone")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        var source = await db.ChecklistTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (source is null) return NotFound();
        if (source.LifecycleStatus != ChecklistLifecycleStatus.Published) return Conflict(new {error = "Only a published checklist can be cloned into its next version."});
        var questions = await db.ChecklistQuestions.AsNoTracking().Where(x => x.ChecklistTemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var clone = new ChecklistTemplate {Name = source.Name, InspectionType = source.InspectionType, Version = source.Version + 1, FamilyId = source.FamilyId ?? source.Id,
            IsActive = false, LifecycleStatus = ChecklistLifecycleStatus.Draft, StandardReference = source.StandardReference, StandardEdition = source.StandardEdition,
            ControlledSourceReference = source.ControlledSourceReference, ApplicableProtectionTypes = source.ApplicableProtectionTypes, CreatedByUserId = Actor};
        db.ChecklistTemplates.Add(clone);
        foreach (var q in questions) db.ChecklistQuestions.Add(new ChecklistQuestion {ChecklistTemplateId = clone.Id, Text = q.Text, SortOrder = q.SortOrder, IsRequired = q.IsRequired,
            SourceItemReference = q.SourceItemReference, SourceItemNumber = q.SourceItemNumber, ApplicableGrades = q.ApplicableGrades, SubChecksJson = q.SubChecksJson,
                SectionCode = q.SectionCode, ApplicableProtectionTypes = q.ApplicableProtectionTypes, RequireRemarkOnFail = q.RequireRemarkOnFail, ResponseScaleCode = q.ResponseScaleCode,
            NumericReadingEnabled = q.NumericReadingEnabled, MeasurementUnit = q.MeasurementUnit, MinimumReading = q.MinimumReading, MaximumReading = q.MaximumReading, NumericReadingRequired = q.NumericReadingRequired});
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Preview), new {id = clone.Id}, new {clone.Id, clone.FamilyId, clone.Version, clone.LifecycleStatus});
    }

    [HttpPost("templates/{id:guid}/submit")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        if (template.LifecycleStatus != ChecklistLifecycleStatus.Draft) return Conflict(new {error = "Only a draft can be submitted for technical approval."});
        if (string.IsNullOrWhiteSpace(template.ControlledSourceReference) || string.IsNullOrWhiteSpace(template.StandardReference) || string.IsNullOrWhiteSpace(template.ApplicableProtectionTypes) || !await db.ChecklistQuestions.AnyAsync(x => x.ChecklistTemplateId == id, ct)) return BadRequest(new {error = "A controlled source reference, standard, Ex applicability and checkpoints are required before submission."});
        template.LifecycleStatus = ChecklistLifecycleStatus.PendingTechnicalApproval; template.SubmittedAt = DateTimeOffset.UtcNow; template.SubmittedByUserId = Actor;
        db.AuditLogs.Add(new AuditLog {EntityType = "ChecklistTemplate", EntityId = id, ActorId = Actor, Action = "Checklist Submitted for Technical Approval"});
        await db.SaveChangesAsync(ct);
        return Ok(new {template.Id, template.LifecycleStatus, template.SubmittedAt});
    }

    [HttpPost("templates/{id:guid}/approve")]
    [Authorize(Roles = "Qc,Admin")]
    public async Task<IActionResult> Approve(Guid id, ChecklistApprovalRequest request, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        if (template.LifecycleStatus != ChecklistLifecycleStatus.PendingTechnicalApproval) return Conflict(new {error = "Checklist is not awaiting technical approval."});
        if (template.SubmittedByUserId == Actor) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Comments)) return BadRequest(new {error = "Technical approval comments are required."});
        template.LifecycleStatus = ChecklistLifecycleStatus.Published; template.IsActive = true; template.ApprovedAt = DateTimeOffset.UtcNow; template.ApprovedByUserId = Actor;
        var family = template.FamilyId ?? template.Id;
        var previous = await db.ChecklistTemplates.Where(x => x.Id != id && (x.FamilyId == family || (x.FamilyId == null && x.Id == family)) && x.LifecycleStatus == ChecklistLifecycleStatus.Published).ToListAsync(ct);
        foreach (var old in previous) {old.LifecycleStatus = ChecklistLifecycleStatus.Archived; old.IsActive = false;}
        db.AuditLogs.Add(new AuditLog {EntityType = "ChecklistTemplate", EntityId = id, ActorId = Actor, Action = "Checklist Technically Approved", Comments = request.Comments.Trim()});
        await db.SaveChangesAsync(ct);
        return Ok(new {template.Id, template.Version, template.LifecycleStatus, template.ApprovedAt, template.ApprovedByUserId});
    }

    [HttpPost("templates/{id:guid}/archive")]
    [Authorize(Roles = "Admin,Qc")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var template = await db.ChecklistTemplates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        if (template.LifecycleStatus != ChecklistLifecycleStatus.Published) return Conflict(new {error = "Only a published version can be archived."});
        template.LifecycleStatus = ChecklistLifecycleStatus.Archived; template.IsActive = false;
        db.AuditLogs.Add(new AuditLog {EntityType = "ChecklistTemplate", EntityId = id, ActorId = Actor, Action = "Checklist Archived", Comments = "Historical inspection snapshots are retained."});
        await db.SaveChangesAsync(ct);
        return Ok(new {template.Id, template.LifecycleStatus});
    }

    private static bool ValidSourceMetadata(ChecklistQuestionInput q, InspectionType grade)
    {
        if (q.SourceItemReference?.Length > 200 || q.SourceItemNumber is <= 0 ||
            (q.SourceItemReference != null && q.SourceItemNumber is null) || q.ApplicableGrades?.Length > 100 || q.SubChecksJson?.Length > 4000)
            return false;
        if (q.ApplicableGrades != null)
        {
            var grades = q.ApplicableGrades.Split(',');
            if (!grades.Contains(grade.ToString()) || grades.Any(x => !Enum.TryParse<InspectionType>(x, out var value) || !Enum.IsDefined(value))) return false;
        }
        if (q.SubChecksJson != null)
        {
            try
            {
                var checks = System.Text.Json.JsonSerializer.Deserialize<SampleSubCheck[]>(q.SubChecksJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                if (checks is null || checks.Any(x => string.IsNullOrWhiteSpace(x.Label) || x.ApplicableGrades is null || x.ApplicableGrades.Any(g => !Enum.TryParse<InspectionType>(g, out var value) || !Enum.IsDefined(value)))) return false;
            }
            catch (System.Text.Json.JsonException) { return false; }
        }
        return true;
    }

    private static bool Valid(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : throw new ArgumentException($"Field exceeds {max} characters.");
    private static string NormalizeList(string value) => string.Join(",", value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase));
}

public sealed record CreateChecklistRequest(string Name, InspectionType Grade, string StandardReference, string? StandardEdition, string? ControlledSourceReference, string ApplicableProtectionTypes);
public sealed record EditChecklistRequest(int ExpectedRevision, string Name, string StandardReference, string? StandardEdition, string? ControlledSourceReference, string ApplicableProtectionTypes, IReadOnlyList<ChecklistQuestionInput> Questions);
public sealed record ChecklistQuestionInput(string Text, int SortOrder, string SectionCode, bool IsRequired, string ResponseScaleCode, string? ApplicableProtectionTypes, bool RequireRemarkOnFail, bool NumericReadingEnabled, string? MeasurementUnit, decimal? MinimumReading, decimal? MaximumReading, bool NumericReadingRequired, string? SourceItemReference = null, int? SourceItemNumber = null, string? ApplicableGrades = null, string? SubChecksJson = null);
public sealed record ChecklistApprovalRequest(string Comments);
