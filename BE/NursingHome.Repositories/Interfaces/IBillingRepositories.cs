using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DataAccessObjects;

namespace NursingHome.Repositories.Interfaces;

public interface IBillingRepository
{
    Task<List<ServicePackage>> ListPackagesAsync(bool activeOnly);

    Task<ServicePackage?> GetPackageAsync(int id);

    Task<bool> PackageNameExistsAsync(string name, int? excludeId);

    Task<ServicePackage> AddPackageAsync(ServicePackage package);

    Task UpdatePackageAsync(ServicePackage package);

    Task<List<ResidentSubscription>> ListSubscriptionsAsync(int? residentId);

    Task<ResidentSubscription> AddSubscriptionAsync(int residentId, int packageId, DateOnly start, DateOnly? end);

    Task EndSubscriptionAsync(int id, DateOnly today, DateTime now);

    Task<List<ResidentSubscription>> SubscriptionsForPeriodAsync(int residentId, DateOnly start, DateOnly end);

    Task<List<Invoice>> ListInvoicesAsync(int? residentId, bool excludeDraft);

    Task<Invoice?> GetInvoiceAsync(int id);

    Task<Invoice> CreateInvoiceAsync(Invoice invoice);

    Task AddItemAsync(int invoiceId, InvoiceItem item, DateTime now);

    Task<Invoice> IssueAsync(int id, DateTime now);

    Task CancelAsync(int id, DateTime now);

    Task<PaymentApplyOutcome> ApplyPaymentAsync(Payment payment, long? transactionId, DateTime now);
}

public interface IPaymentRepository
{
    Task<PaymentTransaction?> GetOpenTransactionAsync(int invoiceId);

    Task<PaymentTransaction?> GetTransactionAsync(long id);

    Task<PaymentTransaction> AddTransactionAsync(PaymentTransaction transaction);

    Task UpdateTransactionAsync(PaymentTransaction transaction);

    Task<List<PaymentTransaction>> ListTransactionsAsync(PaymentTransactionStatus? status, int take);

    Task<List<PaymentTransaction>> ListPendingForSyncAsync(int take);

    Task SetStatusAsync(long id, PaymentTransactionStatus status, DateTime now);

    Task<PaymentWebhookLog> AddLogAsync(PaymentWebhookLog log);

    Task<List<PaymentWebhookLog>> ListLogsAsync(int take);

    Task<bool> AppliedEventExistsAsync(string providerEventId);
}

public interface IDashboardRepository
{
    Task<DashboardStats> GetStatsAsync(DateTime dayStartUtc, DateTime dayEndUtc);

    Task<List<MonthlyAmount>> CollectedByMonthAsync(DateTime fromUtc);

    Task<(int AvailableBeds, int TotalBeds)> GetPublicBedStatsAsync();
}
