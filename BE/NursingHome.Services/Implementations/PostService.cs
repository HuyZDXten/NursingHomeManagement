using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Admin;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-19 tin tức và UC-115 album ảnh. Bài tạo ra ở dạng nháp; chỉ bài đã đăng mới hiện ở trang công khai.
/// Album chỉ đăng khi đã xác nhận có sự đồng ý của cư dân/gia đình (ràng buộc cả ở DB).
/// </summary>
public class PostService(ICommsRepository comms, FacilityClock clock) : IPostService
{
    public const int MaxPageSize = 50;

    public Task<PageDto<PostSummaryDto>> ListAsync(string? kind, int page, int pageSize) => PageAsync(false, kind, page, pageSize);

    public Task<PageDto<PostSummaryDto>> ListPublishedAsync(string? kind, int page, int pageSize) => PageAsync(true, kind, page, pageSize);

    public async Task<PostDto> GetAsync(int id) =>
        (await comms.GetPostAsync(id) ?? throw new NotFoundException("Không tìm thấy bài viết.")).ToDto();

    public async Task<PublicPostDto> GetPublishedAsync(string slug) =>
        (await comms.GetPublishedBySlugAsync(slug.Trim().ToLowerInvariant()) ?? throw new NotFoundException("Không tìm thấy bài viết.")).ToPublic();

    public async Task<PostDto> CreateAsync(CurrentUser user, SavePostRequest request)
    {
        var kind = PostRules.ParseKind(request.Kind);
        var images = BuildImages(request);
        var post = new Post
        {
            Kind = kind,
            Title = request.Title.Trim(),
            Slug = await UniqueSlugAsync(request.Title),
            Summary = Clean(request.Summary),
            Content = request.Content.Trim(),
            CoverUrl = CoverOf(request, images),
            ConsentConfirmed = request.ConsentConfirmed,
            IsPublished = false,
            CreatedById = user.AccountId,
            CreatedAt = clock.UtcNow,
            Images = images
        };
        RequireText(post);
        var created = await comms.CreatePostAsync(post);
        return await GetAsync(created.Id);
    }

    public async Task<PostDto> UpdateAsync(int id, SavePostRequest request)
    {
        var images = BuildImages(request);
        var cover = CoverOf(request, images);
        var now = clock.UtcNow;
        var updated = await comms.UpdatePostAsync(id, post =>
        {
            if (PostRules.ParseKind(request.Kind) != post.Kind)
            {
                throw new BusinessRuleException("Không đổi được loại bài (tin tức/album) sau khi tạo.");
            }

            post.Title = request.Title.Trim(); // slug giữ nguyên để đường dẫn đã chia sẻ không chết
            post.Summary = Clean(request.Summary);
            post.Content = request.Content.Trim();
            post.CoverUrl = cover;
            post.ConsentConfirmed = request.ConsentConfirmed;
            post.UpdatedAt = now;
            RequireText(post);
            if (post.IsPublished)
            {
                PostRules.ValidatePublished(post.Kind, post.ConsentConfirmed, images.Count);
            }
        }, images);
        if (updated is null)
        {
            throw new NotFoundException("Không tìm thấy bài viết.");
        }

        return await GetAsync(id);
    }

    public async Task<PostDto> SetPublishedAsync(int id, bool publish)
    {
        var now = clock.UtcNow;
        var updated = await comms.UpdatePostAsync(id, post =>
        {
            if (publish)
            {
                PostRules.ValidatePublished(post.Kind, post.ConsentConfirmed, post.Images.Count);
                if (!post.IsPublished)
                {
                    post.IsPublished = true;
                    post.PublishedAt = now;
                }
            }
            else
            {
                post.IsPublished = false;
            }

            post.UpdatedAt = now;
        }, null);
        if (updated is null)
        {
            throw new NotFoundException("Không tìm thấy bài viết.");
        }

        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        if (!await comms.DeletePostAsync(id))
        {
            throw new NotFoundException("Không tìm thấy bài viết.");
        }
    }

    private async Task<PageDto<PostSummaryDto>> PageAsync(bool publishedOnly, string? kind, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        PostKind? parsed = string.IsNullOrWhiteSpace(kind) ? null : PostRules.ParseKind(kind);
        var (items, total) = await comms.ListPostsAsync(publishedOnly, parsed, (page - 1) * pageSize, pageSize);
        return new PageDto<PostSummaryDto>
        {
            Items = items.Select(row => row.Post.ToSummary(row.ImageCount)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static List<PostImage> BuildImages(SavePostRequest request)
    {
        if (request.Images.Count > PostRules.MaxImages)
        {
            throw new BusinessRuleException($"Tối đa {PostRules.MaxImages} ảnh mỗi bài.");
        }

        if (request.CoverUrl is not null && !PostRules.IsTrustedImageUrl(request.CoverUrl))
        {
            throw new BusinessRuleException("Ảnh bìa phải là ảnh đã tải lên hệ thống.");
        }

        var images = new List<PostImage>();
        foreach (var (item, index) in request.Images.Select((item, index) => (item, index)))
        {
            if (!PostRules.IsTrustedImageUrl(item.Url))
            {
                throw new BusinessRuleException("Ảnh phải là ảnh đã tải lên hệ thống (dùng nút Tải ảnh).");
            }

            images.Add(new PostImage { Url = item.Url, Caption = Clean(item.Caption), SortOrder = index });
        }

        return images;
    }

    /// <summary>Ảnh bìa: do người dùng chọn, không thì ảnh đầu tiên.</summary>
    private static string? CoverOf(SavePostRequest request, List<PostImage> images) =>
        string.IsNullOrWhiteSpace(request.CoverUrl) ? images.FirstOrDefault()?.Url : request.CoverUrl;

    private static void RequireText(Post post)
    {
        if (post.Title.Length == 0 || post.Content.Length == 0)
        {
            throw new BusinessRuleException("Nhập tiêu đề và nội dung bài viết.");
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<string> UniqueSlugAsync(string title)
    {
        string baseSlug = PostRules.Slugify(title);
        string slug = baseSlug;
        for (int suffix = 2; await comms.SlugExistsAsync(slug); suffix++)
        {
            string tail = $"-{suffix}";
            slug = (baseSlug.Length + tail.Length > PostRules.MaxSlugLength ? baseSlug[..(PostRules.MaxSlugLength - tail.Length)] : baseSlug) + tail;
        }

        return slug;
    }
}
