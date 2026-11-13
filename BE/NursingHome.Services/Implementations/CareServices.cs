using Microsoft.Extensions.Options;
using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.DTOs.Care;
using NursingHome.DTOs.Common;
using NursingHome.DTOs.Medication;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;
using NursingHome.Services.Options;

namespace NursingHome.Services.Implementations;

/// <summary>UC-50..UC-55: công việc chăm sóc. Pending → InProgress/Completed; chỉ Admin được Cancelled.</summary>
public class CareTaskService(
    ICareRepository care,
    IResidentRepository residents,
    IAccountRepository accounts,
    IResidentAccessService access,
    INotificationService notifications,
    FacilityClock clock) : ICareTaskService
{
    public async Task<List<CareTaskDto>> ListAsync(CurrentUser user, DateOnly? date, bool mine, int? residentId)
    {
        if (residentId.HasValue)
        {
            await access.RequireAsync(user, residentId.Value, ResidentSection.Care);
        }

        // Caregiver luôn chỉ thấy việc của mình; Nurse/Admin xem tất cả hoặc lọc "của tôi".
        int? assignee = user.IsInRole(Roles.Caregiver) || mine ? user.AccountId : null;
        DateTime? from = date.HasValue ? clock.StartOfDayUtc(date.Value) : null;
        DateTime? to = from?.AddDays(1);
        return (await care.ListTasksAsync(assignee, residentId, from, to)).Select(task => task.ToDto()).ToList();
    }

    public async Task<CareTaskDto> GetAsync(CurrentUser user, int id) => (await FindVisibleAsync(user, id)).ToDto();

    public async Task<CareTaskDto> CreateAsync(CurrentUser user, CreateCareTaskRequest request)
    {
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Vui lòng chọn cư dân đang lưu trú.");
        }

        if (!await accounts.IsActiveInRolesAsync(request.AssignedToId, Roles.CareAssignees))
        {
            throw new BusinessRuleException("Người được phân công phải là Nurse hoặc Caregiver đang hoạt động.");
        }

        var task = await care.AddTaskAsync(new CareTask
        {
            ResidentId = request.ResidentId,
            AssignedToId = request.AssignedToId,
            CreatedById = user.AccountId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions?.Trim(),
            DueAt = clock.ToUtc(request.DueDate, request.DueTime),
            Status = CareTaskStatus.Pending,
            CreatedAt = clock.UtcNow
        });

        await notifications.NotifyAsync([request.AssignedToId], NotificationType.Care,
            "Bạn có công việc chăm sóc mới", $"{task.Title} — hạn {request.DueDate:dd/MM} {request.DueTime:HH\\:mm}", "/staff/care-tasks");
        return (await care.GetTaskAsync(task.Id))!.ToDto();
    }

    public async Task<CareTaskDto> UpdateAsync(CurrentUser user, int id, UpdateCareTaskRequest request)
    {
        var task = await FindVisibleAsync(user, id);
        if (!Enum.TryParse<CareTaskStatus>(request.Status, out var status) ||
            status is not (CareTaskStatus.InProgress or CareTaskStatus.Completed or CareTaskStatus.Cancelled))
        {
            throw new BusinessRuleException("Chọn trạng thái InProgress, Completed hoặc Cancelled.");
        }

        if (status == CareTaskStatus.Cancelled && !user.IsInRole(Roles.Admin))
        {
            throw new ForbiddenException("Chỉ Admin được hủy công việc.");
        }

        bool canUpdate = task.AssignedToId == user.AccountId || user.IsInRole(Roles.Admin, Roles.Nurse);
        if (!canUpdate)
        {
            throw new ForbiddenException("Bạn không được giao công việc này.");
        }

        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.");
        }

        await care.UpdateStatusAsync(id, rowVersion, status, new CareRecord
        {
            Status = status,
            Result = request.Result.Trim(),
            ShareWithFamily = request.ShareWithFamily,
            RecordedById = user.AccountId,
            PerformedAt = clock.UtcNow,
            CreatedAt = clock.UtcNow
        }, clock.UtcNow);
        return (await care.GetTaskAsync(id))!.ToDto();
    }

    public async Task<List<CareRecordDto>> ListRecordsAsync(CurrentUser user, int id)
    {
        await FindVisibleAsync(user, id);
        return (await care.ListRecordsAsync(id)).Select(record => record.ToDto()).ToList();
    }

    private async Task<CareTask> FindVisibleAsync(CurrentUser user, int id)
    {
        var task = await care.GetTaskAsync(id) ?? throw new NotFoundException("Không tìm thấy công việc.");
        if (user.IsInRole(Roles.Caregiver) && task.AssignedToId != user.AccountId)
        {
            throw new NotFoundException("Không tìm thấy công việc.");
        }

        if (!user.IsInRole(Roles.Caregiver))
        {
            await access.RequireAsync(user, task.ResidentId, ResidentSection.Care);
        }

        return task;
    }
}

