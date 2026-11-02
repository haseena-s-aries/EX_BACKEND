using System.Security.Claims;
using System.Security.Cryptography;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Controllers;

public sealed class EquipmentDocumentOptions
{
    public bool Enabled { get; set; }
    public string[] CategoryCodes { get; set; } = ["ExCertificate", "ManufacturerDatasheet", "InstallationMaintenanceManual", "ElectricalDrawing", "InspectionProcedure", "ApprovedTechnicalInstruction", "OtherControlledReference"];
    // Admin is a provisional default; replace with the approved technical reviewer policy.
    public string[] ApprovalRoles { get; set; } = ["Admin"];
    public bool UseAuditEnabled { get; set; }
}

[ApiController, Authorize, Route("api/v1/equipment-documents")]
public sealed class EquipmentReferenceDocumentsController(SurveyDbContext db, IProjectPhotoStorage storage, IOptions<EquipmentDocumentOptions> configured, IOptions<OfflineInspectionOptions> offline) : ControllerBase
{
    private EquipmentDocumentOptions Options => configured.Value;
    private HashSet<string> Categories => new(Options.CategoryCodes, StringComparer.OrdinalIgnoreCase);
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private ProjectReportData Access => new(db);

    [HttpGet("equipment/{equipmentId:guid}")]
    public async Task<IActionResult> List(Guid equipmentId, [FromQuery] string? category, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var equipment = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
        if (equipment is null || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == equipment.ProjectId, ct)) return NotFound();
        var query = db.EquipmentReferenceDocuments.AsNoTracking().Where(x => x.ProjectId == equipment.ProjectId && (x.EquipmentId == equipmentId || x.AreaId == equipment.AreaId || (x.EquipmentId == null && x.AreaId == null)));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.CategoryCode == category);
        var rows = await query.OrderByDescending(x => x.UploadedAt).Select(x => new { x.Id, x.ProjectId, x.AreaId, x.EquipmentId, x.SupersedesDocumentId, x.Title, category = x.CategoryCode, x.DocumentNumber, x.Revision, x.IssueDate, x.ExpiryDate, x.IssuingOrganization, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Sha256, status = x.Status.ToString(), x.UploadedByUserId, x.UploadedAt, x.SubmittedAt, x.ApprovedByUserId, x.ApprovedAt, x.ApprovalComment, x.ArchivedAt, downloadUrl = $"/api/v1/equipment-documents/{x.Id:D}/download" }).ToListAsync(ct);
        return Ok(new { items = rows, categories = Categories.Order(StringComparer.OrdinalIgnoreCase) });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadReferenceDocumentRequest request, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        if (!Categories.Contains(request.CategoryCode ?? "") || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255 || request.File is null)
            return BadRequest(new { error = "Provide a title, supported category and document file." });
        if (request.ExpiryDate.HasValue && request.IssueDate.HasValue && request.ExpiryDate < request.IssueDate) return BadRequest(new { error = "Expiry date cannot precede issue date." });
        var scope = await ResolveScope(request.ProjectId, request.AreaId, request.EquipmentId, ct);
        if (scope is null) return NotFound();
        var resolved = scope.Value;
        var id = Guid.NewGuid();
        StoredProjectPhoto stored;
        try { stored = await storage.SaveReferenceDocumentAsync(request.File, resolved.ProjectId, id, ct); }
        catch (ProjectPhotoValidationException ex) { return BadRequest(new { error = ex.Message }); }
        try
        {
            var content = await storage.OpenReadAsync(stored.RelativePath, ct);
            if (content is null) throw new IOException("Stored document could not be reopened.");
            await using var fileStream = content.Stream;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(fileStream, ct));
            var row = new EquipmentReferenceDocument { Id = id, ProjectId = resolved.ProjectId, AreaId = resolved.AreaId, EquipmentId = resolved.EquipmentId, SupersedesDocumentId = request.SupersedesDocumentId, Title = request.Title.Trim(), CategoryCode = request.CategoryCode!, DocumentNumber = Clean(request.DocumentNumber, 120), Revision = Clean(request.Revision, 100), IssueDate = request.IssueDate, ExpiryDate = request.ExpiryDate, IssuingOrganization = Clean(request.IssuingOrganization, 255), OriginalFileName = stored.OriginalFileName, StoragePath = stored.RelativePath, ContentType = content.ContentType, SizeBytes = request.File.Length, Sha256 = hash, UploadedByUserId = Actor };
            if (row.SupersedesDocumentId.HasValue && !await db.EquipmentReferenceDocuments.AnyAsync(x => x.Id == row.SupersedesDocumentId && x.ProjectId == row.ProjectId && x.EquipmentId == row.EquipmentId && x.AreaId == row.AreaId && x.CategoryCode == row.CategoryCode, ct))
            { await storage.DeleteAsync(stored.RelativePath, ct); return BadRequest(new { error = "Replacement must reference a document in the same scope and category." }); }
            db.EquipmentReferenceDocuments.Add(row); db.EquipmentReferenceDocumentEvents.Add(new EquipmentReferenceDocumentEvent{DocumentId=row.Id,ActorUserId=Actor,EventCode="Uploaded"}); await db.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(Download), new { id = row.Id }, ToView(row));
        }
        catch { await storage.DeleteAsync(stored.RelativePath, ct); throw; }
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var row = await db.EquipmentReferenceDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        var file = await storage.OpenReadAsync(row.StoragePath, ct); if (file is null) return NotFound();
        if (!file.Stream.CanSeek) { await file.Stream.DisposeAsync(); return Conflict(new { error = "Stored file cannot be integrity checked." }); }
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file.Stream, ct));
        if (!actual.Equals(row.Sha256, StringComparison.OrdinalIgnoreCase)) { await file.Stream.DisposeAsync(); return Conflict(new { error = "Stored file checksum does not match its approved metadata." }); }
        file.Stream.Position = 0;
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers.CacheControl = "private, no-store";
        return File(file.Stream, row.ContentType, row.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var doc = await db.EquipmentReferenceDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null || !await Access.AccessibleProjects(User).AnyAsync(x => x.Id == doc.ProjectId, ct)) return NotFound();
        var events = await db.EquipmentReferenceDocumentEvents.AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.OccurredAt).Select(x => new { x.Id, x.EventCode, x.ActorUserId, x.Comments, x.OccurredAt }).ToListAsync(ct);
        var uses = await db.EquipmentDocumentUseEvents.AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.OpenedAt).Select(x => new { x.ActivityId, x.UserId, x.WorkPackageId, x.DocumentRevision, x.Sha256, x.OpenedAt, x.RecordedAt }).ToListAsync(ct);
        var revisions = await db.EquipmentReferenceDocuments.AsNoTracking().Where(x => x.ProjectId == doc.ProjectId && x.AreaId == doc.AreaId && x.EquipmentId == doc.EquipmentId && x.CategoryCode == doc.CategoryCode).OrderBy(x => x.UploadedAt).Select(x => new { x.Id, x.Revision, x.Status, x.UploadedByUserId, x.UploadedAt, x.ApprovedByUserId, x.ApprovedAt, x.Sha256 }).ToListAsync(ct);
        return Ok(new { documentId = id, events, uses, revisions });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var row = await db.EquipmentReferenceDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        if (row.Status != ReferenceDocumentStatus.Draft) return Conflict(new { error = "Only draft documents can be submitted." });
        row.Status = ReferenceDocumentStatus.Submitted; row.SubmittedByUserId = Actor; row.SubmittedAt = DateTimeOffset.UtcNow; db.EquipmentReferenceDocumentEvents.Add(new EquipmentReferenceDocumentEvent{DocumentId=row.Id,ActorUserId=Actor,EventCode="Submitted"}); await db.SaveChangesAsync(ct); return Ok(ToView(row));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize]
    public async Task<IActionResult> Approve(Guid id, DocumentApprovalRequest request, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        if (!Options.ApprovalRoles.Any(User.IsInRole)) return Forbid();
        var row = await db.EquipmentReferenceDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        if (row.Status != ReferenceDocumentStatus.Submitted) return Conflict(new { error = "Only submitted documents can be approved." });
        if (row.UploadedByUserId == Actor) return Forbid();
        if (!string.IsNullOrWhiteSpace(request.Comment) && request.Comment.Length > 2000) return BadRequest(new { error = "Approval comment is too long." });
        var previous = await db.EquipmentReferenceDocuments.Where(x => x.Id != row.Id && x.ProjectId == row.ProjectId && x.AreaId == row.AreaId && x.EquipmentId == row.EquipmentId && x.CategoryCode == row.CategoryCode && x.Status == ReferenceDocumentStatus.Approved).ToListAsync(ct);
        foreach (var old in previous) { old.Status = ReferenceDocumentStatus.Superseded; db.EquipmentReferenceDocumentEvents.Add(new EquipmentReferenceDocumentEvent{DocumentId=old.Id,ActorUserId=Actor,EventCode="Superseded",Comments=$"Replaced by {row.Id:D}"}); }
        row.Status = ReferenceDocumentStatus.Approved; row.ApprovedByUserId = Actor; row.ApprovedAt = DateTimeOffset.UtcNow; row.ApprovalComment = Clean(request.Comment, 2000); db.EquipmentReferenceDocumentEvents.Add(new EquipmentReferenceDocumentEvent{DocumentId=row.Id,ActorUserId=Actor,EventCode="Approved",Comments=row.ApprovalComment}); await db.SaveChangesAsync(ct); return Ok(ToView(row));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var row = await db.EquipmentReferenceDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        if (row.Status is ReferenceDocumentStatus.Superseded or ReferenceDocumentStatus.Archived) return Conflict(new { error = "Document is already superseded or archived." });
        row.Status = ReferenceDocumentStatus.Archived; row.ArchivedAt = DateTimeOffset.UtcNow; db.EquipmentReferenceDocumentEvents.Add(new EquipmentReferenceDocumentEvent{DocumentId=row.Id,ActorUserId=Actor,EventCode="Archived"}); await db.SaveChangesAsync(ct); return Ok(ToView(row));
    }

    [HttpGet("requirement-policies")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RequirementPolicies([FromQuery] Guid? projectId, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        var query = db.EquipmentDocumentRequirementPolicies.AsNoTracking();
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId || x.ProjectId == null);
        var rows = await query.OrderBy(x => x.CategoryCode).ToListAsync(ct);
        foreach (var row in rows) if (row.ProjectId.HasValue && !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        return Ok(rows);
    }

    [HttpPost("requirement-policies")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateRequirementPolicy(RequirementPolicyRequest request, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        if (!Categories.Contains(request.CategoryCode ?? "") || request.ProjectId.HasValue && !await Access.AccessibleProjects(User).AnyAsync(x => x.Id == request.ProjectId, ct)) return BadRequest(new { error = "Choose an authorized project and a supported document category." });
        if (request.EquipmentType?.Length > 100) return BadRequest();
        if (request.WorkPackageId.HasValue && !await db.InspectionWorkPackages.AnyAsync(x => x.Id == request.WorkPackageId && (!request.ProjectId.HasValue || x.ProjectId == request.ProjectId), ct)) return BadRequest(new { error = "Work package is not in the selected project." });
        var row = new EquipmentDocumentRequirementPolicy { ProjectId = request.ProjectId, EquipmentType = Clean(request.EquipmentType, 100), WorkPackageId = request.WorkPackageId, CategoryCode = request.CategoryCode!, IsRequired = request.IsRequired, CreatedByUserId = Actor };
        db.EquipmentDocumentRequirementPolicies.Add(row); await db.SaveChangesAsync(ct); return Ok(row);
    }

    [HttpPost("requirement-policies/{id:guid}/approve")]
    [Authorize]
    public async Task<IActionResult> ApproveRequirementPolicy(Guid id, CancellationToken ct)
    {
        if (!Options.Enabled) return NotFound();
        if (!Options.ApprovalRoles.Any(User.IsInRole)) return Forbid();
        var row = await db.EquipmentDocumentRequirementPolicies.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || row.ProjectId.HasValue && !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == row.ProjectId, ct)) return NotFound();
        if (row.CreatedByUserId == Actor) return Forbid();
        row.IsApproved = true; row.ApprovedByUserId = Actor; row.ApprovedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Ok(row);
    }

    [HttpPost("use-events")]
    [Authorize(Roles = "Surveyor")]
    public async Task<IActionResult> RecordUse(DocumentUseRequest request, CancellationToken ct)
    {
        if (!Options.Enabled || !offline.Value.Enabled) return Conflict(new { acknowledged = false, error = "Document-use synchronization is unavailable; keep the event in the local outbox." });
        if (!Options.UseAuditEnabled) return Conflict(new { acknowledged = false, auditEnabled = false, error = "Server document-use auditing is disabled; event remains pending on the device." });
        if (request.EventId == Guid.Empty || !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return BadRequest();
        var activity = await db.InspectionActivities.SingleOrDefaultAsync(x => x.Id == request.ActivityId, ct);
        var doc = await db.EquipmentReferenceDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DocumentId, ct);
        var project = activity is null ? null : await (from t in db.InspectionTasks join p in db.InspectionPlans on t.InspectionPlanId equals p.Id join e in db.AreaEquipment on t.AreaEquipmentId equals e.Id where t.Id == activity.InspectionTaskId select new { t.AssignedUserId, t.WorkPackageId, p.ProjectId, EquipmentId = e.Id, AreaId = e.AreaId }).SingleOrDefaultAsync(ct);
        if (activity is null || doc is null || project is null || project.AssignedUserId != userId || project.ProjectId != doc.ProjectId || request.WorkPackageId != project.WorkPackageId || doc.EquipmentId.HasValue && doc.EquipmentId != project.EquipmentId || doc.AreaId.HasValue && doc.AreaId != project.AreaId || !await Access.AccessibleProjects(User).AnyAsync(p => p.Id == doc.ProjectId, ct)) return NotFound();
        if ((doc.Revision ?? "Unversioned") != request.Revision || !doc.Sha256.Equals(request.Sha256, StringComparison.OrdinalIgnoreCase)) return Conflict(new { acknowledged = false, error = "Document revision no longer matches; preserve the event and resolve the revision." });
        var existing = await db.EquipmentDocumentUseEvents.SingleOrDefaultAsync(x => x.Id == request.EventId, ct);
        if (existing is not null && (existing.UserId != userId || existing.ActivityId != activity.Id || existing.DocumentId != doc.Id || existing.DocumentRevision != (doc.Revision ?? "Unversioned") || !existing.Sha256.Equals(doc.Sha256, StringComparison.OrdinalIgnoreCase) || existing.OpenedAt != request.OpenedAt)) return Conflict(new { acknowledged = false, error = "This event identifier is already bound to different document-use data." });
        if (existing is null) { db.EquipmentDocumentUseEvents.Add(new EquipmentDocumentUseEvent { Id = request.EventId, ActivityId = activity.Id, UserId = userId, EquipmentId = project.EquipmentId, WorkPackageId = project.WorkPackageId, DocumentId = doc.Id, DocumentRevision = doc.Revision ?? "Unversioned", Sha256 = doc.Sha256, OpenedAt = request.OpenedAt }); await db.SaveChangesAsync(ct); }
        return Ok(new { acknowledged = true, duplicate = existing, eventId = request.EventId });
    }

    private async Task<(Guid ProjectId, Guid? AreaId, Guid? EquipmentId)?> ResolveScope(Guid? projectId, Guid? areaId, Guid? equipmentId, CancellationToken ct)
    {
        if (equipmentId.HasValue)
        {
            var e = await db.AreaEquipment.AsNoTracking().Where(x => x.Id == equipmentId).Select(x => new { x.ProjectId, AreaId = (Guid?)x.AreaId }).SingleOrDefaultAsync(ct);
            if (e is null || projectId.HasValue && projectId != e.ProjectId || areaId.HasValue && areaId != e.AreaId) return null;
            projectId = e.ProjectId; areaId = e.AreaId;
        }
        else if (areaId.HasValue)
        {
            var a = await db.ProjectAreas.AsNoTracking().Where(x => x.Id == areaId).Select(x => new { x.ProjectId }).SingleOrDefaultAsync(ct);
            if (a is null || projectId.HasValue && projectId != a.ProjectId) return null; projectId = a.ProjectId;
        }
        if (!projectId.HasValue || !await Access.AccessibleProjects(User).AnyAsync(x => x.Id == projectId, ct)) return null;
        return (projectId.Value, areaId, equipmentId);
    }
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static object ToView(EquipmentReferenceDocument x) => new { x.Id, x.ProjectId, x.AreaId, x.EquipmentId, x.SupersedesDocumentId, x.Title, category = x.CategoryCode, x.DocumentNumber, x.Revision, x.IssueDate, x.ExpiryDate, x.IssuingOrganization, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Sha256, status = x.Status.ToString(), x.UploadedAt, x.SubmittedAt, x.ApprovedByUserId, x.ApprovedAt, x.ApprovalComment, downloadUrl = $"/api/v1/equipment-documents/{x.Id:D}/download" };
}

public sealed class UploadReferenceDocumentRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? EquipmentId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public string? Title { get; set; }
    public string? CategoryCode { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Revision { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? IssuingOrganization { get; set; }
    public IFormFile? File { get; set; }
}
public sealed record DocumentApprovalRequest(string? Comment);
public sealed record RequirementPolicyRequest(Guid? ProjectId, string? EquipmentType, Guid? WorkPackageId, string CategoryCode, bool IsRequired);
public sealed record DocumentUseRequest(Guid EventId, Guid ActivityId, Guid? WorkPackageId, Guid DocumentId, string? Revision, string Sha256, DateTimeOffset OpenedAt);
