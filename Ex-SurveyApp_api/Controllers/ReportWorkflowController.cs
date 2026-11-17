using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public static class ReportWorkflowLocks
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();
    public static SemaphoreSlim For(Guid projectId) => Locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
}

public sealed record ChangeReportStatus([Required] string Status, long ExpectedVersion, string? Reason = null,
    bool ConfirmSubmission = false, Guid? DraftArtifactId = null);
public sealed record ReopenReportRequest(long ExpectedVersion, [Required, StringLength(2000)] string Reason);

[ApiController]
[Authorize(Roles = "Admin,Manager,Surveyor,Qc,Client")]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/reports/projects/{projectId:guid}/workflow")]
public sealed class ReportWorkflowController(SurveyDbContext db, IProjectPhotoStorage photos,
    IGeneratedReportArtifactStorage archive, ILogger<ReportWorkflowController> logger) : ControllerBase
{
    private ProjectReportData Data => new(db);
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool MayEdit => User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Surveyor");
    private bool MayReopen => User.IsInRole("Admin") || User.IsInRole("Manager");

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        if (!await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        return Ok(await Describe(projectId, ct));
    }

    private async Task<object> Describe(Guid projectId, CancellationToken ct)
    {
        var state = await db.ProjectReportWorkflows.AsNoTracking().SingleOrDefaultAsync(w => w.ProjectId == projectId, ct);
        var history = await (from e in db.ReportWorkflowEvents.AsNoTracking()
            join u in db.Users.AsNoTracking() on e.ActorUserId equals u.Id into users
            from user in users.DefaultIfEmpty()
            where e.ProjectId == projectId orderby e.Version descending
            select new { e.Id, e.FromStatus, e.ToStatus, e.Revision, e.Version, e.Reason, e.ActorUserId,
                ActorName = user == null ? null : user.DisplayName, e.OccurredAt, e.ArtifactId }).ToArrayAsync(ct);
        var draft = await db.GeneratedReportArtifacts.AsNoTracking().Where(a => a.ProjectId == projectId && a.ReportType == "ProjectInspectionReport" && a.ApprovalState == "Draft")
            .OrderByDescending(a => a.Revision).Select(a => new { a.Id, a.FileName, a.Revision, a.GeneratedAt }).FirstOrDefaultAsync(ct);
        var submitter = state?.SubmittedByUserId is {} id ? await db.Users.Where(u => u.Id == id).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) : null;
        Response.Headers.CacheControl = "private, no-store";
        return new { ProjectId = projectId, Status = state?.Status ?? "Ongoing", Revision = state?.Revision ?? 1,
            Version = state?.Version ?? 0, state?.HoldReason, state?.UpdatedAt, state?.UpdatedByUserId,
            state?.SubmittedAt, state?.SubmittedByUserId, SubmittedByName = submitter, state?.SubmittedArtifactId,
            CanEdit = MayEdit && state?.Status != "Submitted", CanReopen = MayReopen && state?.Status == "Submitted",
            DraftArtifact = draft, History = history };
    }

    [HttpPut]
    [Authorize(Roles = "Admin,Manager,Surveyor")]
    public async Task<IActionResult> Change(Guid projectId, ChangeReportStatus request, CancellationToken ct)
    {
        if (!await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        if (!MayEdit) return Forbid();
        if (request.Status is not ("Ongoing" or "Hold" or "Submitted")) return BadRequest(new { message = "Choose Ongoing, Hold or Submitted." });
        var reason = request.Reason?.Trim();
        if (reason?.Length > 2000) return BadRequest(new { message = "Reason allows up to 2,000 characters." });
        if (request.Status == "Hold" && string.IsNullOrWhiteSpace(reason)) return BadRequest(new { message = "A Hold Reason is required." });
        var gate = ReportWorkflowLocks.For(projectId); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            var state = await db.ProjectReportWorkflows.SingleOrDefaultAsync(w => w.ProjectId == projectId, ct);
            if ((state?.Version ?? 0) != request.ExpectedVersion) return Conflict(new { message = "Report Status changed. Refresh before trying again." });
            if (state?.Status == "Submitted") return Conflict(new { message = "The submitted report is locked. An Admin or Manager must open a new revision." });
            if ((state?.Status ?? "Ongoing") == request.Status) return Ok(await Describe(projectId, ct));
            if (state?.Status == "Hold" && request.Status == "Submitted") return BadRequest(new { message = "Resume this held report to Ongoing before submitting." });
            GeneratedReportArtifact? final = null;
            if (request.Status == "Submitted")
            {
                if (!request.ConfirmSubmission) return BadRequest(new { message = "Confirm final report submission before proceeding." });
                var draft = await db.GeneratedReportArtifacts.AsNoTracking().Where(a => a.ProjectId == projectId && a.ReportType == "ProjectInspectionReport" && a.ApprovalState == "Draft").OrderByDescending(a => a.Revision).FirstOrDefaultAsync(ct);
                if (draft is null || !request.DraftArtifactId.HasValue) return BadRequest(new { message = "Generate a project inspection PDF before submitting the report." });
                if (draft.Id != request.DraftArtifactId.Value) return Conflict(new { message = "A newer draft PDF exists. Refresh Report Status and confirm the current version." });
                var lastSubmittedAt = state?.SubmittedAt;
                if (lastSubmittedAt.HasValue && draft.GeneratedAt <= lastSubmittedAt.Value) return BadRequest(new { message = "Generate a new draft PDF for this reopened revision before submitting." });
                if (string.IsNullOrWhiteSpace(draft.UidNumber) || string.IsNullOrWhiteSpace(draft.DocumentNumber) || string.IsNullOrWhiteSpace(draft.DocumentRevision))
                    return BadRequest(new { message = "The draft PDF requires UID Number, Document Number and Revision. Complete Report Document Details and generate it again." });
                var haer = await Data.LoadHaer(projectId, User, ct);
                if (haer is null || haer.Equipment.Count == 0) return BadRequest(new { message = "Add project equipment and complete its inspections before submitting." });
                var pending = haer.Equipment.Where(e => e.InspectionHistory.FirstOrDefault()?.Status is not ("QcApproved" or "Accepted")).ToArray();
                if (pending.Length > 0) return BadRequest(new { message = $"The latest inspection must be QC approved for all equipment. {pending.Length} equipment item(s) still require inspection or QC approval." });
                var report = await Data.Load(projectId, User, ct);
                if (report is null) return NotFound();
                report = report with { UidNumber = draft.UidNumber, DocumentNumber = draft.DocumentNumber, Revision = draft.DocumentRevision };
                // Take a new immutable final snapshot from current data, retaining the approved draft's document details.
                var bytes = await new ProjectReportPdf(photos, logger).Generate(report, ct);
                var next = (await db.GeneratedReportArtifacts.Where(a => a.ProjectId == projectId && a.ReportType == "ProjectInspectionReport" && a.LinkedEntityId == projectId).Select(a => (int?)a.Revision).MaxAsync(ct) ?? 0) + 1;
                var id = Guid.NewGuid(); var name = $"Project_Report_{projectId:N}_r{next}_Submitted.pdf";
                var stored = await archive.SaveAsync(projectId, id, name, bytes, ct);
                final = new GeneratedReportArtifact { Id = id, ProjectId = projectId, LinkedEntityId = projectId, ReportType = "ProjectInspectionReport", ReportNumber = $"Project report {projectId:D}", Revision = next,
                    UidNumber = draft.UidNumber, DocumentNumber = draft.DocumentNumber, DocumentRevision = draft.DocumentRevision,
                    FileName = name, ContentType = "application/pdf", StorageKey = stored.StorageKey, Sha256 = stored.Sha256,
                    GeneratedByUserId = Actor, ApprovalState = "Submitted" };
                db.GeneratedReportArtifacts.Add(final);
            }
            if (state is null) { state = new ProjectReportWorkflow { ProjectId = projectId }; db.ProjectReportWorkflows.Add(state); }
            var from = state.Status; state.Status = request.Status;
            state.HoldReason = request.Status == "Hold" ? reason : null;
            if (final is not null) { state.SubmittedAt = DateTimeOffset.UtcNow; state.SubmittedByUserId = Actor; state.SubmittedArtifactId = final.Id; }
            AddEvent(state, from, reason, final?.Id);
            await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(await Describe(projectId, ct));
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Report Status changed. Refresh before trying again." }); }
        finally { gate.Release(); }
    }

    [HttpPost("reopen")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Reopen(Guid projectId, ReopenReportRequest request, CancellationToken ct)
    {
        if (!MayReopen) return Forbid();
        if (!await Data.AccessibleProjects(User).AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000) return BadRequest(new { message = "Enter a revision reason (up to 2,000 characters)." });
        var gate = ReportWorkflowLocks.For(projectId); await gate.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            var state = await db.ProjectReportWorkflows.SingleOrDefaultAsync(w => w.ProjectId == projectId, ct);
            if (state is null || state.Version != request.ExpectedVersion || state.Status != "Submitted") return Conflict(new { message = "Refresh the submitted report before opening a revision." });
            state.Status = "Ongoing"; state.Revision++; state.HoldReason = null;
            AddEvent(state, "Submitted", request.Reason.Trim(), state.SubmittedArtifactId);
            await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(await Describe(projectId, ct));
        }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Report Status changed. Refresh before trying again." }); }
        finally { gate.Release(); }
    }

    private void AddEvent(ProjectReportWorkflow state, string from, string? reason, Guid? artifactId)
    {
        state.Version++; state.UpdatedAt = DateTimeOffset.UtcNow; state.UpdatedByUserId = Actor;
        db.ReportWorkflowEvents.Add(new ReportWorkflowEvent { ProjectId = state.ProjectId, FromStatus = from, ToStatus = state.Status,
            Revision = state.Revision, Version = state.Version, Reason = reason, ActorUserId = Actor, OccurredAt = state.UpdatedAt, ArtifactId = artifactId });
        db.AuditLogs.Add(new AuditLog { EntityType = "Project", EntityId = state.ProjectId, ActorId = Actor,
            Action = "Report Status Changed", Comments = System.Text.Json.JsonSerializer.Serialize(new { From = from, To = state.Status, state.Revision, state.Version, Reason = reason, ArtifactId = artifactId }) });
    }
}
