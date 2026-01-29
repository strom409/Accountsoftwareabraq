using AbraqAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Interfaces;

public interface IPaymentSettlementService
{
    Task<(List<PaymentSettlementGroupViewModel> groups, int totalCount, int totalPages)> GetSettlementsAsync(
        string? paNumber,
        DateTime? fromDate,
        DateTime? toDate,
        string? vendorGroup,
        string? vendorName,
        string? unit,
        string? approvalStatus,
        string? paymentStatus,
        int page,
        int pageSize);

    Task<(bool success, string message)> CreateMultipleSettlementsAsync(PaymentSettlementBatchModel model);
    Task<(bool success, string message)> DeleteSettlementAsync(int id);
    Task<(bool success, string message)> ApproveSettlementAsync(int id);
    Task<(bool success, string message)> UnapproveSettlementAsync(int id);
    Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? paymentFromId = null, string? type = null);
    Task<IEnumerable<LookupItem>> GetEntryProfilesAsync();
    Task<PaymentSettlement?> GetSettlementByIdAsync(int id);
    Task<List<PaymentSettlement>> GetSettlementEntriesByPANumberAsync(string paNumber);
    Task<(bool success, string message)> UpdateSettlementAsync(PaymentSettlementBatchModel model, string paNumber);
    Task<object?> GetPADetailsAsync(string paNumber);
    
    Task LoadDropdownsAsync(dynamic viewBag);
}

