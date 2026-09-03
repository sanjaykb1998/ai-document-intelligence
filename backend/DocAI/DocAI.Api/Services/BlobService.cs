using System.IO;

public class BlobService
{
    private readonly string _uploadDirectory;

    public BlobService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var uploadPath = configuration["Storage:UploadPath"];
        if (string.IsNullOrWhiteSpace(uploadPath))
        {
            uploadPath = "uploads";
        }

        _uploadDirectory = Path.IsPathRooted(uploadPath)
            ? uploadPath
            : Path.Combine(environment.ContentRootPath, uploadPath);

        Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task<string> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is empty.", nameof(file));
        }

        var storedFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(_uploadDirectory, storedFileName);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream);

        return storedFileName;
    }

    public string GetFilePath(string storedFileName)
    {
        return Path.Combine(_uploadDirectory, storedFileName);
    }

    public Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(string storedFileName)
    {
        var filePath = GetFilePath(storedFileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", storedFileName);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string contentType = "application/octet-stream";
        string fileName = Path.GetFileName(storedFileName);
        return Task.FromResult((stream, contentType, fileName));
    }

    public Task DeleteAsync(string storedFileName)
    {
        var filePath = GetFilePath(storedFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}