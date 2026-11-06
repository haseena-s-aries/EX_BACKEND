using System.Security.Claims;
using System.Security.Cryptography;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Surveyor))]
[EndpointScopeInventory(EndpointScopeKind.TenantResource)]
[Route("api/surveyor")]
public class SurveyorController(
    SurveyDbContext db,
    IProjectPhotoStorage photoStorage,
    ILogger<SurveyorController> logger) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<SurveyorDashboardResponse>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var projectCounts = await db.Projects.AsNoTracking()
            .Where(x => x.AssignedSurveyorId == CurrentUserId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                MyProjectCount = group.Count(),
                InProgress = group.Count(x => x.Status == ProjectStatus.InProgress),
                OnHold = group.Count(x => x.Status == ProjectStatus.OnHold),
                NotStarted = group.Count(x => x.Status == ProjectStatus.Pending)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var reviewRows = await (
            from inspection in db.SurveySessions.AsNoTracking()
            join equipment in db.AreaEquipment.AsNoTracking()
                on inspection.AreaEquipmentId equals equipment.Id
            join project in db.Projects.AsNoTracking()
                on equipment.ProjectId equals project.Id
            join review in db.QcReviews.AsNoTracking()
                on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            where project.AssignedSurveyorId == CurrentUserId && inspection.SubmittedAt != null
            select new { ReviewStatus = (QcReviewStatus?)review.Status })
            .ToListAsync(cancellationToken);

        var feedback = await (
            from review in db.QcReviews.AsNoTracking()
            join inspection in db.SurveySessions.AsNoTracking()
                on review.SurveySessionId equals inspection.Id
            join equipment in db.AreaEquipment.AsNoTracking()
                on inspection.AreaEquipmentId equals equipment.Id
            join project in db.Projects.AsNoTracking()
                on equipment.ProjectId equals project.Id
            join area in db.ProjectAreas.AsNoTracking()
                on equipment.AreaId equals area.Id
            where project.AssignedSurveyorId == CurrentUserId
                  && review.Status == QcReviewStatus.ChangesRequested
            orderby review.ReviewedAt descending, equipment.SequenceNo
            select new SurveyorQcFeedbackResponse(
                project.Id,
                area.Id,
                equipment.Id,
                project.ProjectName,
                area.AreaName,
                equipment.SequenceNo,
                equipment.TagNumber,
                equipment.Type,
                review.Comments ?? string.Empty,
                review.Severity,
                review.ReviewedAt))
            .ToListAsync(cancellationToken);

        return Ok(new SurveyorDashboardResponse(
            projectCounts?.MyProjectCount ?? 0,
            projectCounts?.InProgress ?? 0,
            projectCounts?.OnHold ?? 0,
            projectCounts?.NotStarted ?? 0,
            reviewRows.Count(x => x.ReviewStatus is null or QcReviewStatus.PendingReview),
            reviewRows.Count(x => x.ReviewStatus == QcReviewStatus.ChangesRequested),
            reviewRows.Count(x => x.ReviewStatus == QcReviewStatus.Approved),
            feedback));
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<SurveyorProjectSummaryResponse>>> GetProjects(
        CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(x => x.AssignedSurveyorId == CurrentUserId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new SurveyorProjectSummaryResponse(
                x.Id, x.ProjectName, x.Sector, x.JobNo, x.Location, x.VesselOrSiteName,
                x.CreatedAt, x.Status, x.Areas.Count))
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpGet("projects/{projectId:guid}")]
    public async Task<ActionResult<SurveyorProjectDetailsResponse>> GetProject(
        Guid projectId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var project = await db.Projects.AsNoTracking()
            .Include(x => x.Areas)
                .ThenInclude(x => x.Photos)
            .Include(x => x.Areas)
                .ThenInclude(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == projectId, cancellationToken);

        var userIds = new Guid?[] { project.AssignedSurveyorId, project.AssignedQcId, project.AssignedClientId };
        var names = await db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        return Ok(new SurveyorProjectDetailsResponse(
            project.Id, project.ProjectName, project.Sector, project.JobNo, project.Location,
            project.VesselOrSiteName, project.CreatedAt, project.Status,
            names.GetValueOrDefault(project.AssignedSurveyorId, string.Empty),
            names.GetValueOrDefault(project.AssignedQcId, string.Empty),
            project.AssignedClientId.HasValue ? names.GetValueOrDefault(project.AssignedClientId.Value, string.Empty) : "Not assigned",
            project.Areas.OrderBy(x => x.CreatedAt).Select(x => ToAreaSummary(projectId, x)).ToArray()));
    }

    [HttpGet("projects/{projectId:guid}/areas")]
    public async Task<ActionResult<IEnumerable<SurveyorAreaSummaryResponse>>> GetAreas(
        Guid projectId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var areas = await db.ProjectAreas.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.Photos)
            .Include(x => x.EquipmentPins)
            .AsSplitQuery()
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(areas.Select(x => ToAreaSummary(projectId, x)));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}")]
    public async Task<ActionResult<SurveyorAreaDetailsResponse>> GetArea(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);

        var area = await db.ProjectAreas.AsNoTracking()
            .Where(x => x.Id == areaId && x.ProjectId == projectId)
            .Include(x => x.Photos)
            .Include(x => x.EquipmentPins)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (area is null)
            return NotFound(new { error = "Project area was not found." });

        var reviews = await LoadEquipmentReviews(
            area.EquipmentPins.Select(x => x.Id).ToArray(), cancellationToken);
        return Ok(new SurveyorAreaDetailsResponse(
            area.Id,
            area.AreaName,
            GetLayoutImagePath(projectId, area),
            area.EquipmentPins.OrderBy(x => x.SequenceNo)
                .Select(x => ToEquipmentResponse(x, reviews.GetValueOrDefault(x.Id))).ToArray()));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}/equipment")]
    public async Task<ActionResult<IEnumerable<AreaEquipmentResponse>>> GetEquipment(
        Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var equipment = await db.AreaEquipment.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.AreaId == areaId)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync(cancellationToken);
        var reviews = await LoadEquipmentReviews(equipment.Select(x => x.Id).ToArray(), cancellationToken);
        return Ok(equipment.Select(x => ToEquipmentResponse(x, reviews.GetValueOrDefault(x.Id))));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}/equipment/resolve")]
    public async Task<IActionResult> ResolveEquipmentInArea(
        Guid projectId, Guid areaId, [FromQuery] string identifier,
        [FromQuery] string technology = "MANUAL", CancellationToken cancellationToken = default)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed) return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { state = "unknown", message = "This area was not found in the assigned job." });
        var value = identifier?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
            return BadRequest(new { state = "invalid", message = "Enter a valid equipment identifier." });

        IQueryable<AreaEquipment> query = db.AreaEquipment.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.AreaId == areaId);
        if (technology is "HF_RFID" or "NFC")
        {
            var normalized = new string(value.Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '-')
                .ToArray()).ToUpperInvariant();
            if (normalized.Length is < 4 or > 128 || !normalized.All(Uri.IsHexDigit))
                return BadRequest(new { state = "invalid", message = "The RFID/NFC identifier format is invalid." });
            var ids = await query.Where(x => x.RfidTagUid != null &&
                x.RfidTagUid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper() == normalized).Select(x => x.Id).Take(2).ToListAsync(cancellationToken);
            if (ids.Count == 0)
                ids = await (from tag in db.EquipmentTagAssignments.AsNoTracking()
                    join item in query on tag.EquipmentId equals item.Id
                    where tag.IsActive && tag.NormalizedIdentifier == normalized &&
                          (tag.Technology == "HF_RFID" || tag.Technology == "NFC")
                    select item.Id).Distinct().Take(2).ToListAsync(cancellationToken);
            return await BuildScopedResolveResult(ids, projectId, areaId, cancellationToken);
        }
        if (technology == "QR")
        {
            var ids = await (from tag in db.EquipmentTagAssignments.AsNoTracking()
                join item in query on tag.EquipmentId equals item.Id
                where tag.IsActive && tag.Technology == "QR" && tag.NormalizedIdentifier == value
                select item.Id).Distinct().Take(2).ToListAsync(cancellationToken);
            return await BuildScopedResolveResult(ids, projectId, areaId, cancellationToken);
        }
        if (technology != "MANUAL")
            return BadRequest(new { state = "invalid", message = "Choose HF_RFID, NFC, QR, or manual search." });
        var matches = await query.Where(x => x.TagNumber == value || x.EquipmentCode == value)
            .Select(x => x.Id).Take(2).ToListAsync(cancellationToken);
        return await BuildScopedResolveResult(matches, projectId, areaId, cancellationToken);
    }

    private async Task<IActionResult> BuildScopedResolveResult(
        List<Guid> ids, Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        if (ids.Count > 1)
            return Conflict(new { state = "duplicate", message = "More than one equipment item matches. Do not select automatically." });
        if (ids.Count == 0)
            return NotFound(new { state = "unknown", message = "No equipment matches this identifier in the selected job area. You may register it as new equipment." });
        var item = await db.AreaEquipment.AsNoTracking()
            .Where(x => x.Id == ids[0] && x.ProjectId == projectId && x.AreaId == areaId)
            .Select(x => new { x.Id, x.ProjectId, x.AreaId, x.TagNumber, equipmentId = x.EquipmentCode,
                x.Type, x.Description, x.Manufacturer, x.Model, x.SerialNumber, x.Location,
                x.ExProtectionType, x.RfidTagUid })
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? NotFound(new { state = "unknown", message = "No equipment matches this identifier." }) : Ok(new { state = "matched", equipment = item });
    }

    [HttpPost("projects/{projectId:guid}/areas/{areaId:guid}/equipment/scan-events")]
    public async Task<IActionResult> RecordEquipmentScanEvent(
        Guid projectId, Guid areaId, RecordEquipmentScanEventRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed) return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });
        if (request.ClientEventId == Guid.Empty || string.IsNullOrWhiteSpace(request.Identifier) ||
            request.Technology is not ("HF_RFID" or "NFC" or "QR" or "MANUAL") ||
            request.MatchResult is not ("matched" or "unknown" or "duplicate" or "invalid"))
            return BadRequest(new { error = "A stable event ID, normalized identifier, supported technology, and match result are required." });
        if (request.EquipmentId.HasValue && !await db.AreaEquipment.AsNoTracking().AnyAsync(
                x => x.Id == request.EquipmentId.Value && x.ProjectId == projectId && x.AreaId == areaId,
                cancellationToken))
            return BadRequest(new { error = "The equipment ID is not part of this job area." });
        var scannedAt = request.ScannedAt ?? DateTimeOffset.UtcNow;
        if (scannedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return BadRequest(new { error = "Scan timestamp cannot be in the future." });
        var existing = await db.EquipmentScanEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClientEventId == request.ClientEventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ProjectId != projectId || existing.AreaId != areaId || existing.ActorUserId != CurrentUserId ||
                existing.Identifier != request.Identifier.Trim() || existing.Technology != request.Technology)
                return Conflict(new { error = "This event ID was already used for a different scan." });
            return Ok(new { existing.Id, existing.ClientEventId, duplicate = true });
        }
        var row = new EquipmentScanEvent
        {
            ClientEventId = request.ClientEventId,
            ProjectId = projectId,
            AreaId = areaId,
            EquipmentId = request.EquipmentId,
            ActorUserId = CurrentUserId,
            Identifier = request.Identifier.Trim(),
            Technology = request.Technology,
            MatchResult = request.MatchResult,
            Discrepancy = TrimOrNull(request.Discrepancy),
            ScannedAt = scannedAt,
            RecordedAt = DateTimeOffset.UtcNow
        };
        db.EquipmentScanEvents.Add(row);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        { return Conflict(new { error = "This scan event was already recorded." }); }
        return StatusCode(StatusCodes.Status201Created, new { row.Id, row.ClientEventId, row.ProjectId, row.AreaId, row.EquipmentId, row.Technology, row.MatchResult, row.ScannedAt, row.RecordedAt });
    }

    [HttpPost("projects/{projectId:guid}/areas/{areaId:guid}/equipment")]
    public async Task<ActionResult<AreaEquipmentResponse>> CreateEquipment(
        Guid projectId, Guid areaId, UpsertAreaEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return validationError;

        var tagNumber = request.TagNumber.Trim();
        if (request.ClientEquipmentId == Guid.Empty)
            return BadRequest(new { error = "Client equipment ID cannot be empty." });
        if (request.ClientEquipmentId is Guid clientEquipmentId)
        {
            var prior = await db.AreaEquipment.SingleOrDefaultAsync(x => x.Id == clientEquipmentId, cancellationToken);
            if (prior is not null)
            {
                if (!MatchesClientRegistration(prior, request, projectId, areaId, tagNumber, NormalizeRfidTagUid(request.RfidTagUid)))
                    return Conflict(new { error = "This local equipment ID was already used for a different registration. Local data was not changed." });
                return Ok(ToEquipmentResponse(prior));
            }
        }
        if (await db.AreaEquipment.AnyAsync(
                x => x.AreaId == areaId && x.TagNumber == tagNumber, cancellationToken))
            return Conflict(new { error = "TagNumber already exists in this area." });

        var rfidTagUid = NormalizeRfidTagUid(request.RfidTagUid);
        var rfidTechnology = string.IsNullOrWhiteSpace(request.RfidIdentificationMethod)
            ? "HF_RFID"
            : request.RfidIdentificationMethod.Trim().ToUpperInvariant();
        if (rfidTagUid is not null && rfidTechnology is not ("HF_RFID" or "NFC"))
            return BadRequest(new { error = "Initial equipment tags must use HF_RFID or NFC. QR assignments use the tag management workflow." });
        if (rfidTagUid is not null && (rfidTagUid.Length is < 4 or > 128 || !rfidTagUid.All(Uri.IsHexDigit)))
            return BadRequest(new { error = "RFID/NFC UID must contain 4 to 128 hexadecimal characters (separators are allowed)." });
        if (rfidTagUid is not null && await db.AreaEquipment.AnyAsync(
                x => x.RfidTagUid == rfidTagUid, cancellationToken))
            return Conflict(new { error = "RFID tag UID is already assigned to another asset." });

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var nextSequenceNo = (await db.AreaEquipment
            .Where(x => x.AreaId == areaId)
            .MaxAsync(x => (int?)x.SequenceNo, cancellationToken) ?? 0) + 1;
        var now = DateTimeOffset.UtcNow;
        var equipment = new AreaEquipment
        {
            Id = request.ClientEquipmentId ?? Guid.NewGuid(),
            ProjectId = projectId,
            AreaId = areaId,
            SequenceNo = nextSequenceNo,
            XPercent = request.XPercent,
            YPercent = request.YPercent,
            IsPlacedOnLayout = request.IsPlacedOnLayout,
            TagNumber = tagNumber,
            EquipmentCode = TrimOrNull(request.EquipmentId),
            RfidTagUid = rfidTagUid,
            Type = request.Type.Trim(),
            CircuitId = TrimOrNull(request.CircuitId),
            Location = TrimOrNull(request.Location),
            Manufacturer = TrimOrNull(request.Manufacturer),
            ProtectionDetails = TrimOrNull(request.ProtectionDetails),
            CertificateNumber = TrimOrNull(request.CertificateNumber),
            Description = TrimOrNull(request.Description),
            Model = TrimOrNull(request.Model),
            SerialNumber = TrimOrNull(request.SerialNumber),
            ExProtectionType = TrimOrNull(request.ExProtectionType),
            GasGroup = TrimOrNull(request.GasGroup),
            TemperatureClass = TrimOrNull(request.TemperatureClass),
            HazardousAreaZone = TrimOrNull(request.HazardousAreaZone),
            AmbientTemperatureRange = TrimOrNull(request.AmbientTemperatureRange),
            IpRating = TrimOrNull(request.IpRating),
            NotifiedBody = TrimOrNull(request.NotifiedBody),
            SpecialConditionsOfUse = TrimOrNull(request.SpecialConditionsOfUse),
            RfidIdentificationMethod = rfidTagUid is null ? null : rfidTechnology,
            RegistrationStatus = "Submitted",
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.AreaEquipment.Add(equipment);
        db.AuditLogs.Add(new AuditLog { EntityType = "AreaEquipment", EntityId = equipment.Id,
            ActorId = CurrentUserId, Action = "Equipment Registration Submitted", Comments = "New field equipment registration submitted for independent verification." });
        if(rfidTagUid is not null)
        {
            var registeredAt=DateTimeOffset.UtcNow;
            db.EquipmentTagAssignments.Add(new EquipmentTagAssignment{EquipmentId=equipment.Id,ProjectId=projectId,Technology=rfidTechnology,NormalizedIdentifier=rfidTagUid,AssignedByUserId=CurrentUserId,AssignedAt=registeredAt,AssignmentReason="Registered with the new field equipment record."});
            db.EquipmentTagHistory.Add(new EquipmentTagHistory{EquipmentId=equipment.Id,ProjectId=projectId,Technology=rfidTechnology,NormalizedIdentifier=rfidTagUid,ActionCode="Assigned",Reason="Registered with the new field equipment record.",ActorUserId=CurrentUserId,OccurredAt=registeredAt});
        }
        var project = await TouchProject(projectId, cancellationToken);
        if (project.Status == ProjectStatus.Pending)
            project.Status = ProjectStatus.InProgress;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = rfidTagUid is null
                ? "TagNumber already exists in this area."
                : "Tag number or RFID tag UID is already assigned to another asset." });
        }

        return StatusCode(StatusCodes.Status201Created, ToEquipmentResponse(equipment));
    }

    [HttpPut("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}")]
    public async Task<ActionResult<AreaEquipmentResponse>> UpdateEquipment(
        Guid projectId, Guid areaId, Guid equipmentId, UpsertAreaEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return validationError;

        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(
            x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId,
            cancellationToken);
        if (equipment is null)
            return NotFound(new { error = "Equipment pin was not found." });

        var tagNumber = request.TagNumber.Trim();
        if (await db.AreaEquipment.AnyAsync(
                x => x.AreaId == areaId && x.Id != equipmentId && x.TagNumber == tagNumber,
                cancellationToken))
            return Conflict(new { error = "TagNumber already exists in this area." });

        var rfidTagUid = request.RfidTagUid is null?equipment.RfidTagUid:NormalizeRfidTagUid(request.RfidTagUid);
        if(request.RfidTagUid is not null&&!string.Equals(rfidTagUid,equipment.RfidTagUid,StringComparison.OrdinalIgnoreCase))
            return Conflict(new {error="RFID tag replacement or retirement must use the equipment tag management workflow so the change is confirmed and recorded."});
        if (rfidTagUid is not null && await db.AreaEquipment.AnyAsync(
                x => x.Id != equipmentId && x.RfidTagUid == rfidTagUid, cancellationToken))
            return Conflict(new { error = "RFID tag UID is already assigned to another asset." });

        equipment.XPercent = request.XPercent;
        equipment.YPercent = request.YPercent;
        equipment.IsPlacedOnLayout = request.IsPlacedOnLayout;
        equipment.TagNumber = tagNumber;
        equipment.EquipmentCode = TrimOrNull(request.EquipmentId);
        equipment.RfidTagUid = rfidTagUid;
        equipment.Type = request.Type.Trim();
        equipment.CircuitId = TrimOrNull(request.CircuitId);
        equipment.Location = TrimOrNull(request.Location);
        equipment.Manufacturer = TrimOrNull(request.Manufacturer);
        equipment.ProtectionDetails = TrimOrNull(request.ProtectionDetails);
        equipment.CertificateNumber = TrimOrNull(request.CertificateNumber);
        equipment.Description = TrimOrNull(request.Description);
        equipment.Model = TrimOrNull(request.Model);
        equipment.SerialNumber = TrimOrNull(request.SerialNumber);
        equipment.ExProtectionType = TrimOrNull(request.ExProtectionType);
        equipment.GasGroup = TrimOrNull(request.GasGroup);
        equipment.TemperatureClass = TrimOrNull(request.TemperatureClass);
        equipment.HazardousAreaZone = TrimOrNull(request.HazardousAreaZone);
        equipment.AmbientTemperatureRange = TrimOrNull(request.AmbientTemperatureRange);
        equipment.IpRating = TrimOrNull(request.IpRating);
        equipment.NotifiedBody = TrimOrNull(request.NotifiedBody);
        equipment.SpecialConditionsOfUse = TrimOrNull(request.SpecialConditionsOfUse);
        equipment.RfidIdentificationMethod = equipment.RfidTagUid is null ? null : equipment.RfidIdentificationMethod;
        equipment.RegistrationStatus = "Submitted";
        equipment.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog { EntityType = "AreaEquipment", EntityId = equipment.Id,
            ActorId = CurrentUserId, Action = "Equipment Registration Updated",
            Comments = "Equipment master data changed and resubmitted for independent verification." });
        await TouchProject(projectId, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { error = rfidTagUid is null
                ? "TagNumber already exists in this area."
                : "Tag number or RFID tag UID is already assigned to another asset." });
        }

        return Ok(ToEquipmentResponse(equipment));
    }

    [HttpDelete("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}")]
    public async Task<IActionResult> DeleteEquipment(
        Guid projectId, Guid areaId, Guid equipmentId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed)
            return ProjectAccessError(access);
        if (!await AreaExists(projectId, areaId, cancellationToken))
            return NotFound(new { error = "Project area was not found." });

        var equipment = await db.AreaEquipment.SingleOrDefaultAsync(
            x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId,
            cancellationToken);
        if (equipment is null)
            return NotFound(new { error = "Equipment pin was not found." });

        if (await db.Findings.AnyAsync(x => x.RegisterEquipmentId == equipmentId, cancellationToken))
            return Conflict(new { error = "This equipment has linked defects and cannot be deleted." });

        db.AreaEquipment.Remove(equipment);
        await TouchProject(projectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}/photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(220_000_000)]
    public async Task<IActionResult> UploadEquipmentPhotos(
        Guid projectId, Guid areaId, Guid equipmentId, [FromForm] UploadEquipmentPhotosRequest request,
        CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed) return ProjectAccessError(access);
        var item = await db.AreaEquipment.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId, cancellationToken);
        if (item is null) return NotFound(new { error = "Equipment was not found in this assigned job area." });
        var role = request.Role?.Trim();
        if (role is not ("Overview" or "Nameplate" or "ExMarking" or "Installation" or "Additional"))
            return BadRequest(new { error = "Choose Overview, Nameplate, ExMarking, Installation, or Additional." });
        if (request.Files.Count == 0) return BadRequest(new { error = "Select at least one equipment photograph." });
        if (request.Files.Count > photoStorage.MaxFilesPerRequest)
            return BadRequest(new { error = $"A maximum of {photoStorage.MaxFilesPerRequest} photos can be uploaded at once." });
        if (request.ClientPhotoId == Guid.Empty)
            return BadRequest(new { error = "Client photo ID cannot be empty." });
        if (request.ClientPhotoId is Guid clientPhotoId)
        {
            var prior = await db.Files.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientPhotoId, cancellationToken);
            if (prior is not null)
            {
                if (request.Files.Count != 1 || prior.ProjectId != projectId || prior.AreaEquipmentId != equipmentId ||
                    prior.EquipmentPhotoRole != role || prior.UploadedById != CurrentUserId ||
                    !string.Equals(prior.FileName, Path.GetFileName(request.Files[0].FileName.Replace('\\', '/')), StringComparison.Ordinal) ||
                    !string.Equals(prior.ContentType, request.Files[0].ContentType, StringComparison.OrdinalIgnoreCase) ||
                    !await EquipmentPhotoContentMatchesAsync(prior, request.Files[0], cancellationToken))
                    return Conflict(new { error = "This local photo ID was already used for different evidence. Local data was not changed." });
                return Ok(new[] { new { prior.Id, prior.AreaEquipmentId, prior.EquipmentPhotoRole, prior.FileName, prior.ContentType, prior.UploadedById, prior.UploadedAt,
                    url = $"/api/surveyor/projects/{projectId:D}/areas/{areaId:D}/equipment/{equipmentId:D}/photos/{prior.Id:D}" } });
            }
        }

        var storedPaths = new List<string>();
        var rows = new List<SurveyFile>();
        try
        {
            foreach (var file in request.Files)
            {
                var stored = await photoStorage.SaveAsync(file, projectId, areaId, cancellationToken);
                storedPaths.Add(stored.RelativePath);
                rows.Add(new SurveyFile
                {
                    Id = request.ClientPhotoId ?? Guid.NewGuid(),
                    ProjectId = projectId, AreaEquipmentId = equipmentId,
                    Category = FileCategory.EquipmentPhoto, EquipmentPhotoRole = role,
                    FileName = stored.OriginalFileName, StorageKey = stored.RelativePath,
                    ContentType = file.ContentType, UploadedById = CurrentUserId,
                    UploadedAt = DateTimeOffset.UtcNow
                });
            }
            db.Files.AddRange(rows);
            db.AuditLogs.Add(new AuditLog { EntityType = "AreaEquipment", EntityId = equipmentId,
                ActorId = CurrentUserId, Action = "Equipment Photos Added", Comments = $"{rows.Count} {role} photo(s) uploaded." });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ProjectPhotoValidationException ex)
        {
            foreach (var path in storedPaths) await photoStorage.DeleteAsync(path, cancellationToken);
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            foreach (var path in storedPaths) await photoStorage.DeleteAsync(path, cancellationToken);
            throw;
        }
        return StatusCode(StatusCodes.Status201Created, rows.Select(x => new
        {
            x.Id, x.AreaEquipmentId, x.EquipmentPhotoRole, x.FileName, x.ContentType, x.UploadedById, x.UploadedAt,
            url = $"/api/surveyor/projects/{projectId:D}/areas/{areaId:D}/equipment/{equipmentId:D}/photos/{x.Id:D}"
        }));
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}/photos")]
    public async Task<IActionResult> GetEquipmentPhotos(
        Guid projectId, Guid areaId, Guid equipmentId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed) return ProjectAccessError(access);
        if (!await db.AreaEquipment.AsNoTracking().AnyAsync(
                x => x.Id == equipmentId && x.ProjectId == projectId && x.AreaId == areaId, cancellationToken))
            return NotFound(new { error = "Equipment was not found in this assigned job area." });
        var rows = await db.Files.AsNoTracking().Where(x => x.ProjectId == projectId &&
                x.AreaEquipmentId == equipmentId && x.Category == FileCategory.EquipmentPhoto)
            .OrderBy(x => x.UploadedAt).Select(x => new { x.Id, x.FileName, x.ContentType, x.EquipmentPhotoRole,
                x.UploadedById, x.UploadedAt,
                url = $"/api/surveyor/projects/{projectId:D}/areas/{areaId:D}/equipment/{equipmentId:D}/photos/{x.Id:D}" })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("projects/{projectId:guid}/areas/{areaId:guid}/equipment/{equipmentId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DownloadEquipmentPhoto(
        Guid projectId, Guid areaId, Guid equipmentId, Guid photoId, CancellationToken cancellationToken)
    {
        var access = await CheckProjectAccess(projectId, cancellationToken);
        if (access != ProjectAccess.Allowed) return ProjectAccessError(access);
        var photo = await db.Files.AsNoTracking().SingleOrDefaultAsync(x => x.Id == photoId &&
            x.ProjectId == projectId && x.AreaEquipmentId == equipmentId &&
            x.Category == FileCategory.EquipmentPhoto, cancellationToken);
        if (photo is null) return NotFound(new { error = "Equipment photo was not found." });
        var content = await photoStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        return content is null ? NotFound(new { error = "Equipment photo file was not found." }) :
            File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("equipment/{equipmentId:guid}")]
    public async Task<ActionResult<SurveyorEquipmentDetailsResponse>> GetEquipmentDetails(
        Guid equipmentId, CancellationToken cancellationToken)
    {
        var context = await GetEquipmentContext(equipmentId, tracking: false, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Equipment was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();

        var inspection = await GetInspectionInfo(equipmentId, cancellationToken);
        return Ok(ToEquipmentDetailsResponse(context, inspection));
    }

    [HttpGet("equipment/by-rfid/{rfidTagUid}")]
    public async Task<ActionResult<SurveyorEquipmentDetailsResponse>> GetEquipmentByRfid(
        string rfidTagUid, CancellationToken cancellationToken)
    {
        var normalizedUid = NormalizeRfidTagUid(rfidTagUid);
        if (normalizedUid is null)
            return NotFound(new { error = "No equipment is linked to this RFID tag." });

        var equipmentId = await db.AreaEquipment.AsNoTracking()
            .Where(x => x.RfidTagUid == normalizedUid)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!equipmentId.HasValue)
            equipmentId = await db.EquipmentTagAssignments.AsNoTracking()
                .Where(x => x.IsActive && (x.Technology == "HF_RFID" || x.Technology == "NFC") &&
                    x.NormalizedIdentifier == normalizedUid)
                .Select(x => (Guid?)x.EquipmentId)
                .SingleOrDefaultAsync(cancellationToken);
        if (!equipmentId.HasValue)
            return NotFound(new { error = "No equipment is linked to this RFID tag." });

        var context = await GetEquipmentContext(equipmentId.Value, tracking: false, cancellationToken);
        if (context is null || context.Project.AssignedSurveyorId != CurrentUserId)
            return NotFound(new { error = "No equipment is linked to this RFID tag." });

        var inspection = await GetInspectionInfo(equipmentId.Value, cancellationToken);
        return Ok(ToEquipmentDetailsResponse(context, inspection));
    }

    [HttpGet("equipment/{equipmentId:guid}/inspection")]
    public async Task<ActionResult<SurveyorEquipmentInspectionResponse>> GetInspection(
        Guid equipmentId, CancellationToken cancellationToken)
    {
        var context = await GetEquipmentContext(equipmentId, tracking: false, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Equipment was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();

        var inspection = await db.SurveySessions.AsNoTracking().SingleOrDefaultAsync(
            x => x.AreaEquipmentId == equipmentId, cancellationToken);
        if (inspection is null)
            return NotFound(new { error = "Inspection has not been started for this equipment." });

        return Ok(await BuildInspectionResponse(inspection, cancellationToken));
    }

    [HttpPost("equipment/{equipmentId:guid}/inspection")]
    public async Task<ActionResult<SurveyorEquipmentInspectionResponse>> CreateInspection(
        Guid equipmentId, SaveEquipmentInspectionRequest request, CancellationToken cancellationToken)
    {
        var context = await GetEquipmentContext(equipmentId, tracking: false, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Equipment was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();
        if (await db.SurveySessions.AnyAsync(x => x.AreaEquipmentId == equipmentId, cancellationToken))
            return Conflict(new { error = "An inspection already exists for this equipment." });

        var validation = await ValidateInspectionRequest(request, cancellationToken);
        if (validation.Error is not null)
            return validation.Error;

        var now = DateTimeOffset.UtcNow;
        var inspection = new SurveySession
        {
            AreaEquipmentId = equipmentId,
            SurveyorId = CurrentUserId,
            InspectorName = request.InspectorName.Trim(),
            SurveyorRemarks = TrimOrNull(request.Comments),
            InspectionTimestamp = request.InspectionDate!.Value,
            Status = request.IsCompleted ? SurveyStatus.Submitted : SurveyStatus.Draft,
            SubmittedAt = request.IsCompleted ? now : null,
            DueDate = request.IsCompleted ? now.AddDays(7) : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.SurveySessions.Add(inspection);
        var currentQuestionIds = validation.QuestionIds!;
        var requestedAnswers = request.Answers.ToDictionary(x => x.ChecklistItemId);
        db.SurveyAnswers.AddRange(currentQuestionIds.Select(questionId =>
        {
            requestedAnswers.TryGetValue(questionId, out var answer);
            return new SurveyAnswer
            {
                SurveySessionId = inspection.Id, ChecklistQuestionId = questionId,
                Value = (answer?.IsChecked ?? false).ToString(), Comment = TrimOrNull(answer?.Remarks)
            };
        }));
        if (request.IsCompleted)
            QcWorkflowHistory.Record(db, inspection.Id, CurrentUserId, "Surveyor Submitted", comments: inspection.SurveyorRemarks);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            await BuildInspectionResponse(inspection, cancellationToken));
    }

    [HttpPut("inspections/{inspectionId:guid}")]
    public async Task<ActionResult<SurveyorEquipmentInspectionResponse>> UpdateInspection(
        Guid inspectionId, SaveEquipmentInspectionRequest request, CancellationToken cancellationToken)
    {
        var context = await GetInspectionContext(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();

        var review = await db.QcReviews.SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (IsInspectionLocked(context.Inspection, review))
            return Conflict(new { error = "This inspection is QC approved and locked." });

        var validation = await ValidateInspectionRequest(request, cancellationToken, inspectionId);
        if (validation.Error is not null)
            return validation.Error;

        var resubmission = context.Inspection.Status == SurveyStatus.Returned || review?.Status == QcReviewStatus.ChangesRequested;
        var firstSubmission = context.Inspection.SubmittedAt == null;
        var now = DateTimeOffset.UtcNow;
        context.Inspection.InspectorName = request.InspectorName.Trim();
        context.Inspection.SurveyorRemarks = TrimOrNull(request.Comments);
        context.Inspection.InspectionTimestamp = request.InspectionDate!.Value;
        context.Inspection.Status = request.IsCompleted ? SurveyStatus.Submitted : SurveyStatus.Draft;
        context.Inspection.SubmittedAt = request.IsCompleted ? now : null;
        context.Inspection.DueDate = request.IsCompleted ? now.AddDays(7) : null;
        context.Inspection.UpdatedAt = now;

        var answers = await db.SurveyAnswers
            .Where(x => x.SurveySessionId == inspectionId)
            .ToListAsync(cancellationToken);
        var requestedIds = request.Answers.Select(x => x.ChecklistItemId).ToHashSet();
        db.SurveyAnswers.RemoveRange(answers.Where(x => !requestedIds.Contains(x.ChecklistQuestionId)));
        var answerByQuestion = answers.ToDictionary(x => x.ChecklistQuestionId);
        foreach (var answerRequest in request.Answers)
        {
            if (!answerByQuestion.TryGetValue(answerRequest.ChecklistItemId, out var answer))
            {
                answer = new SurveyAnswer
                {
                    SurveySessionId = inspectionId,
                    ChecklistQuestionId = answerRequest.ChecklistItemId
                };
                db.SurveyAnswers.Add(answer);
            }
            answer.Value = answerRequest.IsChecked.ToString();
            answer.Comment = TrimOrNull(answerRequest.Remarks);
        }

        if (request.IsCompleted && review?.Status == QcReviewStatus.ChangesRequested)
        {
            review.Status = QcReviewStatus.PendingReview;
            review.StartedAt = null;
            review.ReviewedAt = null;
        }

        if (request.IsCompleted && (firstSubmission || resubmission))
            QcWorkflowHistory.Record(db, inspectionId, CurrentUserId, resubmission ? "Surveyor Resubmitted" : "Surveyor Submitted", comments: context.Inspection.SurveyorRemarks);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildInspectionResponse(context.Inspection, cancellationToken));
    }

    [HttpGet("checklist")]
    public async Task<ActionResult<IEnumerable<SurveyorChecklistItemResponse>>> GetChecklist(
        CancellationToken cancellationToken)
    {
        var items = await (
            from question in db.ChecklistQuestions.AsNoTracking()
            join template in db.ChecklistTemplates.AsNoTracking()
                on question.ChecklistTemplateId equals template.Id
            where template.IsActive && template.LifecycleStatus == ChecklistLifecycleStatus.Published
            orderby template.InspectionType, question.SortOrder
            select new SurveyorChecklistItemResponse(
                question.Id, template.InspectionType, question.Text, question.SortOrder, question.IsRequired,
                question.SectionCode, question.ApplicableProtectionTypes, question.SourceItemNumber,
                question.SourceItemReference, question.ApplicableGrades, question.SubChecksJson))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("checklist/sample-preview")]
    public async Task<IActionResult> GetSampleChecklistPreview(CancellationToken cancellationToken)
    {
        var source = SampleChecklistImporter.ReadSource();
        var ids = Enum.GetValues<InspectionType>()
            .Select(grade => SampleChecklistImporter.StableId($"{source.SourceRevision}/{grade}")).ToArray();
        var templates = await db.ChecklistTemplates.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new {x.IsActive, x.LifecycleStatus}).ToListAsync(cancellationToken);
        var items = Enum.GetValues<InspectionType>().SelectMany(grade => source.Items
            .Where(point => point.ApplicableGrades.Contains(grade.ToString()))
            .Select(point => new SurveyorChecklistItemResponse(
                SampleChecklistImporter.StableId($"{source.SourceRevision}/{point.Section}/{point.ItemNumber}/{grade}"),
                grade, point.Text, (point.Section == "Apparatus" ? 100 : point.Section == "Installation" ? 200 : 300) + point.ItemNumber,
                true, point.Section, point.ProtectionApplicability, point.ItemNumber,
                $"{source.SourceRevision}/{point.Section}/{point.ItemNumber}", string.Join(',', point.ApplicableGrades),
                point.SubChecks.Length == 0 ? null : System.Text.Json.JsonSerializer.Serialize(point.SubChecks,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))).ToArray();
        return Ok(new {source.SourceRevision, pointCount = source.Items.Length,
            isPublished = templates.Count == 3 && templates.All(x => x.IsActive && x.LifecycleStatus == ChecklistLifecycleStatus.Published), items});
    }

    [HttpPost("inspections/{inspectionId:guid}/photos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IEnumerable<SurveyorInspectionPhotoResponse>>> UploadInspectionPhotos(
        Guid inspectionId, [FromForm] UploadInspectionPhotosRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetInspectionContext(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();
        var review = await db.QcReviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (IsInspectionLocked(context.Inspection, review))
            return Conflict(new { error = "This inspection is QC approved and locked." });
        if (request.Files.Count == 0)
            return BadRequest(new { error = "Select at least one inspection photo." });
        if (request.Files.Count > photoStorage.MaxFilesPerRequest)
            return BadRequest(new { error = $"A maximum of {photoStorage.MaxFilesPerRequest} photos can be uploaded at once." });

        var storedPaths = new List<string>();
        var photos = new List<SurveyFile>();
        try
        {
            foreach (var file in request.Files)
            {
                var stored = await photoStorage.SaveInspectionAsync(
                    file, context.Project.Id, inspectionId, cancellationToken);
                storedPaths.Add(stored.RelativePath);
                photos.Add(new SurveyFile
                {
                    SurveySessionId = inspectionId,
                    Category = FileCategory.EquipmentPhoto,
                    FileName = stored.OriginalFileName,
                    StorageKey = stored.RelativePath,
                    ContentType = file.ContentType,
                    UploadedById = CurrentUserId
                });
            }

            db.Files.AddRange(photos);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ProjectPhotoValidationException ex)
        {
            await DeleteStoredFiles(storedPaths);
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            await DeleteStoredFiles(storedPaths);
            throw;
        }

        return StatusCode(StatusCodes.Status201Created,
            photos.Select(ToInspectionPhotoResponse).ToArray());
    }

    [HttpGet("inspections/{inspectionId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DownloadInspectionPhoto(
        Guid inspectionId, Guid photoId, CancellationToken cancellationToken)
    {
        var context = await GetInspectionContext(inspectionId, cancellationToken, tracking: false);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();
        var photo = await db.Files.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == photoId && x.SurveySessionId == inspectionId &&
                 x.Category == FileCategory.EquipmentPhoto, cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Inspection photo was not found." });
        var content = await photoStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
        return content is null
            ? NotFound(new { error = "Inspection photo file was not found." })
            : File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("inspections/{inspectionId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DeleteInspectionPhoto(
        Guid inspectionId, Guid photoId, CancellationToken cancellationToken)
    {
        var context = await GetInspectionContext(inspectionId, cancellationToken);
        if (context is null)
            return NotFound(new { error = "Inspection was not found." });
        if (context.Project.AssignedSurveyorId != CurrentUserId)
            return ProjectForbidden();
        var review = await db.QcReviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.SurveySessionId == inspectionId, cancellationToken);
        if (IsInspectionLocked(context.Inspection, review))
            return Conflict(new { error = "This inspection is QC approved and locked." });

        var photo = await db.Files.SingleOrDefaultAsync(
            x => x.Id == photoId && x.SurveySessionId == inspectionId &&
                 x.Category == FileCategory.EquipmentPhoto, cancellationToken);
        if (photo is null)
            return NotFound(new { error = "Inspection photo was not found." });
        db.Files.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await photoStorage.DeleteAsync(photo.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete stored inspection photo {PhotoId}", photoId);
        }
        return NoContent();
    }

    private async Task<EquipmentContext?> GetEquipmentContext(
        Guid equipmentId, bool tracking, CancellationToken cancellationToken)
    {
        var equipment = tracking ? db.AreaEquipment : db.AreaEquipment.AsNoTracking();
        var areas = tracking ? db.ProjectAreas : db.ProjectAreas.AsNoTracking();
        var projects = tracking ? db.Projects : db.Projects.AsNoTracking();
        return await (
            from pin in equipment
            join area in areas on pin.AreaId equals area.Id
            join project in projects on pin.ProjectId equals project.Id
            where pin.Id == equipmentId
            select new EquipmentContext(pin, area, project))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<InspectionContext?> GetInspectionContext(
        Guid inspectionId, CancellationToken cancellationToken, bool tracking = true)
    {
        var inspections = tracking ? db.SurveySessions : db.SurveySessions.AsNoTracking();
        var equipment = tracking ? db.AreaEquipment : db.AreaEquipment.AsNoTracking();
        var projects = tracking ? db.Projects : db.Projects.AsNoTracking();
        return await (
            from inspection in inspections
            join pin in equipment on inspection.AreaEquipmentId equals pin.Id
            join project in projects on pin.ProjectId equals project.Id
            where inspection.Id == inspectionId
            select new InspectionContext(inspection, pin, project))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<InspectionInfo?> GetInspectionInfo(
        Guid equipmentId, CancellationToken cancellationToken) =>
        await (
            from inspection in db.SurveySessions.AsNoTracking()
            where inspection.AreaEquipmentId == equipmentId
            join review in db.QcReviews.AsNoTracking()
                on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            select new InspectionInfo(
                inspection.Id,
                inspection.Status,
                review == null ? null : review.Status,
                review == null ? null : review.Comments,
                review == null ? null : review.Severity))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<ChecklistQuestion> CurrentChecklistQuestions() =>
        from question in db.ChecklistQuestions.AsNoTracking()
        join template in db.ChecklistTemplates.AsNoTracking() on question.ChecklistTemplateId equals template.Id
        where template.IsActive && template.LifecycleStatus == ChecklistLifecycleStatus.Published
        select question;

    private async Task<SurveyorEquipmentInspectionResponse> BuildInspectionResponse(
        SurveySession inspection, CancellationToken cancellationToken)
    {
        // Stored answer references pin the original set, including unchecked items and archived versions.
        var savedAnswers = await db.SurveyAnswers.AsNoTracking()
            .Where(x => x.SurveySessionId == inspection.Id)
            .ToDictionaryAsync(x => x.ChecklistQuestionId, cancellationToken);
        var ids = savedAnswers.Keys.ToArray();
        var checklist = await (
            from question in db.ChecklistQuestions.AsNoTracking()
            join template in db.ChecklistTemplates.AsNoTracking()
                on question.ChecklistTemplateId equals template.Id
            where ids.Contains(question.Id)
            orderby template.InspectionType, question.SortOrder
            select new { Question = question, template.InspectionType }).ToListAsync(cancellationToken);
        var answers = checklist.Select(item =>
        {
            var question = item.Question;
            var answer = savedAnswers[question.Id];
            return new SurveyorInspectionAnswerResponse(
                question.Id, item.InspectionType, question.Text, question.SortOrder, question.IsRequired,
                bool.TryParse(answer.Value, out var isChecked) && isChecked, answer.Comment,
                question.SectionCode, question.ApplicableProtectionTypes, question.SourceItemNumber,
                question.SourceItemReference, question.ApplicableGrades, question.SubChecksJson);
        }).ToArray();
        var photoEntities = await db.Files.AsNoTracking()
            .Where(x => x.SurveySessionId == inspection.Id && x.Category == FileCategory.EquipmentPhoto)
            .OrderBy(x => x.UploadedAt)
            .ToListAsync(cancellationToken);
        var photos = photoEntities.Select(ToInspectionPhotoResponse).ToArray();
        var review = await db.QcReviews.AsNoTracking().SingleOrDefaultAsync(
            x => x.SurveySessionId == inspection.Id, cancellationToken);

        return new SurveyorEquipmentInspectionResponse(
            inspection.Id,
            inspection.AreaEquipmentId!.Value,
            inspection.InspectionTimestamp ?? inspection.CreatedAt,
            inspection.InspectorName ?? string.Empty,
            inspection.SurveyorRemarks,
            GetInspectionStatus(inspection.Status, review?.Status),
            !IsInspectionLocked(inspection, review),
            inspection.CreatedAt,
            inspection.UpdatedAt,
            answers,
            photos,
            review?.Comments,
            review?.Severity);
    }

    private async Task<InspectionValidationResult> ValidateInspectionRequest(
        SaveEquipmentInspectionRequest request, CancellationToken cancellationToken, Guid? inspectionId = null)
    {
        if (!request.InspectionDate.HasValue)
            return new InspectionValidationResult(BadRequest(new { error = "InspectionDate is required." }));
        if (string.IsNullOrWhiteSpace(request.InspectorName))
            return new InspectionValidationResult(BadRequest(new { error = "InspectorName is required." }));
        if (request.Answers is null)
            return new InspectionValidationResult(BadRequest(new { error = "Answers are required." }));

        var answerIds = request.Answers.Select(x => x.ChecklistItemId).ToArray();
        if (answerIds.Any(x => x == Guid.Empty) || answerIds.Distinct().Count() != answerIds.Length)
            return new InspectionValidationResult(BadRequest(new { error = "Checklist answers contain invalid or duplicate item IDs." }));
        var permitted = inspectionId.HasValue
            ? await db.SurveyAnswers.AsNoTracking().Where(x => x.SurveySessionId == inspectionId.Value)
                .Select(x => x.ChecklistQuestionId).ToListAsync(cancellationToken)
            : await CurrentChecklistQuestions().Select(x => x.Id).ToListAsync(cancellationToken);
        if (answerIds.Any(id => !permitted.Contains(id)) ||
            (inspectionId.HasValue && !permitted.ToHashSet().SetEquals(answerIds)))
            return new InspectionValidationResult(BadRequest(new { error = "Answers must use this inspection's original checklist. Reload before saving." }));
        return new InspectionValidationResult(null, permitted);
    }

    private static SurveyorEquipmentDetailsResponse ToEquipmentDetailsResponse(
        EquipmentContext context, InspectionInfo? inspection)
    {
        var pin = context.Equipment;
        var status = inspection is null
            ? EquipmentInspectionStatus.NotStarted
            : GetInspectionStatus(inspection.SurveyStatus, inspection.QcStatus);
        return new SurveyorEquipmentDetailsResponse(
            pin.Id,
            pin.ProjectId,
            pin.AreaId,
            context.Area.AreaName,
            pin.SequenceNo,
            pin.XPercent,
            pin.YPercent,
            pin.TagNumber,
            pin.EquipmentCode,
            pin.Type,
            pin.CircuitId,
            pin.Location,
            pin.Manufacturer,
            pin.ProtectionDetails,
            pin.CertificateNumber,
            pin.RfidTagUid,
            pin.CreatedAt,
            inspection?.InspectionId,
            status,
            status != EquipmentInspectionStatus.QcApproved,
            inspection?.QcComments,
            inspection?.QcSeverity);
    }

    private static EquipmentInspectionStatus GetInspectionStatus(SurveyStatus surveyStatus, QcReviewStatus? qcStatus) =>
        QcWorkflowStatus.InspectionStatus(surveyStatus, qcStatus);

    private static bool IsInspectionLocked(SurveySession inspection, QcReview? review) =>
        inspection.Status == SurveyStatus.Approved || review?.Status == QcReviewStatus.Approved;

    private static SurveyorInspectionPhotoResponse ToInspectionPhotoResponse(SurveyFile photo) => new(
        photo.Id,
        photo.FileName,
        $"/api/surveyor/inspections/{photo.SurveySessionId}/photos/{photo.Id}",
        photo.UploadedAt);

    private async Task DeleteStoredFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                await photoStorage.DeleteAsync(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not clean up stored inspection photo {Path}", path);
            }
        }
    }

    private ObjectResult ProjectForbidden() => StatusCode(
        StatusCodes.Status403Forbidden,
        new { error = "This project is assigned to another Surveyor." });

    private async Task<ProjectAccess> CheckProjectAccess(Guid projectId, CancellationToken cancellationToken)
    {
        var assignedSurveyorId = await db.Projects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => (Guid?)x.AssignedSurveyorId)
            .SingleOrDefaultAsync(cancellationToken);

        if (assignedSurveyorId is null)
            return ProjectAccess.NotFound;
        return assignedSurveyorId == CurrentUserId ? ProjectAccess.Allowed : ProjectAccess.Forbidden;
    }

    private ObjectResult ProjectAccessError(ProjectAccess access) => access == ProjectAccess.NotFound
        ? NotFound(new { error = "Project was not found." })
        : StatusCode(StatusCodes.Status403Forbidden,
            new { error = "This project is assigned to another Surveyor." });

    private Task<bool> AreaExists(Guid projectId, Guid areaId, CancellationToken cancellationToken) =>
        db.ProjectAreas.AsNoTracking().AnyAsync(
            x => x.Id == areaId && x.ProjectId == projectId, cancellationToken);

    private async Task<Project> TouchProject(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleAsync(x => x.Id == projectId, cancellationToken);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        return project;
    }

    private BadRequestObjectResult? ValidateRequest(UpsertAreaEquipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TagNumber) || string.IsNullOrWhiteSpace(request.Type))
            return BadRequest(new { error = "TagNumber and Type are required." });
        if (request.XPercent is < 0 or > 100 || request.YPercent is < 0 or > 100)
            return BadRequest(new { error = "XPercent and YPercent must be between 0 and 100." });
        return null;
    }

    private static SurveyorAreaSummaryResponse ToAreaSummary(Guid projectId, ProjectArea area) => new(
        area.Id,
        area.AreaName,
        GetLayoutImagePath(projectId, area),
        area.Photos.Count,
        area.EquipmentPins.Count);

    private static string? GetLayoutImagePath(Guid projectId, ProjectArea area)
    {
        var layoutPhoto = area.Photos.FirstOrDefault(x => x.FilePath == area.LayoutImagePath)
                          ?? area.Photos.OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).FirstOrDefault();
        return layoutPhoto is null
            ? null
            : $"/api/projects/{projectId}/areas/{area.Id}/photos/{layoutPhoto.Id}";
    }

    private async Task<Dictionary<Guid, EquipmentReviewInfo>> LoadEquipmentReviews(
        Guid[] equipmentIds, CancellationToken cancellationToken)
    {
        if (equipmentIds.Length == 0)
            return [];

        return await (
            from inspection in db.SurveySessions.AsNoTracking()
            where inspection.AreaEquipmentId.HasValue && equipmentIds.Contains(inspection.AreaEquipmentId.Value)
            join review in db.QcReviews.AsNoTracking() on inspection.Id equals review.SurveySessionId into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            select new EquipmentReviewInfo(
                inspection.AreaEquipmentId!.Value,
                inspection.Id,
                inspection.Status,
                review == null ? QcReviewStatus.PendingReview : review.Status,
                review == null ? null : review.Comments,
                review == null ? null : review.Severity))
            .ToDictionaryAsync(x => x.EquipmentId, cancellationToken);
    }

    private static AreaEquipmentResponse ToEquipmentResponse(
        AreaEquipment equipment, EquipmentReviewInfo? review = null) => new(
        equipment.Id,
        equipment.ProjectId,
        equipment.AreaId,
        equipment.SequenceNo,
        equipment.XPercent,
        equipment.YPercent,
        equipment.TagNumber,
        equipment.EquipmentCode,
        equipment.Type,
        equipment.CircuitId,
        equipment.Location,
        equipment.Manufacturer,
        equipment.ProtectionDetails,
        equipment.CertificateNumber,
        equipment.RfidTagUid,
        review is null
            ? EquipmentInspectionStatus.NotStarted.ToString()
            : GetInspectionStatus(review.SurveyStatus, review.Status).ToString(),
        equipment.CreatedByUserId,
        equipment.CreatedAt,
        equipment.UpdatedAt,
        review?.InspectionId,
        review?.Status,
        review?.Comments,
        review?.Severity,
        equipment.IsPlacedOnLayout,
        equipment.Description,
        equipment.Model,
        equipment.SerialNumber,
        equipment.ExProtectionType,
        equipment.GasGroup,
        equipment.TemperatureClass,
        equipment.HazardousAreaZone,
        equipment.AmbientTemperatureRange,
        equipment.IpRating,
        equipment.NotifiedBody,
        equipment.SpecialConditionsOfUse,
        equipment.RfidIdentificationMethod,
        equipment.RegistrationStatus);

    private bool MatchesClientRegistration(AreaEquipment existing, UpsertAreaEquipmentRequest request,
        Guid projectId, Guid areaId, string tagNumber, string? normalizedUid) =>
        existing.ProjectId == projectId && existing.AreaId == areaId && existing.CreatedByUserId == CurrentUserId &&
        existing.TagNumber == tagNumber && existing.EquipmentCode == TrimOrNull(request.EquipmentId) &&
        existing.Type == request.Type.Trim() && existing.CircuitId == TrimOrNull(request.CircuitId) &&
        existing.Location == TrimOrNull(request.Location) && existing.Manufacturer == TrimOrNull(request.Manufacturer) &&
        existing.ProtectionDetails == TrimOrNull(request.ProtectionDetails) && existing.CertificateNumber == TrimOrNull(request.CertificateNumber) &&
        existing.RfidTagUid == normalizedUid && existing.IsPlacedOnLayout == request.IsPlacedOnLayout &&
        existing.Description == TrimOrNull(request.Description) && existing.Model == TrimOrNull(request.Model) &&
        existing.SerialNumber == TrimOrNull(request.SerialNumber) && existing.ExProtectionType == TrimOrNull(request.ExProtectionType) &&
        existing.GasGroup == TrimOrNull(request.GasGroup) && existing.TemperatureClass == TrimOrNull(request.TemperatureClass) &&
        existing.HazardousAreaZone == TrimOrNull(request.HazardousAreaZone) &&
        existing.AmbientTemperatureRange == TrimOrNull(request.AmbientTemperatureRange) &&
        existing.IpRating == TrimOrNull(request.IpRating) && existing.NotifiedBody == TrimOrNull(request.NotifiedBody) &&
        existing.SpecialConditionsOfUse == TrimOrNull(request.SpecialConditionsOfUse) &&
        existing.RfidIdentificationMethod == (normalizedUid is null ? null : (request.RfidIdentificationMethod ?? "HF_RFID").Trim().ToUpperInvariant());

    private async Task<bool> EquipmentPhotoContentMatchesAsync(SurveyFile prior, IFormFile incoming, CancellationToken cancellationToken)
    {
        var stored = await photoStorage.OpenReadAsync(prior.StorageKey, cancellationToken);
        if (stored is null) return false;
        await using (stored.Stream)
        await using (var uploaded = incoming.OpenReadStream())
        {
            var storedHash = await SHA256.HashDataAsync(stored.Stream, cancellationToken);
            var incomingHash = await SHA256.HashDataAsync(uploaded, cancellationToken);
            return CryptographicOperations.FixedTimeEquals(storedHash, incomingHash);
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeRfidTagUid(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new string(value.Trim()
            .Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '-')
            .ToArray()).ToUpperInvariant();

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private sealed record EquipmentReviewInfo(
        Guid EquipmentId,
        Guid InspectionId,
        SurveyStatus SurveyStatus,
        QcReviewStatus Status,
        string? Comments,
        QcSeverity? Severity);

    private sealed record EquipmentContext(AreaEquipment Equipment, ProjectArea Area, Project Project);
    private sealed record InspectionContext(SurveySession Inspection, AreaEquipment Equipment, Project Project);
    private sealed record InspectionInfo(
        Guid InspectionId,
        SurveyStatus SurveyStatus,
        QcReviewStatus? QcStatus,
        string? QcComments,
        QcSeverity? QcSeverity);
    private sealed record InspectionValidationResult(BadRequestObjectResult? Error, IReadOnlyList<Guid>? QuestionIds = null);

    private enum ProjectAccess { Allowed, NotFound, Forbidden }
}
