using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Services;

public sealed class GeneratedReportArtifactStorageSettings
{
    public string RootPath { get; set; } = "Storage/GeneratedReports";
}

public sealed record ArchivedReportFile(string StorageKey,string Sha256);

public interface IGeneratedReportArtifactStorage
{
    Task<ArchivedReportFile> SaveAsync(Guid projectId,Guid artifactId,string fileName,byte[] content,CancellationToken ct);
    Task<StoredProjectPhotoContent?> OpenReadAsync(string storageKey,CancellationToken ct);
}

/// <summary>Stores generated report binaries outside the web root and never overwrites existing artifacts.</summary>
public sealed class GeneratedReportArtifactStorage(IOptions<GeneratedReportArtifactStorageSettings> options,IWebHostEnvironment environment) : IGeneratedReportArtifactStorage
{
    private readonly string root=Path.GetFullPath(Path.IsPathRooted(options.Value.RootPath)?options.Value.RootPath:Path.Combine(environment.ContentRootPath,options.Value.RootPath));
    public async Task<ArchivedReportFile> SaveAsync(Guid projectId,Guid artifactId,string fileName,byte[] content,CancellationToken ct)
    {
        var safeName=string.Concat(Path.GetFileName(fileName).Where(c=>char.IsLetterOrDigit(c)||c is '.' or '-' or '_'));
        if(string.IsNullOrWhiteSpace(safeName))safeName="report.bin";
        var relative=Path.Combine(projectId.ToString("N"),artifactId.ToString("N")+"_"+safeName);
        var full=Resolve(relative);Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using(var stream=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.WriteThrough))
        {await stream.WriteAsync(content,ct);await stream.FlushAsync(ct);}
        return new(relative.Replace(Path.DirectorySeparatorChar,'/'),Convert.ToHexString(SHA256.HashData(content)));
    }

    public async Task<StoredProjectPhotoContent?> OpenReadAsync(string storageKey,CancellationToken ct)
    {
        var path=Resolve(storageKey.Replace('/',Path.DirectorySeparatorChar));
        if(!File.Exists(path))return null;
        var bytes=await File.ReadAllBytesAsync(path,ct);
        var type=Path.GetExtension(path).ToLowerInvariant() switch{".pdf"=>"application/pdf",".zip"=>"application/zip",".csv"=>"text/csv",_=>"application/octet-stream"};
        return new(new MemoryStream(bytes,writable:false),type);
    }

    private string Resolve(string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidOperationException("Report artifact path must be relative.");
        var full=Path.GetFullPath(Path.Combine(root,relative));var prefix=root+Path.DirectorySeparatorChar;
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Report artifact path is outside the archive root.");
        return full;
    }
}
