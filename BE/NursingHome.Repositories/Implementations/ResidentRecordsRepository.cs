using NursingHome.BusinessObjects.Entities;
using NursingHome.DataAccessObjects;
using NursingHome.Repositories.Interfaces;

namespace NursingHome.Repositories.Implementations;

public class ResidentRecordsRepository : IResidentRecordsRepository
{
    private static ResidentRecordsDAO Dao => ResidentRecordsDAO.Instance;

    public Task<List<ResidentDocument>> ListDocumentsAsync(int residentId) => Dao.ListDocumentsAsync(residentId);

    public Task<ResidentDocument?> GetDocumentAsync(int id) => Dao.GetDocumentAsync(id);

    public Task<ResidentDocument> AddDocumentAsync(ResidentDocument document) => Dao.AddDocumentAsync(document);

    public Task<bool> DeleteDocumentAsync(int id) => Dao.DeleteDocumentAsync(id);

    public Task<List<ResidentContract>> ListContractsAsync(int residentId) => Dao.ListContractsAsync(residentId);

    public Task<ResidentContract?> GetContractAsync(int id) => Dao.GetContractAsync(id);

    public Task<ResidentContract> AddContractAsync(ResidentContract contract) => Dao.AddContractAsync(contract);

    public Task<bool> EndContractAsync(int id, DateOnly endDate, DateTime now) => Dao.EndContractAsync(id, endDate, now);

    public Task<List<ResidentLeave>> ListLeavesAsync(int residentId) => Dao.ListLeavesAsync(residentId);

    public Task<List<ResidentLeave>> ListOpenLeavesAsync() => Dao.ListOpenLeavesAsync();

    public Task<ResidentLeave> StartLeaveAsync(ResidentLeave leave) => Dao.StartLeaveAsync(leave);

    public Task<bool> ReturnFromLeaveAsync(int leaveId, int residentId, int byAccountId, DateTime returnedAt) =>
        Dao.ReturnFromLeaveAsync(leaveId, residentId, byAccountId, returnedAt);

    public Task<List<ResidentStay>> ListStaysAsync(int residentId) => Dao.ListStaysAsync(residentId);

    public Task ReadmitAsync(int residentId, int bedId, int packageId, decimal? agreedPrice, DateOnly startDate, DateOnly dischargedOn, int byAccountId, DateTime now) =>
        Dao.ReadmitAsync(residentId, bedId, packageId, agreedPrice, startDate, dischargedOn, byAccountId, now);

    public Task<List<Deposit>> ListDepositsAsync(int residentId) => Dao.ListDepositsAsync(residentId);

    public Task<Deposit?> GetDepositAsync(int id) => Dao.GetDepositAsync(id);

    public Task<Deposit> AddDepositAsync(Deposit deposit) => Dao.AddDepositAsync(deposit);

    public Task RefundDepositAsync(int id, decimal amount, string? note, int byAccountId, DateTime now) =>
        Dao.RefundDepositAsync(id, amount, note, byAccountId, now);

    public Task<decimal> OutstandingAsync(int residentId) => Dao.OutstandingAsync(residentId);
}
