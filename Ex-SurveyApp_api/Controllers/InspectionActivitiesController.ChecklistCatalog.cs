using System.Data;
using System.Text.Json;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class InspectionActivitiesController
{
    private static readonly JsonSerializerOptions CatalogJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] CatalogSections = ["Apparatus", "Installation", "Environment"];

    private static CatalogContent DefaultCatalog() => new(
        Categories.Select(pair => new CatalogCategory(pair.Value, pair.Key, null)).ToArray(), []);

    private static CatalogContent DeserializeCatalog(EquipmentChecklistCatalog? row) =>
        row is null ? DefaultCatalog() : JsonSerializer.Deserialize<CatalogContent>(row.Content, CatalogJson)!;

    private async Task<CatalogContent> ReadCatalog(CancellationToken ct) =>
        DeserializeCatalog(await db.EquipmentChecklistCatalogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct));

    private static SampleChecklistPoint[] CatalogPoints(SampleChecklistSource source, CatalogContent catalog) =>
        source.Items.Concat(catalog.Checkpoints)
            .OrderBy(p => Array.IndexOf(CatalogSections, p.Section)).ThenBy(p => p.ItemNumber).ToArray();

    [HttpGet("category-checklist/catalog")]
    [Authorize(Roles = "Admin,Surveyor,Qc")]
    public async Task<IActionResult> GetChecklistCatalog(CancellationToken ct)
    {
        var catalog = await ReadCatalog(ct);
        var source = SampleChecklistImporter.ReadSource();
        var points = CatalogPoints(source, catalog);
        return Ok(new {
            catalog.Categories,
            NextNumbers = CatalogSections.ToDictionary(section => section,
                section => points.Where(p => p.Section == section).Max(p => p.ItemNumber) + 1)
        });
    }

    [HttpPut("category-checklist/catalog/categories")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public Task<IActionResult> SaveChecklistCategory(CatalogCategoryWrite request, CancellationToken ct) =>
        WriteCatalog(catalog =>
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || request.Description?.Length > 500 ||
                name.Equals("All Checkpoints", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Enter a category name of up to 80 characters and a description of up to 500 characters.");
            if (catalog.Categories.Any(c => c.Id != request.Id && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A category with this name already exists.");
            if (request.Id is not null && !catalog.Categories.Any(c => c.Id == request.Id))
                throw new ArgumentException("Category was not found.");
            var category = new CatalogCategory(request.Id ?? $"custom-{Guid.NewGuid():N}", name, request.Description?.Trim());
            return catalog with {Categories = request.Id is null ? [..catalog.Categories, category] :
                catalog.Categories.Select(c => c.Id == category.Id ? category : c).ToArray()};
        }, ct);

    [HttpPut("category-checklist/catalog/checkpoints")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public Task<IActionResult> SaveChecklistCheckpoint(CatalogCheckpointWrite request, CancellationToken ct) =>
        WriteCatalog(catalog =>
        {
            var text = request.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000 || !CatalogSections.Contains(request.Section) ||
                request.ItemNumber is < 1 or > 999999 || !catalog.Categories.Any(c => c.Id == request.CategoryId))
                throw new ArgumentException("Enter a valid section, number, description, and category.");
            var point = new SampleChecklistPoint(request.Section, request.ItemNumber, text, request.CategoryId, [], []);
            var code = PointCode(point);
            if (request.Code is null)
            {
                if (CatalogPoints(SampleChecklistImporter.ReadSource(), catalog).Any(p => PointCode(p) == code))
                    throw new ArgumentException("This checkpoint number already exists in the section.");
                return catalog with {Checkpoints = [..catalog.Checkpoints, point]};
            }
            if (request.Code != code || !catalog.Checkpoints.Any(p => PointCode(p) == code))
                throw new ArgumentException("Only custom checkpoint descriptions and categories can be edited here.");
            return catalog with {Checkpoints = catalog.Checkpoints.Select(p => PointCode(p) == code ? point : p).ToArray()};
        }, ct);

    private async Task<IActionResult> WriteCatalog(Func<CatalogContent, CatalogContent> update, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == ActorId && x.IsActive && x.Role == UserRole.Admin &&
            x.ApprovalStatus == ApprovalStatus.Approved, ct)) return Forbid();
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await db.EquipmentChecklistCatalogs.SingleOrDefaultAsync(x => x.Id == 1, ct);
        CatalogContent content;
        try { content = update(DeserializeCatalog(row)); }
        catch (ArgumentException ex) { return BadRequest(new {error = ex.Message}); }
        if (row is null)
        {
            row = new EquipmentChecklistCatalog {Id = 1};
            db.EquipmentChecklistCatalogs.Add(row);
        }
        row.Version++;
        row.Content = JsonSerializer.Serialize(content, CatalogJson);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }

    private sealed record CatalogContent(CatalogCategory[] Categories, SampleChecklistPoint[] Checkpoints);
}

public sealed record CatalogCategory(string Id, string Name, string? Description);
public sealed record CatalogCategoryWrite(string? Id, string? Name, string? Description);
public sealed record CatalogCheckpointWrite(string? Code, string Section, int ItemNumber, string? Text, string CategoryId);
