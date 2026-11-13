using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.BusinessObjects.Exceptions;
using NursingHome.BusinessObjects.Helpers;
using NursingHome.DataAccessObjects;
using NursingHome.DTOs.Billing;
using NursingHome.DTOs.Common;
using NursingHome.Repositories.Interfaces;
using NursingHome.Services.Helpers;
using NursingHome.Services.Interfaces;

namespace NursingHome.Services.Implementations;

/// <summary>
/// UC-83..UC-92: gói dịch vụ, đăng ký gói (giá chốt), hóa đơn tháng tính theo ngày, phát hành, thu tiền thủ công.
/// Tiền VND làm tròn tới đồng (DECIMAL(18,0)).
/// </summary>
public class BillingService(
    IBillingRepository billing,
    IResidentRepository residents,
    IResidentAccessService access,
    INotificationService notifications,
    FacilityClock clock) : IBillingService
{
    // ---------------- Gói dịch vụ ----------------

    public async Task<List<ServicePackageDto>> ListPackagesAsync(bool activeOnly) =>
        (await billing.ListPackagesAsync(activeOnly)).Select(package => package.ToDto()).ToList();

    public async Task<ServicePackageDto> SavePackageAsync(int? id, SavePackageRequest request)
    {
        EnsureWholeVnd(request.MonthlyPrice, "Giá gói");
        string name = request.Name.Trim();
        if (await billing.PackageNameExistsAsync(name, id))
        {
            throw new ConflictException("Tên gói đã tồn tại.");
        }

        if (id.HasValue)
        {
            // Đổi giá danh mục KHÔNG ảnh hưởng đăng ký đã chốt và hóa đơn cũ.
            var package = await billing.GetPackageAsync(id.Value) ?? throw new NotFoundException("Không tìm thấy gói.");
            package.Name = name;
            package.MonthlyPrice = request.MonthlyPrice;
            package.Description = request.Description?.Trim();
            package.IsActive = request.IsActive;
            package.UpdatedAt = clock.UtcNow;
            await billing.UpdatePackageAsync(package);
            return package.ToDto();
        }

        var created = await billing.AddPackageAsync(new ServicePackage
        {
            Name = name,
            MonthlyPrice = request.MonthlyPrice,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = clock.UtcNow
        });
        return created.ToDto();
    }

    // ---------------- Đăng ký gói ----------------

    public async Task<List<SubscriptionDto>> ListSubscriptionsAsync(int? residentId) =>
        (await billing.ListSubscriptionsAsync(residentId)).Select(item => item.ToDto()).ToList();

    public async Task<SubscriptionDto> SubscribeAsync(CreateSubscriptionRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new BusinessRuleException("Ngày kết thúc phải sau ngày bắt đầu.");
        }

        var subscription = await billing.AddSubscriptionAsync(request.ResidentId, request.ServicePackageId, request.StartDate, request.EndDate);
        return (await billing.ListSubscriptionsAsync(request.ResidentId)).First(item => item.Id == subscription.Id).ToDto();
    }

    public Task EndSubscriptionAsync(int id) => billing.EndSubscriptionAsync(id, clock.Today, clock.UtcNow);

    // ---------------- Hóa đơn ----------------

    public async Task<List<InvoiceListItemDto>> ListInvoicesAsync(int? residentId) =>
        (await billing.ListInvoicesAsync(residentId, excludeDraft: false)).Select(invoice => invoice.ToListItem()).ToList();

    public async Task<InvoiceDetailDto> GetInvoiceAsync(CurrentUser user, int id)
    {
        var invoice = await FindAsync(id);
        if (user.IsInRole(Roles.Family))
        {
            await access.RequireAsync(user, invoice.ResidentId, ResidentSection.Billing);
            if (invoice.Status == InvoiceStatus.Draft)
            {
                throw new NotFoundException("Không tìm thấy hóa đơn.");
            }
        }

        return invoice.ToDetail();
    }

    /// <summary>
    /// Hóa đơn tháng: mỗi gói giao với kỳ được tính theo số ngày (tính cả ngày đầu/cuối),
    /// giới hạn bởi ngày nhập viện/ngừng lưu trú. Kỳ là ngày 1 của tháng.
    /// </summary>
    public async Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        if (request.BillingPeriod.Day != 1 || request.DueDate < request.BillingPeriod)
        {
            throw new BusinessRuleException("Kỳ hóa đơn phải là ngày đầu tháng và hạn thanh toán không được trước kỳ.");
        }

        var resident = await residents.GetByIdAsync(request.ResidentId) ?? throw new NotFoundException("Không tìm thấy cư dân.");
        var periodEnd = request.BillingPeriod.AddMonths(1).AddDays(-1);
        var admitted = FacilityClock.ToLocalDate(resident.AdmittedAt);
        DateOnly? discharged = resident.DischargedAt.HasValue ? FacilityClock.ToLocalDate(resident.DischargedAt.Value) : null;

        var invoice = new Invoice
        {
            ResidentId = resident.Id,
            Number = $"INV-{request.BillingPeriod:yyyyMM}-{resident.Id:D5}",
            BillingPeriod = request.BillingPeriod,
            DueDate = request.DueDate,
            Status = InvoiceStatus.Draft,
            CreatedAt = clock.UtcNow
        };
        foreach (var subscription in await billing.SubscriptionsForPeriodAsync(resident.Id, request.BillingPeriod, periodEnd))
        {
            var charge = InvoiceMath.ProrateMonth(request.BillingPeriod, subscription.StartDate, subscription.EndDate,
                admitted, discharged, subscription.AgreedMonthlyPrice);
            if (charge is { } line)
            {
                invoice.Items.Add(new InvoiceItem
                {
                    Description = $"{subscription.ServicePackage.Name}: {line.Start:dd/MM}–{line.End:dd/MM} ({line.Days} ngày)",
                    Quantity = 1,
                    UnitPrice = line.Amount,
                    CreatedAt = clock.UtcNow
                });
            }
        }

        if (invoice.Items.Count == 0)
        {
            throw new BusinessRuleException("Không có đăng ký gói và ngày lưu trú phù hợp trong kỳ này.");
        }

        var created = await billing.CreateInvoiceAsync(invoice);
        return (await FindAsync(created.Id)).ToDetail();
    }

    public async Task<InvoiceDetailDto> AddItemAsync(int id, AddInvoiceItemRequest request)
    {
        EnsureWholeVnd(request.UnitPrice, "Đơn giá");
        if (decimal.Round(request.Quantity, 2) != request.Quantity)
        {
            throw new BusinessRuleException("Số lượng có tối đa 2 chữ số thập phân.");
        }

        await billing.AddItemAsync(id, new InvoiceItem
        {
            Description = request.Description.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            CreatedAt = clock.UtcNow
        }, clock.UtcNow);
        return (await FindAsync(id)).ToDetail();
    }

    public async Task<InvoiceDetailDto> IssueAsync(int id)
    {
        var issued = await billing.IssueAsync(id, clock.UtcNow);
        var familyIds = await residents.FamilyAccountIdsAsync(issued.ResidentId, requireBilling: true);
        await notifications.NotifyAsync(familyIds, NotificationType.Invoice,
            $"Hóa đơn {issued.Number} đã phát hành",
            $"Tổng {InvoiceMath.Total(issued.Items):N0} đ, hạn thanh toán {issued.DueDate:dd/MM/yyyy}.",
            $"/family/invoices/{issued.Id}");
        return (await FindAsync(id)).ToDetail();
    }

    public async Task<InvoiceDetailDto> RecordPaymentAsync(CurrentUser user, int id, RecordPaymentRequest request)
    {
        EnsureWholeVnd(request.Amount, "Số tiền");
        if (!Enum.TryParse<PaymentMethod>(request.Method, out var method) || method == PaymentMethod.PayOS)
        {
            throw new BusinessRuleException("Phương thức thu thủ công là Cash hoặc BankTransfer. PayOS do hệ thống tự ghi.");
        }

        var outcome = await billing.ApplyPaymentAsync(new Payment
        {
            InvoiceId = id,
            Amount = request.Amount,
            Method = method,
            Reference = request.Reference.Trim(),
            RecordedById = user.AccountId,
            CreatedAt = clock.UtcNow
        }, null, clock.UtcNow);

        return outcome.Result switch
        {
            PaymentApplyResult.Applied => (await FindAsync(id)).ToDetail(),
            PaymentApplyResult.Duplicate => throw new ConflictException("Mã tham chiếu thanh toán đã tồn tại."),
            PaymentApplyResult.InvalidState => throw new BusinessRuleException("Hóa đơn không ở trạng thái chờ thanh toán."),
            _ => throw new BusinessRuleException($"Số tiền vượt công nợ còn lại ({outcome.Remaining:N0} đ).")
        };
    }

    public Task CancelAsync(int id) => billing.CancelAsync(id, clock.UtcNow);

    private async Task<Invoice> FindAsync(int id) =>
        await billing.GetInvoiceAsync(id) ?? throw new NotFoundException("Không tìm thấy hóa đơn.");

    private static void EnsureWholeVnd(decimal amount, string field)
    {
        if (amount < 0 || decimal.Round(amount, 0) != amount)
        {
            throw new BusinessRuleException($"{field} phải là số đồng nguyên, không âm.");
        }
    }
}
