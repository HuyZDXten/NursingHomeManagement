using NursingHome.Services.Interfaces;

namespace NursingHome.API.Storage;

/// <summary>
/// BL-V2: giấy tờ/hợp đồng (CCCD, BHYT...) lưu ở {ContentRoot}/App_Data/documents — NGOÀI wwwroot nên không ai tải
/// trực tiếp được; chỉ đọc qua endpoint có kiểm quyền. Không đưa lên Cloudinary vì là dữ liệu nhạy cảm.
/// </summary>
public class LocalDocumentStorage(IWebHostEnvironment environment) : IDocumentStorage
{
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "documents");

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        string name = $"{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(Root, name)))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        return name;
    }

    public Stream OpenRead(string storedName) => File.OpenRead(PathOf(storedName));

    public void Delete(string storedName)
    {
        string path = PathOf(storedName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Tên lưu do hệ thống sinh (GUID + đuôi); vẫn chặn ký tự đường dẫn phòng dữ liệu hỏng.</summary>
    private string PathOf(string storedName)
    {
        if (storedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || storedName.Contains(".."))
        {
            throw new FileNotFoundException("Tên file không hợp lệ.");
        }

        return Path.Combine(Root, storedName);
    }
}
