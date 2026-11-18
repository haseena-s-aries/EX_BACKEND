using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Services;

/// <summary>Opt-in, atomic replay protection for the existing fieldwork endpoints only.</summary>
public sealed class FieldworkMutationFilter(SurveyDbContext db) : IAsyncResourceFilter
{
    private static readonly HashSet<string> Allowed = [
        "Surveyor.CreateEquipment", "Surveyor.UpdateEquipment", "Surveyor.UploadEquipmentPhotos",
        "InspectionActivities.OpenCategoryChecklist", "InspectionActivities.StartForEquipment",
        "InspectionActivities.SaveCheckpoint", "InspectionActivities.SaveEquipmentComments",
        "InspectionActivities.UploadPhoto", "InspectionActivities.Submit", "InspectionActivities.AddFinding",
        "InspectionActivities.SaveCategorySelection",
        "Defects.SaveRegisterRow", "Defects.SetRegisterPhoto"];

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;
        if (!request.Headers.ContainsKey("X-Fieldwork-Operation")) { await next(); return; }
        var ct = http.RequestAborted;
        var route = context.RouteData.Values;
        var action = $"{route["controller"]}.{route["action"]}";
        if (!Allowed.Contains(action) || !http.User.IsInRole("Surveyor") ||
            !Guid.TryParse(request.Headers["X-Fieldwork-Operation"], out var operationId) || operationId == Guid.Empty ||
            !Guid.TryParse(request.Headers["X-Fieldwork-Project"], out var projectId))
        { context.Result = new BadRequestObjectResult(new {error = "Invalid fieldwork operation."}); return; }
        if (!db.Database.IsSqlServer())
        { context.Result = new ObjectResult(new {error = "Durable fieldwork synchronization requires SQL Server."}) {StatusCode = 503}; return; }

