using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Accounts;
using NursingHome.DTOs.Auth;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Residents;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// BL-V1 — UC-07 ảnh đại diện, UC-24 ảnh chân dung cư dân. Ảnh cũ không xóa khỏi kho (đơn giản, không mất ảnh
/// khi lưu DB lỗi giữa chừng); đổi ảnh chỉ đổi URL.
/// </summary>
public class MediaService(
    IImageStorage storage,
    IAccountRepository accounts,
    IResidentRepository residents,
    IResidentService residentService,
    FacilityClock clock) : IMediaService
{
    public async Task<UploadedImageDto> UploadImageAsync(Stream content, string? fileName, long length, string folder) =>
        new() { Url = await SaveAsync(content, fileName, length, folder) };

    public async Task<ProfileDto> SetMyAvatarAsync(CurrentUser user, Stream content, string? fileName, long length)
    {
        var account = await accounts.GetByIdAsync(user.AccountId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        account.AvatarUrl = await SaveAsync(content, fileName, length, ImageRules.AvatarFolder);
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);
        return account.ToProfile();
    }

    public async Task<ProfileDto> RemoveMyAvatarAsync(CurrentUser user)
    {
        var account = await accounts.GetByIdAsync(user.AccountId) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        account.AvatarUrl = null;
        account.UpdatedAt = clock.UtcNow;
        await accounts.UpdateAsync(account);
        return account.ToProfile();
    }

    public async Task<ResidentDetailDto> SetResidentPhotoAsync(CurrentUser user, int residentId, Stream content, string? fileName, long length)
    {
        var resident = await residents.GetByIdAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        resident.PhotoUrl = await SaveAsync(content, fileName, length, ImageRules.ResidentFolder);
        resident.UpdatedAt = clock.UtcNow;
        await residents.UpdateAsync(resident);
        return await residentService.GetAsync(user, residentId);
    }

    public async Task<ResidentDetailDto> RemoveResidentPhotoAsync(CurrentUser user, int residentId)
    {
        var resident = await residents.GetByIdAsync(residentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        resident.PhotoUrl = null;
        resident.UpdatedAt = clock.UtcNow;
        await residents.UpdateAsync(resident);
        return await residentService.GetAsync(user, residentId);
    }

    /// <summary>Kiểm kích thước + chữ ký file TRƯỚC khi lưu; stream phải tua lại được (controller chép vào MemoryStream).</summary>
    private async Task<string> SaveAsync(Stream content, string? fileName, long length, string folder)
    {
        if (!ImageRules.Folders.Contains(folder))
        {
            throw new BusinessRuleException("Thư mục ảnh không hợp lệ.");
        }

        if (length <= 0 || length > ImageRules.MaxBytes)
        {
            ImageRules.Validate(fileName, length, ReadOnlySpan<byte>.Empty);
        }

        var header = new byte[ImageRules.HeaderLength];
        int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        string extension = ImageRules.Validate(fileName, length, header.AsSpan(0, read));
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Stream ảnh phải tua lại được.");
        }

        content.Position = 0;
        return await storage.SaveAsync(content, extension, folder);
    }
}
