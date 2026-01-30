using AbraqAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Interfaces;

public interface IReceiptEntryService
{
    Task<(List<ReceiptEntryGroupViewModel> groups, int totalCount, int totalPages)> GetReceiptEntriesAsync(
        string? voucherNo, 
        string? growerGroup, 
        string? growerName,
        string? unit,
        string? status, 
        DateTime? fromDate, 
        DateTime? toDate, 
        int page, 
        int pageSize);

    Task<(bool success, string message)> CreateMultipleReceiptsAsync(ReceiptEntryBatchModel model);
    Task<(bool success, object? data, string? error)> GetVoucherDetailsAsync(string voucherNo);
    Task<(bool success, string message)> DeleteReceiptEntryAsync(int id);
    Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? paymentFromId = null, string? type = null);
    Task<IEnumerable<LookupItem>> GetEntryProfilesAsync();
    Task LoadDropdownsAsync(dynamic viewBag);
    
    // New methods for Edit and Details
    Task<ReceiptEntry?> GetReceiptEntryByIdAsync(int id);
    Task<List<ReceiptEntry>> GetReceiptEntriesByVoucherNoAsync(string voucherNo);
    Task<(bool success, string message)> UpdateReceiptEntryAsync(ReceiptEntry model);
    Task<(bool success, string message)> UpdateReceiptVoucherAsync(string voucherNo, ReceiptEntryBatchModel model);
    Task<string> GetAccountNameAsync(int accountId, string accountType);
    Task<(bool success, string message)> ApproveReceiptEntryAsync(int id);
    Task<(bool success, string message)> UnapproveReceiptEntryAsync(int id);
    Task<List<string>> GetUnitNamesAsync();
}

