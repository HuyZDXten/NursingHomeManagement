namespace NursingHome.BusinessObjects.Entities;

public class Account
{
    public int Id { get; set; }

    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string FullName { get; set; } = "";

    public string? Phone { get; set; }

    /// <summary>Một trong <see cref="Enums.Roles"/>.</summary>
    public string Role { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public class RefreshToken
{
    public int Id { get; set; }

    public string TokenHash { get; set; } = "";

    public int AccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }
}

public class Notification
{
    public long Id { get; set; }

    public int AccountId { get; set; }

    public Enums.NotificationType Type { get; set; }

    public string Title { get; set; } = "";

    public string? Body { get; set; }

    public string? Link { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
