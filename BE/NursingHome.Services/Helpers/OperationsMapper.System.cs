using NursingHome.BusinessObjects.Entities;
using NursingHome.DTOs.Admin;

namespace NursingHome.Services.Helpers;

public static partial class OperationsMapper
{
    public static AuditLogDto ToDto(this AuditLog log) => new()
    {
        Id = log.Id,
        AccountId = log.AccountId,
        ActorName = log.ActorName ?? "(hệ thống)",
        Action = log.Action,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Summary = log.Summary,
        DataJson = log.DataJson,
        Ip = log.Ip,
        At = log.At
    };

    public static LookupDto ToDto(this Lookup lookup) => new()
    {
        Id = lookup.Id,
        Category = lookup.Category,
        Code = lookup.Code,
        Name = lookup.Name,
        SortOrder = lookup.SortOrder,
        IsActive = lookup.IsActive
    };

    public static AnnouncementDto ToDto(this Announcement announcement) => new()
    {
        Id = announcement.Id,
        Title = announcement.Title,
        Body = announcement.Body,
        Audience = announcement.Audience,
        Role = announcement.Role,
        SendEmail = announcement.SendEmail,
        RecipientCount = announcement.RecipientCount,
        EmailSentCount = announcement.EmailSentCount,
        CreatedByName = announcement.CreatedBy?.FullName ?? "",
        CreatedAt = announcement.CreatedAt
    };

    public static PostSummaryDto ToSummary(this Post post, int imageCount) => new()
    {
        Id = post.Id,
        Kind = post.Kind.ToString(),
        Title = post.Title,
        Slug = post.Slug,
        Summary = post.Summary,
        CoverUrl = post.CoverUrl,
        IsPublished = post.IsPublished,
        PublishedAt = post.PublishedAt,
        ImageCount = imageCount,
        CreatedAt = post.CreatedAt
    };

    public static PostImageDto ToDto(this PostImage image) => new() { Id = image.Id, Url = image.Url, Caption = image.Caption, SortOrder = image.SortOrder };

    public static PostDto ToDto(this Post post)
    {
        var dto = new PostDto
        {
            Content = post.Content,
            ConsentConfirmed = post.ConsentConfirmed,
            CreatedByName = post.CreatedBy?.FullName ?? "",
            UpdatedAt = post.UpdatedAt,
            Images = post.Images.OrderBy(item => item.SortOrder).ThenBy(item => item.Id).Select(item => item.ToDto()).ToList()
        };
        var summary = post.ToSummary(post.Images.Count);
        dto.Id = summary.Id;
        dto.Kind = summary.Kind;
        dto.Title = summary.Title;
        dto.Slug = summary.Slug;
        dto.Summary = summary.Summary;
        dto.CoverUrl = summary.CoverUrl;
        dto.IsPublished = summary.IsPublished;
        dto.PublishedAt = summary.PublishedAt;
        dto.ImageCount = summary.ImageCount;
        dto.CreatedAt = summary.CreatedAt;
        return dto;
    }

    public static PublicPostDto ToPublic(this Post post) => new()
    {
        Id = post.Id,
        Kind = post.Kind.ToString(),
        Title = post.Title,
        Slug = post.Slug,
        Summary = post.Summary,
        CoverUrl = post.CoverUrl,
        PublishedAt = post.PublishedAt,
        Content = post.Content,
        Images = post.Images.OrderBy(item => item.SortOrder).ThenBy(item => item.Id).Select(item => item.ToDto()).ToList()
    };
}
