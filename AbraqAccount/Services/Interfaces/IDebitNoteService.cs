using AbraqAccount.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Interfaces;

public interface IDebitNoteService
{
     Task<(List<DebitNote> notes, int totalCount, int totalPages)> GetDebitNotesAsync(
        string? unit, string? debitNoteNo, string? vendor, string? status, 
        DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    
    Task<(bool success, string message)> CreateDebitNoteAsync(DebitNote model, IFormCollection? form = null);
    Task<(bool success, string message)> UpdateDebitNoteAsync(DebitNote model, IFormCollection? form = null);
    Task<(bool success, string message)> DeleteDebitNoteAsync(int id);
    Task<(bool success, string message)> ApproveDebitNoteAsync(int id);
    Task<(bool success, string message)> UnapproveDebitNoteAsync(int id);
    Task<DebitNote?> GetDebitNoteByIdAsync(int id);
    Task LoadDropdownsAsync(dynamic viewBag);
    Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? entryAccountId = null, string? type = null);
    Task PopulateAccountNamesAsync(IEnumerable<DebitNote> notes);
    Task<int?> GetEntryProfileIdAsync(int creditAccountId, string creditType, int debitAccountId, string debitType);
    Task<IEnumerable<LookupItem>> GetEntryProfilesAsync();
    Task<IEnumerable<string>> GetUnitNamesAsync();
}

