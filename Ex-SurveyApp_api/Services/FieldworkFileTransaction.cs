namespace Ex_SurveyApp_api.Services;

/// <summary>
/// Keeps stored files consistent with the browser-fieldwork SQL transaction opened by <see cref="FieldworkMutationFilter"/>.
/// While it is active, controllers must not delete a replaced file immediately (the transaction may still roll back
/// and the database would then point at a missing file), and newly stored files are removed if the transaction does
/// not commit. Outside fieldwork requests it is inactive and controllers keep their existing behaviour.
/// </summary>
public sealed class FieldworkFileTransaction(IProjectPhotoStorage storage, ILogger<FieldworkFileTransaction> logger)
{
    private readonly List<string> deleteAfterCommit = [];
    private readonly List<string> deleteIfRolledBack = [];
    public bool Active { get; private set; }

    public void Begin() { Active = true; deleteAfterCommit.Clear(); deleteIfRolledBack.Clear(); }
    public void DeleteAfterCommit(string relativePath) => deleteAfterCommit.Add(relativePath);
    public void DeleteIfRolledBack(string relativePath) => deleteIfRolledBack.Add(relativePath);

    public async Task CompleteAsync(bool committed)
    {
        if (!Active) return;
        Active = false;
        foreach (var path in committed ? deleteAfterCommit : deleteIfRolledBack)
        {
            try { await storage.DeleteAsync(path, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Fieldwork file cleanup failed for {Path}", path); }
        }
        deleteAfterCommit.Clear(); deleteIfRolledBack.Clear();
    }

    /// <summary>The active fieldwork file transaction for this request, or null for ordinary requests.</summary>
    public static FieldworkFileTransaction? For(Microsoft.AspNetCore.Http.HttpContext? context) =>
        context?.RequestServices.GetService(typeof(FieldworkFileTransaction)) is FieldworkFileTransaction { Active: true } files ? files : null;
}