/// <summary>UC-62, UC-63, UC-65: sinh hiệu + cảnh báo khi vượt ngưỡng mặc định của cơ sở.</summary>
public class HealthService(
    IHealthRepository health,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    IOptions<FacilityOptions> facility,
    FacilityClock clock) : IHealthService
{
    public async Task<List<VitalSignDto>> ListAsync(CurrentUser user, int residentId, DateOnly? from, DateOnly? to)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Health);
        DateTime? fromUtc = from.HasValue ? clock.StartOfDayUtc(from.Value) : null;
        DateTime? toUtc = to.HasValue ? clock.StartOfDayUtc(to.Value).AddDays(1) : null;
        return (await health.ListAsync(residentId, fromUtc, toUtc, sharedOnly: user.IsInRole(Roles.Family), take: 500))
            .Select(item => item.ToDto()).ToList();
    }

    public async Task<VitalSignDto> RecordAsync(CurrentUser user, RecordVitalSignRequest request)
    {
        await access.RequireAsync(user, request.ResidentId, ResidentSection.Health);
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân đã ngừng lưu trú.");
        }

        if (request.Systolic <= request.Diastolic)
        {
            throw new BusinessRuleException("Huyết áp tâm thu phải lớn hơn tâm trương.");
        }

        var saved = await health.AddAsync(new VitalSign
        {
            ResidentId = request.ResidentId,
            Systolic = request.Systolic,
            Diastolic = request.Diastolic,
            HeartRate = request.HeartRate,
            Temperature = request.Temperature,
            SpO2 = request.SpO2,
            Notes = request.Notes?.Trim(),
            ShareWithFamily = request.ShareWithFamily,
            RecordedById = user.AccountId,
            RecordedAt = clock.UtcNow,
            CreatedAt = clock.UtcNow
        });

        var alerts = OutOfRange(request, facility.Value.VitalAlerts);
        if (alerts.Count > 0)
        {
            var resident = await residents.GetByIdAsync(request.ResidentId);
            await notifications.NotifyRolesAsync([Roles.Nurse, Roles.Doctor], NotificationType.Health,
                $"Sinh hiệu cần xem lại: {resident?.FullName}",
                $"{string.Join("; ", alerts)}. Ngưỡng cảnh báo của cơ sở, không phải chẩn đoán.",
                $"/staff/residents/{request.ResidentId}");
        }

        var dto = saved.ToDto();
        dto.RecordedByName = user.Name;
        return dto;
    }

    public static List<string> OutOfRange(RecordVitalSignRequest vital, VitalAlertOptions limits)
    {
        var alerts = new List<string>();
        if (vital.Systolic >= limits.SystolicHigh || vital.Systolic <= limits.SystolicLow)
        {
            alerts.Add($"Huyết áp {vital.Systolic}/{vital.Diastolic}");
        }

        if (vital.HeartRate >= limits.HeartRateHigh || vital.HeartRate <= limits.HeartRateLow)
        {
            alerts.Add($"Mạch {vital.HeartRate}");
        }

        if (vital.Temperature >= limits.TemperatureHigh)
        {
            alerts.Add($"Nhiệt độ {vital.Temperature}°C");
        }

        if (vital.SpO2.HasValue && vital.SpO2 < limits.SpO2Low)
        {
            alerts.Add($"SpO2 {vital.SpO2}%");
        }

        return alerts;
    }
}