        request.EnableBuffering(64 * 1024, 64 * 1024 * 1024);
        // Multipart boundaries change on retry. Hash fields and actual file bytes, not the wire boundary.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(request.Method + request.Path + request.QueryString));
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);
            foreach (var field in form.OrderBy(f => f.Key)) hash.AppendData(Encoding.UTF8.GetBytes(field.Key + ":" + field.Value + "\n"));
            foreach (var file in form.Files)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(file.Name + ":" + file.FileName + ":" + file.ContentType));
                await using var stream = file.OpenReadStream();
                var bytes = new byte[81920]; int count;
                while ((count = await stream.ReadAsync(bytes, ct)) > 0) hash.AppendData(bytes.AsSpan(0, count));
            }
        }
        else
        {
            var bytes = new byte[81920]; int count;
            while ((count = await request.Body.ReadAsync(bytes, ct)) > 0) hash.AppendData(bytes.AsSpan(0, count));
        }
        request.Body.Position = 0;
        var requestHash = Convert.ToHexString(hash.GetHashAndReset());
        var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var lockName = $"Fieldwork:{userId}:{operationId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={lockName}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @r < 0 THROW 51000, 'Fieldwork synchronization is busy. Retry shortly.', 1;", ct);
        // Check assignment even when replaying a committed response after reassignment.
        if (!await FieldworkAccess.Projects(db, http.User).AnyAsync(p => p.Id == projectId, ct))
        { context.Result = new ObjectResult(new {error = "Project assignment has changed. Local work is retained for authorized resolution."}) {StatusCode = 403}; return; }
        if (Guid.TryParse(route.GetValueOrDefault("projectId")?.ToString(), out var routeProject) && routeProject != projectId)
        { context.Result = new ForbidResult(); return; }
        var prior = await db.Set<FieldworkReceipt>().SingleOrDefaultAsync(r => r.UserId == userId && r.OperationId == operationId, ct);
        if (prior != null)
        {
            context.Result = prior.ProjectId != projectId || prior.RequestHash != requestHash
                ? new ConflictObjectResult(new {error = "This operation ID was already used for different data."})
                : new ContentResult {StatusCode = prior.StatusCode, ContentType = "application/json", Content = prior.ResponseJson};
            return;
        }

        string? kind = action switch
        {
            "Surveyor.UpdateEquipment" => "equipment",
            "Defects.SaveRegisterRow" => "defect",
            "InspectionActivities.SaveCategorySelection" => "checklist",
            _ => null
        };
        var entityId = Guid.Empty;
        if (kind == "checklist")
        {
            // Browser fieldwork only edits equipment-specific checklists; project-wide selections stay Admin-only and online.
            if (!Guid.TryParse(request.Query["equipmentId"], out entityId) || entityId == Guid.Empty)
            { context.Result = new BadRequestObjectResult(new {error = "Checklist synchronization requires an equipment ID."}); return; }
        }
        else if (kind != null && !Guid.TryParse(route.GetValueOrDefault(kind == "equipment" ? "equipmentId" : "findingId")?.ToString(), out entityId))
            kind = null;
        if (kind != null)
        {
            var entity = await FieldworkAccess.Entity(db, kind, entityId, projectId, ct);
            if (kind == "checklist" && entity == null) { context.Result = new ForbidResult(); return; }
            var version = entity == null ? "new" : FieldworkAccess.Version(entity);
            if (request.Headers["X-Fieldwork-Version"] != version)
            { context.Result = new ConflictObjectResult(new {error = "The server record changed. Review both versions before retrying.", version, server = entity}); return; }
        }
        if (Guid.TryParse(route.GetValueOrDefault("activityId")?.ToString(), out var activityId))
        {
            if (await FieldworkAccess.Entity(db, "activity", activityId, projectId, ct) == null)
            { context.Result = new ForbidResult(); return; }
        }

        var originalBody = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            var executed = await next();
            if (executed.Exception != null && !executed.ExceptionHandled) return;
            if (http.Response.StatusCode is >= 200 and < 300)
            {
                var body = buffer.Length == 0 ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(buffer.ToArray());
                var savedActivityId = activityId;
                if (action is "InspectionActivities.OpenCategoryChecklist" or "InspectionActivities.StartForEquipment" &&
                    body is {ValueKind: JsonValueKind.Object} b && b.TryGetProperty("id", out var returnedId))
                    Guid.TryParse(returnedId.GetString(), out savedActivityId);
                var activityVersion = savedActivityId == Guid.Empty ? null : await db.InspectionActivities.AsNoTracking()
                    .Where(a => a.Id == savedActivityId).Select(a => a.RowVersion).SingleOrDefaultAsync(ct);
                object? savedEntity = kind == null ? null : await FieldworkAccess.Entity(db, kind, entityId, projectId, ct);
                if (action == "Surveyor.CreateEquipment" && body is {ValueKind: JsonValueKind.Object} eb && eb.TryGetProperty("id", out var eid))
                    savedEntity = await FieldworkAccess.Entity(db, "equipment", eid.GetGuid(), projectId, ct);
                var responseJson = JsonSerializer.Serialize(new {result = body,
                    version = savedEntity == null ? null : FieldworkAccess.Version(savedEntity),
                    activityRowVersion = activityVersion == null ? null : Convert.ToBase64String(activityVersion)});
                buffer.SetLength(0);
                await buffer.WriteAsync(Encoding.UTF8.GetBytes(responseJson), ct);
                http.Response.StatusCode = 200;
                http.Response.ContentType = "application/json";
                http.Response.ContentLength = buffer.Length;
                db.Set<FieldworkReceipt>().Add(new FieldworkReceipt {UserId = userId, OperationId = operationId,
                    ProjectId = projectId, RequestHash = requestHash, StatusCode = http.Response.StatusCode,
                    ResponseJson = responseJson});
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            else await tx.RollbackAsync(ct);
            // A lost response is safe: the committed receipt is replayed on the next attempt.
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, ct);
        }
        finally { http.Response.Body = originalBody; }
    }
}
