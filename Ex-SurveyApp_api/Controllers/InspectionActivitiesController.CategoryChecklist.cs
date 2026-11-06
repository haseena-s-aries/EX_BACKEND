using System.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class InspectionActivitiesController
{
    private const string CategoryPlan = "Sample checklist category inspection";
    private static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["Ex 'd' only"] = "Ex 'd' only",
        ["Ex 'e' 'n' only"] = "Ex 'e' 'n' only",
        ["Ex 'n' only"] = "Ex 'n' only",
        ["Ex 'i' only"] = "Ex 'i' only",
        ["Common Checkpoint"] = "Common check point",
        ["Ex 'p' only"] = "Ex 'p'"
    };

    private static string SelectionKey(Guid projectId, Guid? equipmentId = null) => equipmentId.HasValue
        ? $"CategorySelection/{projectId:D}/{equipmentId:D}" : $"CategorySelection/{projectId:D}";

    private IQueryable<ChecklistVersionSnapshot> SavedSelections(Guid projectId, Guid? equipmentId = null) =>
        db.ChecklistVersionSnapshots.AsNoTracking().Where(x => x.ProtectionTypeSnapshot == SelectionKey(projectId, equipmentId))
            .OrderByDescending(x => x.VersionNumber);

    private static string MappingKey(Guid projectId, Guid equipmentId) => $"CategoryMapping/{projectId:D}/{equipmentId:D}";

    private async Task<Dictionary<string, string>> ReadMappings(Guid projectId, Guid? equipmentId, CancellationToken ct)
    {
        if (!equipmentId.HasValue) return new();
        var key = MappingKey(projectId, equipmentId.Value);
        var id = await db.ChecklistVersionSnapshots.Where(x => x.ProtectionTypeSnapshot == key)
            .OrderByDescending(x => x.VersionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (!id.HasValue) return new();
        return await db.CheckpointDefinitionSnapshots.Where(x => x.ChecklistVersionSnapshotId == id)
            .ToDictionaryAsync(x => x.QuestionCode, x => x.ApplicabilityCode!, ct);
    }

    private static SampleChecklistPoint ApplyMapping(SampleChecklistPoint point, IReadOnlyDictionary<string, string> mappings) =>
        mappings.TryGetValue(PointCode(point), out var category) ? point with {ProtectionApplicability = category} : point;

    private async Task<SampleChecklistPoint[]> GetInspectionPoints(Guid? snapshotId, Guid projectId, Guid equipmentId,
        SampleChecklistSource source, CancellationToken ct)
    {
        if (snapshotId.HasValue)
        {
            var selected = await ResolveSelectedPoints(snapshotId.Value, projectId, equipmentId, source, ct);
            if (selected.Length > 0) return selected;
        }
        var catalog = await ReadCatalog(ct);
        var mappings = await ReadMappings(projectId, equipmentId, ct);
        return CatalogPoints(source, catalog).Select(point => ApplyMapping(point, mappings)).ToArray();
    }

    private async Task<SampleChecklistPoint[]> ResolveSelectedPoints(Guid snapshotId, Guid projectId, Guid equipmentId,
        SampleChecklistSource source, CancellationToken ct)
    {
        var codes = (await db.CheckpointDefinitionSnapshots.Where(p => p.ChecklistVersionSnapshotId == snapshotId)
            .Select(p => p.QuestionCode).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var catalog = await ReadCatalog(ct);
        var mappings = await ReadMappings(projectId, equipmentId, ct);
        return CatalogPoints(source, catalog).Where(p => codes.Contains(PointCode(p))).Select(p => ApplyMapping(p, mappings)).ToArray();
    }

    [HttpGet("category-checklist/projects/{projectId:guid}/selection")]
    [Authorize(Roles = "Admin,Surveyor")]
    public async Task<IActionResult> GetCategorySelection(Guid projectId, [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct, Guid? equipmentId = null)
    {
        if (!await CanAccessCategoryProject(authorizer, projectId, ct)) return NotFound();
        if (equipmentId.HasValue && !await CategoryEquipment(projectId, equipmentId.Value).AnyAsync(ct)) return NotFound();
        if (!equipmentId.HasValue && !User.IsInRole(nameof(UserRole.Admin))) return Forbid();
        var source = SampleChecklistImporter.ReadSource();
        var snapshot = await SavedSelections(projectId, equipmentId).FirstOrDefaultAsync(ct);
        var fallback = snapshot ?? (equipmentId.HasValue ? await SavedSelections(projectId).FirstOrDefaultAsync(ct) : null);
        var selectedCodes = fallback is null ? (equipmentId.HasValue ? [] : source.Items.Select(PointCode).ToArray()) :
            await db.CheckpointDefinitionSnapshots.Where(p => p.ChecklistVersionSnapshotId == fallback.Id)
                .OrderBy(p => p.SortOrder).Select(p => p.QuestionCode).ToArrayAsync(ct);
        var mappings = await ReadMappings(projectId, equipmentId, ct);
        var catalog = await ReadCatalog(ct);
        var customCodes = catalog.Checkpoints.Select(PointCode).ToHashSet();
        return Ok(new {points = CatalogPoints(source, catalog).Select(p => PointView(ApplyMapping(p, mappings), customCodes.Contains(PointCode(p)), p.ProtectionApplicability)), selectedCodes, version = snapshot?.VersionNumber ?? 0});
    }

    [HttpPut("category-checklist/projects/{projectId:guid}/selection")]
    [Authorize(Roles = "Admin,Surveyor")]
    public async Task<IActionResult> SaveCategorySelection(Guid projectId, CategoryCheckpointSelection request,
        [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct, Guid? equipmentId = null)
    {
        if (!await CanAccessCategoryProject(authorizer, projectId, ct)) return NotFound();
        if (equipmentId.HasValue && !await CategoryEquipment(projectId, equipmentId.Value).AnyAsync(ct)) return NotFound();
        if (!equipmentId.HasValue && !User.IsInRole(nameof(UserRole.Admin))) return Forbid();
        var source = SampleChecklistImporter.ReadSource();
        var changes = request.CategoryChanges;
        if (changes is {Count: > 0} && !equipmentId.HasValue) return Forbid();
        var catalog = await ReadCatalog(ct);
        var catalogPoints = CatalogPoints(source, catalog);
        var categoryIds = catalog.Categories.ToDictionary(c => c.Id, c => c.Id, StringComparer.Ordinal);
        foreach (var category in catalog.Categories) categoryIds.TryAdd(category.Name, category.Id);
        foreach (var legacy in Categories) categoryIds.TryAdd(legacy.Key, legacy.Value);
        var knownCodes = catalogPoints.Select(PointCode).ToHashSet(StringComparer.Ordinal);
        if (changes is not null && changes.Any(pair => !knownCodes.Contains(pair.Key) || pair.Value is null || !categoryIds.ContainsKey(pair.Value)))
            return BadRequest(new {error = "Choose a valid category for each checkpoint."});
        var selected = request.Codes?.ToHashSet(StringComparer.Ordinal);
        if (selected is null || (selected.Count == 0 && changes is not {Count: > 0}) || selected.Count != request.Codes!.Length ||
            selected.Except(knownCodes).Any())
            return BadRequest(new {error = "Select at least one valid, unique PDF checkpoint."});
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var latest = await SavedSelections(projectId, equipmentId).FirstOrDefaultAsync(ct);
        if (request.Version != (latest?.VersionNumber ?? 0))
            return Conflict(new {error = "The saved checklist changed. Reload the checkpoint selection and try again."});
        var mappings = await ReadMappings(projectId, equipmentId, ct);
        if (changes is {Count: > 0} && equipmentId.HasValue)
        {
            foreach (var (code, category) in changes) mappings[code] = categoryIds[category];
            var mappingSnapshot = new ChecklistVersionSnapshot {
                VersionNumber = request.Version + 1, ProtectionTypeSnapshot = MappingKey(projectId, equipmentId.Value),
                PublishedByUserId = ActorId, PublishedAt = DateTimeOffset.UtcNow,
                SnapshotHash = HashContent(System.Text.Json.JsonSerializer.Serialize(mappings))
            };
            db.ChecklistVersionSnapshots.Add(mappingSnapshot);
            AddCategoryDefinitions(mappingSnapshot.Id, catalogPoints.Where(p => mappings.ContainsKey(PointCode(p))).Select(p => ApplyMapping(p, mappings)));
        }
        var selectedPoints = catalogPoints.Where(p => selected.Contains(PointCode(p))).Select(p => ApplyMapping(p, mappings)).ToArray();
        var snapshot = new ChecklistVersionSnapshot {
            VersionNumber = request.Version + 1, ProtectionTypeSnapshot = SelectionKey(projectId, equipmentId), PublishedByUserId = ActorId,
            PublishedAt = DateTimeOffset.UtcNow, SnapshotHash = HashContent(source.SourceSha256 + "/" + string.Join(",", selectedPoints.Select(p => $"{PointCode(p)}:{p.ProtectionApplicability}"))),
            ControlledSourceReference = $"Sample checklist.pdf; {source.SourceRevision}; SHA256:{source.SourceSha256}",
            StandardReference = "User-supplied checklist sample"
        };
        db.ChecklistVersionSnapshots.Add(snapshot);
        AddCategoryDefinitions(snapshot.Id, selectedPoints);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(new {version = snapshot.VersionNumber});
    }

    private static string PointCode(SampleChecklistPoint point) => $"{point.Section}/{point.ItemNumber}";

    private static object PointView(SampleChecklistPoint point, bool isCustom = false, string? catalogCategoryId = null) =>
        new {point.Section, point.ItemNumber, point.Text, point.ProtectionApplicability, Code = PointCode(point), IsCustom = isCustom, CatalogCategoryId = catalogCategoryId};

    private static string HashContent(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private void AddCategoryDefinitions(Guid snapshotId, IEnumerable<SampleChecklistPoint> points)
    {
        var order = 0;
        foreach (var point in points)
            db.CheckpointDefinitionSnapshots.Add(new CheckpointDefinitionSnapshot {
                ChecklistVersionSnapshotId = snapshotId, QuestionCode = PointCode(point),
                QuestionText = point.Text, SectionCode = point.Section, ApplicabilityCode = point.ProtectionApplicability,
                SortOrder = ++order, ResponseScaleCode = "PassFailNA", IsRequired = true
            });
    }

    [HttpGet("category-checklist/projects/{projectId:guid}/equipment/{equipmentId:guid}")]
    [Authorize(Roles = "Admin,Surveyor")]
    public async Task<IActionResult> CategoryChecklist(Guid projectId, Guid equipmentId, string category, Guid? activityId, [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct)
    {
        var all = category == "All Checkpoints";
        var catalog = await ReadCatalog(ct);
        var mapping = catalog.Categories.FirstOrDefault(c => c.Id == category)?.Id
            ?? catalog.Categories.FirstOrDefault(c => c.Name == category)?.Id
            ?? Categories.GetValueOrDefault(category ?? "");
        if (!all && mapping is null) return BadRequest(new {error = "Select a checkpoint category."});
        if (!await CanAccessCategoryProject(authorizer, projectId, ct) || !await CategoryEquipment(projectId, equipmentId).AnyAsync(ct)) return NotFound();
        var snapshot = await SavedSelections(projectId, equipmentId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct)
            ?? await SavedSelections(projectId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (activityId.HasValue)
        {
            snapshot = await (from activity in db.InspectionActivities.AsNoTracking()
                                  join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                                  where activity.Id == activityId && activity.Grade == null && activity.InspectorUserId == ActorId &&
                                        task.AreaEquipmentId == equipmentId
                                  select (Guid?)activity.ChecklistVersionId).SingleOrDefaultAsync(ct);
            if (!snapshot.HasValue) return NotFound();
        }
        if (activityId.HasValue)
        {
            var points = await db.CheckpointDefinitionSnapshots.AsNoTracking()
                .Where(p => p.ChecklistVersionSnapshotId == snapshot && (all || p.ApplicabilityCode == mapping))
                .OrderBy(p => p.SortOrder).Select(p => new {p.SectionCode, p.QuestionCode, p.QuestionText, p.ApplicabilityCode}).ToListAsync(ct);
            return Ok(points.Select(p => new {
                Section = p.SectionCode, ItemNumber = int.Parse(p.QuestionCode.Split('/')[1]),
                Text = p.QuestionText, Code = p.QuestionCode, ProtectionApplicability = p.ApplicabilityCode
            }));
        }
        var source = SampleChecklistImporter.ReadSource();
        var available = await GetInspectionPoints(snapshot, projectId, equipmentId, source, ct);
        return Ok(available.Where(point => all || point.ProtectionApplicability == mapping).Select(p => PointView(p)));
    }

    [HttpPost("category-checklist/projects/{projectId:guid}/equipment/{equipmentId:guid}/open")]
    [Authorize(Roles = "Admin,Surveyor")]
    public async Task<IActionResult> OpenCategoryChecklist(Guid projectId, Guid equipmentId, CategoryChecklistOpen request, [FromServices] InspectionActivityAuthorizer authorizer, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Conflict(new {error = "Inspection activity workflow is not enabled on this installation."});
        if (request.InspectionDate == default) return BadRequest(new {error = "Select an inspection date."});
        if (!await CanAccessCategoryProject(authorizer, projectId, ct)) return NotFound();
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == ActorId && u.IsActive && u.ApprovalStatus == ApprovalStatus.Approved, ct);
        if (actor is null) return Forbid();
        var start = new DateTimeOffset(request.InspectionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (!await CategoryEquipment(projectId, equipmentId).AnyAsync(ct)) return NotFound();
        var existing = await (from activity in db.InspectionActivities.AsNoTracking()
                              join task in db.InspectionTasks.AsNoTracking() on activity.InspectionTaskId equals task.Id
                              join plan in db.InspectionPlans.AsNoTracking() on task.InspectionPlanId equals plan.Id
                              where activity.Grade == null && activity.InspectorUserId == actor.Id && activity.StartedAt == start &&
                                    task.AreaEquipmentId == equipmentId && plan.ProjectId == projectId && plan.SourceReference == CategoryPlan
                              select activity).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            var hasResponses = await (from response in db.CheckpointResponseRevisions
                                      join revision in db.InspectionActivityRevisions on response.ActivityRevisionId equals revision.Id
                                      where revision.InspectionActivityId == existing.Id
                                      select response.Id).AnyAsync(ct);
            if (existing.ReviewStatus != InspectionActivityReviewStatus.Draft || hasResponses ||
                await db.ActivityEvidence.AnyAsync(e => e.InspectionActivityId == existing.Id, ct))
                return Ok(new {id = existing.Id});
        }

        var source = SampleChecklistImporter.ReadSource();
        var selection = await SavedSelections(projectId, equipmentId).FirstOrDefaultAsync(ct)
            ?? await SavedSelections(projectId).FirstOrDefaultAsync(ct);
        var inspectionPoints = await GetInspectionPoints(selection?.Id, projectId, equipmentId, source, ct);
        var sourceTemplateIds = Enum.GetValues<InspectionType>()
            .Select(grade => SampleChecklistImporter.StableId($"{source.SourceRevision}/{grade}")).ToArray();
        var approvals = await db.ChecklistTemplates.AsNoTracking()
            .Where(t => sourceTemplateIds.Contains(t.Id) && t.LifecycleStatus == ChecklistLifecycleStatus.Published &&
                        t.IsActive && t.ApprovedByUserId.HasValue && t.ApprovedAt.HasValue)
            .OrderByDescending(t => t.ApprovedAt).ToListAsync(ct);
        if (approvals.Count != sourceTemplateIds.Length)
            return existing is null ? Conflict(new {error = "Publish the technically approved Sample Checklist source versions in Checklist Administration before starting a category inspection."}) : Ok(new {id = existing.Id});

        var effectiveHash = HashContent(System.Text.Json.JsonSerializer.Serialize(inspectionPoints, CatalogJson));
        var snapshotId = SampleChecklistImporter.StableId($"category/{selection?.Id}/{source.SourceRevision}/{effectiveHash}");
        if (!await db.ChecklistVersionSnapshots.AnyAsync(s => s.Id == snapshotId, ct))
        {
            db.ChecklistVersionSnapshots.Add(new ChecklistVersionSnapshot {
                Id = snapshotId, VersionNumber = selection?.VersionNumber ?? 1, SnapshotHash = effectiveHash, StandardReference = "User-supplied checklist sample", PublishedByUserId = approvals[0].ApprovedByUserId!.Value,
                PublishedAt = approvals[0].ApprovedAt!.Value, ControlledSourceReference = $"Sample checklist.pdf; {source.SourceRevision}; SHA256:{source.SourceSha256}"
            });
            AddCategoryDefinitions(snapshotId, inspectionPoints);
        }

        if (existing is not null)
        {
            if (existing.ChecklistVersionId != snapshotId)
            {
                var draft = await db.InspectionActivities.SingleAsync(x => x.Id == existing.Id, ct);
                var task = await db.InspectionTasks.SingleAsync(x => x.Id == draft.InspectionTaskId, ct);
                var revision = await db.InspectionActivityRevisions.SingleAsync(x => x.InspectionActivityId == draft.Id, ct);
                draft.ChecklistVersionId = snapshotId;
                task.ChecklistVersionId = snapshotId;
                draft.UpdatedAt = DateTimeOffset.UtcNow;
                revision.ContentHash = HashContent($"{draft.Id:D}|{task.Id:D}||{snapshotId:D}|{start:O}");
                await db.SaveChangesAsync(ct);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(new {id = existing.Id});
        }

        var planRow = new InspectionPlan {ProjectId = projectId, Name = CategoryPlan, SourceReference = CategoryPlan};
        var taskRow = new InspectionTask {InspectionPlanId = planRow.Id, AreaEquipmentId = equipmentId, AssignedUserId = actor.Id,
            ChecklistVersionId = snapshotId, StatusCode = "CategoryChecklist"};
        var activityRow = new InspectionActivity {InspectionTaskId = taskRow.Id, ChecklistVersionId = snapshotId, InspectorUserId = actor.Id,
            InspectorNameSnapshot = actor.DisplayName, StartedAt = start};
        db.InspectionPlans.Add(planRow);
        db.InspectionTasks.Add(taskRow);
        db.InspectionActivities.Add(activityRow);
        db.InspectionActivityRevisions.Add(new InspectionActivityRevision {
            InspectionActivityId = activityRow.Id, CreatedByUserId = actor.Id,
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{activityRow.Id:D}|{taskRow.Id:D}||{snapshotId:D}|{start:O}")))
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Ok(new {id = activityRow.Id});
    }

    private async Task<bool> CanAccessCategoryProject(InspectionActivityAuthorizer authorizer, Guid projectId, CancellationToken ct)
    {
        try
        {
            await authorizer.RequireActorAsync(ActorId, ct, UserRole.Admin, UserRole.Surveyor);
            await authorizer.EnsureProjectScopeAsync(projectId, ct);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
    }

    private IQueryable<AreaEquipment> CategoryEquipment(Guid projectId, Guid equipmentId)
    {
        var projects = new ProjectReportData(db).AccessibleProjects(User);
        return db.AreaEquipment.AsNoTracking().Where(e => e.Id == equipmentId && e.ProjectId == projectId &&
            projects.Any(p => p.Id == e.ProjectId));
    }
}

public sealed record CategoryChecklistOpen(DateOnly InspectionDate);

public sealed record CategoryCheckpointSelection(string[] Codes, int Version, Dictionary<string, string>? CategoryChanges = null);
