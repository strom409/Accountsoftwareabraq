using AbraqAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Interfaces;

public interface ICreditNoteService
{
    Task<(List<CreditNote> notes, int totalCount, int totalPages)> GetCreditNotesAsync(
        string? unit, string? creditNoteNo, int? growerGroupId, int? farmerId, 
        string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    
    Task<(bool success, string message)> CreateCreditNoteAsync(CreditNote model);
    Task<CreditNote?> GetCreditNoteByIdAsync(int id);
    Task<(bool success, string message)> UpdateCreditNoteAsync(CreditNote model);
    Task<(bool success, string message)> DeleteCreditNoteAsync(int id);
    
    Task LoadDropdownsAsync(dynamic viewBag, int? growerGroupId = null, int? farmerId = null);
    Task<IEnumerable<LookupItem>> GetFarmersByGroupAsync(int groupId);
    Task<IEnumerable<LookupItem>> GetEntryProfilesAsync();
    Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? entryAccountId = null, string? type = null);
    Task PopulateAccountNamesAsync(IEnumerable<CreditNote> notes);
    Task<(bool success, string message)> ApproveCreditNoteAsync(int id);
    Task<(bool success, string message)> UnapproveCreditNoteAsync(int id);
    Task<int?> GetEntryProfileIdAsync(int creditAccountId, string creditType, int debitAccountId, string debitType);
    Task<IEnumerable<string>> GetUnitNamesAsync();
}

