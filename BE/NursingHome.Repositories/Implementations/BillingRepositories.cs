using NursingHome.BusinessObjects.Entities;
using NursingHome.BusinessObjects.Enums;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class BillingRepository : IBillingRepository
{
    private static BillingDAO Dao => BillingDAO.Instance;

    public Task<List<ServicePackage>> ListPackagesAsync(bool activeOnly) => Dao.ListPackagesAsync(activeOnly);

    public Task<ServicePackage?> GetPackageAsync(int id) => Dao.GetPackageAsync(id);

    public Task<bool> PackageNameExistsAsync(string name, int? excludeId) => Dao.PackageNameExistsAsync(name, excludeId);

    public Task<ServicePackage> AddPackageAsync(ServicePackage package) => Dao.AddPackageAsync(package);

    public Task UpdatePackageAsync(ServicePackage package) => Dao.UpdatePackageAsync(package);

    public Task<List<ResidentSubscription>> ListSubscriptionsAsync(int? residentId) => Dao.ListSubscriptionsAsync(residentId);

    public Task<ResidentSubscription> AddSubscriptionAsync(int residentId, int packageId, DateOnly start, DateOnly? end) =>
        Dao.AddSubscriptionAsync(residentId, packageId, start, end);

    public Task EndSubscriptionAsync(int id, DateOnly today, DateTime now) => Dao.EndSubscriptionAsync(id, today, now);

    public Task<List<ResidentSubscription>> SubscriptionsForPeriodAsync(int residentId, DateOnly start, DateOnly end) =>
        Dao.SubscriptionsForPeriodAsync(residentId, start, end);

    public Task<List<Invoice>> ListInvoicesAsync(int? residentId, bool excludeDraft) => Dao.ListInvoicesAsync(residentId, excludeDraft);

    public Task<Invoice?> GetInvoiceAsync(int id) => Dao.GetInvoiceAsync(id);

    public Task<Invoice> CreateInvoiceAsync(Invoice invoice) => Dao.CreateInvoiceAsync(invoice);

    public Task AddItemAsync(int invoiceId, InvoiceItem item, DateTime now) => Dao.AddItemAsync(invoiceId, item, now);

    public Task<Invoice> IssueAsync(int id, DateTime now) => Dao.IssueAsync(id, now);

    public Task CancelAsync(int id, DateTime now) => Dao.CancelAsync(id, now);

    public Task<PaymentApplyOutcome> ApplyPaymentAsync(Payment payment, long? transactionId, DateTime now) =>
        Dao.ApplyPaymentAsync(payment, transactionId, now);
}

public class PaymentRepository : IPaymentRepository
{
    private static PaymentDAO Dao => PaymentDAO.Instance;

    public Task<PaymentTransaction?> GetOpenTransactionAsync(int invoiceId) => Dao.GetOpenTransactionAsync(invoiceId);

    public Task<PaymentTransaction?> GetTransactionAsync(long id) => Dao.GetTransactionAsync(id);

    public Task<PaymentTransaction> AddTransactionAsync(PaymentTransaction transaction) => Dao.AddAsync(transaction);

    public Task UpdateTransactionAsync(PaymentTransaction transaction) => Dao.UpdateAsync(transaction);

    public Task<List<PaymentTransaction>> ListTransactionsAsync(PaymentTransactionStatus? status, int take) => Dao.ListTransactionsAsync(status, take);

    public Task<List<PaymentTransaction>> ListPendingForSyncAsync(int take) => Dao.ListPendingForSyncAsync(take);

    public Task SetStatusAsync(long id, PaymentTransactionStatus status, DateTime now) => Dao.SetStatusAsync(id, status, now);

    public Task<PaymentWebhookLog> AddLogAsync(PaymentWebhookLog log) => Dao.AddLogAsync(log);

    public Task<List<PaymentWebhookLog>> ListLogsAsync(int take) => Dao.ListLogsAsync(take);

    public Task<bool> AppliedEventExistsAsync(string providerEventId) => Dao.AppliedEventExistsAsync(providerEventId);
}

public class DashboardRepository : IDashboardRepository
{
    private static DashboardDAO Dao => DashboardDAO.Instance;

    public Task<DashboardStats> GetStatsAsync(DateTime dayStartUtc, DateTime dayEndUtc) => Dao.GetStatsAsync(dayStartUtc, dayEndUtc);

    public Task<List<MonthlyAmount>> CollectedByMonthAsync(DateTime fromUtc) => Dao.CollectedByMonthAsync(fromUtc);

    public Task<(int AvailableBeds, int TotalBeds)> GetPublicBedStatsAsync() => Dao.GetPublicBedStatsAsync();
}
