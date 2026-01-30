using Microsoft.AspNetCore.Http;
using AbraqAccount.Models;

namespace AbraqAccount.Services.Interfaces;

public interface IGeneralEntryService
{
    Task<(List<GeneralEntry> entries, int totalCount, int totalPages)> GetGeneralEntriesAsync(
        string? voucherNo,
        DateTime? fromDate,
        DateTime? toDate,
        string? debitAccount,
        string? creditAccount,
        string? type,
        string? unit,
        string? status,
        int page,
        int pageSize);

    Task<(List<GeneralEntryGroupViewModel> groups, int totalCount, int totalPages)> GetJournalGroupsAsync(
        string? unit,
        string? noteNo,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize);

    Task<(bool success, string message)> CreateGeneralEntryAsync(GeneralEntry generalEntry, IFormFile? imageFile);
    Task<(bool success, string message)> CreateMultipleEntriesAsync(GeneralEntryBatchModel model);
    Task<(bool success, string message)> ApproveEntryAsync(int id);
    Task<(bool success, string message)> UnapproveEntryAsync(int id);
    Task<(bool success, string message)> DeleteEntryAsync(int id);
    Task<IEnumerable<object>> GetSubGroupLedgersAsync();
    Task<GeneralEntry?> GetEntryByIdAsync(int id);
    Task<List<GeneralEntry>> GetVoucherEntriesAsync(string voucherNo);
    Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? paymentFromId = null, string? type = null);
    Task<IEnumerable<object>> GetExpenseGroupsAsync(string? searchTerm);
    Task<IEnumerable<object>> GetExpenseSubGroupsAsync(int? groupId, string? searchTerm);
    Task<IEnumerable<object>> GetVendorGroupsAsync(string? searchTerm);
    Task<List<string>> GetUniqueTypesAsync();
    
    Task<(bool success, string message)> UpdateVoucherAsync(string voucherNo, GeneralEntryBatchModel model);
    Task<IEnumerable<LookupItem>> GetEntryProfilesAsync(string transactionType);

    // Admin/Utility
    Task<(bool success, string message)> AddTypeColumnAsync();

    // Ledger Report
    Task<IEnumerable<object>> GetSubGroupLedgersAsync(int? masterSubGroupId, string? searchTerm);
    Task<IEnumerable<object>> GetAccountsByGroupIdAsync(int groupId);
    Task<LedgerReportResult> GetLedgerReportAsync(int accountId, string accountType, DateTime fromDate, DateTime toDate);

    Task<(bool success, string message)> CreateGrowerBookEntryAsync(GeneralEntry entry);
    Task<(bool success, string message)> UpdateGrowerBookEntryAsync(GeneralEntry entry);
    Task<(List<GeneralEntry> entries, int totalCount, int totalPages)> GetGrowerBookEntriesAsync(DateTime? fromDate, DateTime? toDate, string? bookNo, string? fromGrower, string? toGrower, string? status, string? unit, int page, int pageSize);

    Task<string> GetMediatorAccountNameAsync();
    
    Task<IEnumerable<object>> GetGrowerGroupsAsync(string? searchTerm);
    Task<IEnumerable<object>> GetFarmersByGroupAsync(int? groupId, string? searchTerm, string? type = null);
    Task<IEnumerable<LookupItem>> GetGrowerAccountsAsync(string? searchTerm, string? transactionType, string? accountSide, int? entryForId = null);
    Task<List<string>> GetUnitNamesAsync();
}

