using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Replaced files survive until commit; new files are removed when the fieldwork transaction rolls back.</summary>
internal static class FieldworkFileTransactionChecks
{
    private sealed class RecordingStorage : IProjectPhotoStorage
    {
        public readonly List<string> Deleted = [];
        public int MaxFilesPerRequest => 20;
        public Task<StoredProjectPhoto> SaveHazardousAreaPlanAsync(IFormFile file, Guid projectId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid inspectionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredProjectPhotoContent?> OpenReadAsync(string relativePath, CancellationToken ct = default) => Task.FromResult<StoredProjectPhotoContent?>(null);
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) { Deleted.Add(relativePath); return Task.CompletedTask; }
    }

    public static async Task Run()
    {
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL: " + name); Console.WriteLine("PASS: " + name); }
        var storage = new RecordingStorage();
        var files = new FieldworkFileTransaction(storage, NullLogger<FieldworkFileTransaction>.Instance);
        Check(!files.Active, "inactive outside fieldwork requests");
        files.Begin(); files.DeleteAfterCommit("old.png"); files.DeleteIfRolledBack("new.png");
        Check(storage.Deleted.Count == 0, "nothing is deleted before the transaction completes");
        await files.CompleteAsync(committed: false);
        Check(storage.Deleted.SequenceEqual(["new.png"]), "rollback removes only the new file and keeps the replaced file");
        storage.Deleted.Clear();
        files.Begin(); files.DeleteAfterCommit("old.png"); files.DeleteIfRolledBack("new.png");
        await files.CompleteAsync(committed: true);
        Check(storage.Deleted.SequenceEqual(["old.png"]), "commit removes only the replaced file");
        Check(!files.Active, "inactive after completion");
        Console.WriteLine("Fieldwork file transaction checks passed.");
    }
}
