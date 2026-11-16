using NursingHome.BusinessObjects.Entities;

namespace NursingHome.Repositories.Interfaces;

/// <summary>BL-V2 hồ sơ lưu trú: giấy tờ, hợp đồng, tạm vắng, tái nhập viện, tiền cọc.</summary>
public interface IResidentRecordsRepository
{
    Task<List<ResidentDocument>> ListDocumentsAsync(int residentId);

    Task<ResidentDocument?> GetDocumentAsync(int id);

    Task<ResidentDocument> AddDocumentAsync(ResidentDocument document);

    Task<bool> DeleteDocumentAsync(int id);

    Task<List<ResidentContract>> ListContractsAsync(int residentId);

    Task<ResidentContract?> GetContractAsync(int id);

    Task<ResidentContract> AddContractAsync(ResidentContract contract);

    Task<bool> EndContractAsync(int id, DateOnly endDate, DateTime now);

    Task<List<ResidentLeave>> ListLeavesAsync(int residentId);

    Task<List<ResidentLeave>> ListOpenLeavesAsync();

    Task<ResidentLeave> StartLeaveAsync(ResidentLeave leave);

    Task<bool> ReturnFromLeaveAsync(int leaveId, int residentId, int byAccountId, DateTime returnedAt);

    Task<List<ResidentStay>> ListStaysAsync(int residentId);

    Task ReadmitAsync(int residentId, int bedId, int packageId, decimal? agreedPrice, DateOnly startDate, DateOnly dischargedOn, int byAccountId, DateTime now);

    Task<List<Deposit>> ListDepositsAsync(int residentId);

    Task<Deposit?> GetDepositAsync(int id);

    Task<Deposit> AddDepositAsync(Deposit deposit);

    Task RefundDepositAsync(int id, decimal amount, string? note, int byAccountId, DateTime now);

    Task<decimal> OutstandingAsync(int residentId);
}
