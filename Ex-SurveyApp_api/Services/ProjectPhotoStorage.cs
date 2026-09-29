using Microsoft.Extensions.Options;

namespace Ex_SurveyApp_api.Services;

public class ProjectPhotoStorageSettings
{
    public string RootPath { get; set; } = "Storage/ProjectAreaPhotos";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxFilesPerRequest { get; set; } = 20;
}

public record StoredProjectPhoto(string OriginalFileName, string RelativePath);
public record StoredProjectPhotoContent(Stream Stream, string ContentType);

public interface IProjectPhotoStorage
{
    int MaxFilesPerRequest { get; }
    Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId,
        CancellationToken cancellationToken);
    Task<StoredProjectPhoto> SaveInspectionAsync(IFormFile file, Guid projectId, Guid inspectionId,
        CancellationToken cancellationToken);
    Task<StoredProjectPhotoContent?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public class ProjectPhotoValidationException(string message) : Exception(message);

public class ProjectPhotoStorage : IProjectPhotoStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ProjectPhotoStorageSettings settings;
    private readonly string rootPath;

    public ProjectPhotoStorage(IOptions<ProjectPhotoStorageSettings> options, IWebHostEnvironment environment)
    {
        settings = options.Value;
        if (settings.MaxFileSizeBytes <= 0 || settings.MaxFilesPerRequest <= 0)
            throw new InvalidOperationException("Project photo storage limits must be greater than zero.");
        rootPath = Path.GetFullPath(Path.IsPathRooted(settings.RootPath)
            ? settings.RootPath
            : Path.Combine(environment.ContentRootPath, settings.RootPath));
    }

    public int MaxFilesPerRequest => settings.MaxFilesPerRequest;

    public async Task<StoredProjectPhoto> SaveAsync(IFormFile file, Guid projectId, Guid areaId,
        CancellationToken cancellationToken)
        => await SaveToDirectory(file, Path.Combine(projectId.ToString("N"), areaId.ToString("N")),
            cancellationToken);

    public async Task<StoredProjectPhoto> SaveInspectionAsync(
        IFormFile file, Guid projectId, Guid inspectionId, CancellationToken cancellationToken)
        => await SaveToDirectory(file,
            Path.Combine("inspections", projectId.ToString("N"), inspectionId.ToString("N")),
            cancellationToken);

    private async Task<StoredProjectPhoto> SaveToDirectory(
        IFormFile file, string relativeDirectory, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new ProjectPhotoValidationException("Empty image files are not allowed.");
        if (file.Length > settings.MaxFileSizeBytes)
            throw new ProjectPhotoValidationException(
                $"Image '{Path.GetFileName(file.FileName)}' exceeds the {settings.MaxFileSizeBytes / 1024 / 1024} MB limit.");

        var originalFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255)
            throw new ProjectPhotoValidationException("Image filenames must contain 1 to 255 characters.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var hasSupportedContentType = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                                      file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        if (!AllowedExtensions.Contains(extension) || !hasSupportedContentType)
            throw new ProjectPhotoValidationException("Only JPG, PNG, and WebP images are allowed.");

        var directory = Path.Combine(rootPath, relativeDirectory);
        Directory.CreateDirectory(directory);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(directory, storedName);

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!HasValidImageSignature(header.AsSpan(0, bytesRead), extension))
            throw new ProjectPhotoValidationException($"Image '{Path.GetFileName(file.FileName)}' has invalid file content.");

        await using var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        await output.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
        await input.CopyToAsync(output, cancellationToken);

        var relativePath = Path.GetRelativePath(rootPath, absolutePath).Replace('\\', '/');
        return new StoredProjectPhoto(originalFileName, relativePath);
    }

    public Task<StoredProjectPhotoContent?> OpenReadAsync(
        string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolvePath(relativePath);
        if (!File.Exists(absolutePath))
            return Task.FromResult<StoredProjectPhotoContent?>(null);

        var contentType = Path.GetExtension(absolutePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<StoredProjectPhotoContent?>(new StoredProjectPhotoContent(stream, contentType));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolvePath(relativePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Photo path is outside the configured storage root.");
        return absolutePath;
    }

    private static bool HasValidImageSignature(ReadOnlySpan<byte> header, string extension) => extension switch
    {
        ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };
}
