using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Exceptions;

namespace NursingHome.Services.Helpers;

/// <summary>Mốc cảnh báo chứng chỉ (UC-43).</summary>
public enum CertificateState
{
    /// <summary>Không có ngày hết hạn, hoặc còn hạn lâu.</summary>
    Valid,

    /// <summary>Còn ≤ 30 ngày.</summary>
    ExpiringSoon,

    /// <summary>Đã quá hạn.</summary>
    Expired
}

/// <summary>BL-V4: quy tắc chứng chỉ (UC-43), chấm công (UC-48), hiệu suất (UC-126). Hàm thuần để unit test.</summary>
public static class StaffHrRules
{
    public const int CertificateAlertDays = 30;

    /// <summary>Có thể vào ca từ trước giờ bắt đầu 30 phút tới hết ca.</summary>
    public const int CheckInEarlyMinutes = 30;

    /// <summary>Vào ca trễ quá 15 phút so với giờ bắt đầu thì tính đi muộn.</summary>
    public const int LateGraceMinutes = 15;

    /// <summary>Admin chỉ sửa công của 31 ngày gần nhất.</summary>
    public const int MaxEditAgeDays = 31;

    // ---------------- Chứng chỉ ----------------

    public static CertificateState StateOf(DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is not { } expires)
        {
            return CertificateState.Valid;
        }

        if (expires < today)
        {
            return CertificateState.Expired;
        }

        return expires <= today.AddDays(CertificateAlertDays) ? CertificateState.ExpiringSoon : CertificateState.Valid;
    }

    /// <summary>Số ngày còn lại (âm = đã quá hạn); null khi không có ngày hết hạn.</summary>
    public static int? DaysLeft(DateOnly? expiresOn, DateOnly today) => expiresOn is { } e ? e.DayNumber - today.DayNumber : null;

    /// <summary>
    /// Mốc cần báo ngay lúc này mà CHƯA báo: đã hết hạn mà chưa báo hết hạn; còn ≤ 30 ngày mà chưa báo mốc 30 ngày.
    /// Chứng chỉ đã quá hạn mà chưa từng báo mốc 30 ngày thì chỉ báo "đã hết hạn" (không báo hai lần liền).
    /// </summary>
    public static CertificateState? PendingAlert(StaffCertificate certificate, DateOnly today) => StateOf(certificate.ExpiresOn, today) switch
    {
        CertificateState.Expired when certificate.AlertExpiredAt is null => CertificateState.Expired,
        CertificateState.ExpiringSoon when certificate.Alert30At is null => CertificateState.ExpiringSoon,
        _ => null
    };

    public static void ValidateCertificate(string name, DateOnly? issuedOn, DateOnly? expiresOn, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("Nhập tên chứng chỉ.");
        }

        if (issuedOn is { } issued && expiresOn is { } expires && expires < issued)
        {
            throw new BusinessRuleException("Ngày hết hạn không được trước ngày cấp.");
        }

        if (issuedOn is { } future && future > today)
        {
            throw new BusinessRuleException("Ngày cấp không được ở tương lai.");
        }
    }

    // ---------------- Chấm công ----------------

    /// <summary>Có được vào ca lúc <paramref name="now"/> không: trong [giờ bắt đầu − 30 phút, giờ kết thúc ca].</summary>
    public static bool CanCheckIn(DateTime shiftStartUtc, DateTime shiftEndUtc, DateTime now) =>
        now >= shiftStartUtc.AddMinutes(-CheckInEarlyMinutes) && now <= shiftEndUtc;

    /// <summary>Số phút đi muộn: 0 nếu vào ca trong 15 phút đầu; ngược lại tính từ giờ bắt đầu ca (25 phút trễ = 25).</summary>
    public static int LateMinutes(DateTime shiftStartUtc, DateTime checkInAt)
    {
        double late = (checkInAt - shiftStartUtc).TotalMinutes;
        return late > LateGraceMinutes ? (int)Math.Round(late, MidpointRounding.AwayFromZero) : 0;
    }

    public static void ValidateEdit(DateTime shiftStartUtc, DateTime shiftEndUtc, DateTime? checkInAt, DateTime? checkOutAt, string? reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Nhập lý do khi sửa công.");
        }

        if (checkInAt is null)
        {
            throw new BusinessRuleException("Nhập giờ vào ca.");
        }

        if (checkOutAt is { } checkOut && checkOut < checkInAt)
        {
            throw new BusinessRuleException("Giờ ra ca không được trước giờ vào ca.");
        }

        if (checkInAt > now.AddMinutes(5) || checkOutAt > now.AddMinutes(5))
        {
            throw new BusinessRuleException("Giờ chấm công không được ở tương lai.");
        }

        if (shiftStartUtc < now.AddDays(-MaxEditAgeDays))
        {
            throw new BusinessRuleException($"Chỉ sửa được công trong {MaxEditAgeDays} ngày gần nhất.");
        }

        // Giờ vào/ra phải gần ca (± 12 giờ) để chặn gõ nhầm ngày.
        if (checkInAt < shiftStartUtc.AddHours(-12) || checkInAt > shiftEndUtc.AddHours(12))
        {
            throw new BusinessRuleException("Giờ vào ca lệch quá xa so với ca đã phân.");
        }
    }

    // ---------------- Hiệu suất ----------------

    /// <summary>% làm tròn 1 chữ số, tối đa 100; null khi mẫu số = 0.</summary>
    public static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Min(100m, Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero));
}