/// <summary>
/// UC-73..UC-78: lịch thuốc. Doctor/Admin nhập lịch theo chỉ định (có số tham chiếu); thay đổi = dừng lịch cũ
/// và nhập lịch mới. Nurse/Caregiver chỉ ghi nhận Taken/Missed/Refused. Không kê đơn, không đề xuất liều.
/// </summary>
public class MedicationService(
    IMedicationRepository medications,
    IResidentRepository residents,
    IResidentAccessService access,
    FacilityClock clock) : IMedicationService
{
    public async Task<List<MedicationScheduleDto>> ListSchedulesAsync(CurrentUser user, int? residentId)
    {
        if (residentId.HasValue)
        {
            await access.RequireAsync(user, residentId.Value, ResidentSection.Medication);
        }

        var visible = await access.VisibleResidentIdsAsync(user);
        return (await medications.ListSchedulesAsync(residentId, visible)).Select(item => item.ToDto()).ToList();
    }

    public async Task<MedicationScheduleDto> CreateScheduleAsync(CurrentUser user, CreateMedicationScheduleRequest request)
    {
        if (await residents.GetStatusAsync(request.ResidentId) != ResidentStatus.Active)
        {
            throw new BusinessRuleException("Cư dân phải đang lưu trú.");
        }

        if (request.EndDate < request.StartDate)
        {
            throw new BusinessRuleException("Ngày kết thúc phải sau ngày bắt đầu.");
        }

        var schedule = await medications.AddScheduleAsync(new MedicationSchedule
        {
            ResidentId = request.ResidentId,
            MedicationName = request.MedicationName.Trim(),
            Dose = request.Dose.Trim(),
            Route = request.Route.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ScheduledTime = new TimeOnly(request.ScheduledTime.Hour, request.ScheduledTime.Minute),
            ApprovalReference = request.ApprovalReference.Trim(),
            Instructions = request.Instructions?.Trim(),
            EnteredById = user.AccountId,
            IsActive = true,
            CreatedAt = clock.UtcNow
        });
        var dto = schedule.ToDto();
        dto.ResidentName = (await residents.GetByIdAsync(request.ResidentId))?.FullName ?? "";
        dto.EnteredByName = user.Name;
        return dto;
    }

    public Task StopAsync(int id) => medications.StopAsync(id, clock.UtcNow);

    public async Task<List<MedicationDoseDto>> TodayAsync(CurrentUser user, DateOnly? date)
    {
        var day = date ?? clock.Today;
        var visible = await access.VisibleResidentIdsAsync(user);
        var schedules = await medications.ActiveForDateAsync(day, visible);
        var start = clock.StartOfDayUtc(day);
        var logs = await medications.LogsBetweenAsync(schedules.Select(item => item.Id).ToList(), start, start.AddDays(1));
        var now = clock.UtcNow;

        return schedules.Select(schedule =>
        {
            var scheduledAt = clock.ToUtc(day, schedule.ScheduledTime);
            var log = logs.FirstOrDefault(item => item.MedicationScheduleId == schedule.Id && item.ScheduledAt == scheduledAt);
            return new MedicationDoseDto
            {
                ScheduleId = schedule.Id,
                ResidentId = schedule.ResidentId,
                ResidentName = schedule.Resident.FullName,
                ResidentCode = schedule.Resident.Code,
                MedicationName = schedule.MedicationName,
                Dose = schedule.Dose,
                Route = schedule.Route,
                Instructions = schedule.Instructions,
                ScheduledTime = schedule.ScheduledTime,
                ScheduledAt = scheduledAt,
                Status = log?.Status.ToString() ?? nameof(MedicationStatus.Scheduled),
                RecordedAt = log?.RecordedAt,
                RecordedByName = log?.RecordedBy?.FullName,
                Notes = log?.Notes,
                CanRecord = log is null && scheduledAt <= now
            };
        }).ToList();
    }

    public async Task RecordAsync(CurrentUser user, RecordMedicationRequest request)
    {
        var schedule = await medications.GetScheduleAsync(request.ScheduleId) ?? throw new NotFoundException("Không tìm thấy lịch thuốc.");
        await access.RequireAsync(user, schedule.ResidentId, ResidentSection.Medication);
        var scheduledAt = clock.ToUtc(request.Date, schedule.ScheduledTime);
        if (!schedule.IsActive || schedule.Resident.Status != ResidentStatus.Active || request.Date < schedule.StartDate ||
            (schedule.EndDate.HasValue && request.Date > schedule.EndDate) || scheduledAt > clock.UtcNow)
        {
            throw new BusinessRuleException("Không thể ghi nhận lịch đã dừng, ngoài thời hạn hoặc chưa đến giờ dùng.");
        }

        if (!Enum.TryParse<MedicationStatus>(request.Status, out var status) ||
            status is not (MedicationStatus.Taken or MedicationStatus.Missed or MedicationStatus.Refused))
        {
            throw new BusinessRuleException("Chọn Taken, Missed hoặc Refused.");
        }

        if (status != MedicationStatus.Taken && string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new BusinessRuleException("Cần ghi lý do chưa dùng hoặc từ chối.");
        }

        if (await medications.LogExistsAsync(schedule.Id, scheduledAt))
        {
            throw new ConflictException("Lần dùng thuốc này đã được ghi nhận, không ghi đè lịch sử.");
        }

        await medications.AddLogAsync(new MedicationLog
        {
            MedicationScheduleId = schedule.Id,
            ScheduledAt = scheduledAt,
            RecordedAt = clock.UtcNow,
            RecordedById = user.AccountId,
            Status = status,
            Notes = request.Notes?.Trim(),
            CreatedAt = clock.UtcNow
        });
    }

    public async Task<List<MedicationLogDto>> HistoryAsync(CurrentUser user, int residentId)
    {
        await access.RequireAsync(user, residentId, ResidentSection.Medication);
        return (await medications.ListLogsForResidentAsync(residentId, 300)).Select(item => item.ToDto()).ToList();
    }
}
