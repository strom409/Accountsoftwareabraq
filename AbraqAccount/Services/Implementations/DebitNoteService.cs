using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;
using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;

namespace AbraqAccount.Services.Implementations;

public class DebitNoteService : IDebitNoteService
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DebitNoteService(AppDbContext context, IDbContextFactory<AppDbContext> contextFactory, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _contextFactory = contextFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    #region Helpers
    private string GetCurrentUsername()
    {
        try
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return "Admin";
            var userSession = session.GetObject<UserSession>(SessionKeys.UserSession);
            return userSession?.Username ?? "Admin";
        }
        catch
        {
            return "Admin";
        }
    }
    #endregion

    #region Listing
    public async Task<(List<DebitNote> notes, int totalCount, int totalPages)> GetDebitNotesAsync(
        string? unit, string? debitNoteNo, string? vendor, string? status, 
        DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        try
        {
            var query = _context.DebitNotes.Where(d => d.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(unit) && unit != "ALL") query = query.Where(d => d.Unit == unit);
            if (!string.IsNullOrEmpty(debitNoteNo)) query = query.Where(d => d.DebitNoteNo.Contains(debitNoteNo));
            if (!string.IsNullOrEmpty(status)) query = query.Where(d => d.Status == status);
            if (fromDate.HasValue) query = query.Where(d => d.DebitNoteDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(d => d.DebitNoteDate <= toDate.Value);

            // Fetch all matching notes first (in-memory filtration for polymorphic names required)
            var allDebitNotes = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
            
            // Populate Polymorphic Names
            await PopulateAccountNamesAsync(allDebitNotes);

            if (!string.IsNullOrEmpty(vendor))
            {
                allDebitNotes = allDebitNotes
                    .Where(d => (d.CreditAccountName != null && d.CreditAccountName.Contains(vendor, StringComparison.OrdinalIgnoreCase)) 
                             || (d.DebitAccountName != null && d.DebitAccountName.Contains(vendor, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var totalCount = allDebitNotes.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var debitNotes = allDebitNotes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return (debitNotes, totalCount, totalPages);
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Create
    public async Task<(bool success, string message)> CreateDebitNoteAsync(DebitNote debitNote, IFormCollection? form = null)
    {
        try
        {
            var details = form != null ? GetDetailsFromForm(form) : debitNote.Details;
            if (!details.Any()) 
            {
                 // Create default detail from header amount if no details provided
                 details.Add(new DebitNoteDetail 
                 { 
                     AccountType = "General", 
                     Amount = debitNote.Amount ?? 0,
                     RefNo = "",
                     HsnSacCode = "",
                     Qty = 0,
                     Rate = 0
                 });
            }

            // Generate No
            var lastNote = await _context.DebitNotes.OrderByDescending(d => d.Id).FirstOrDefaultAsync();
            int nextNoteNo = 1;
            if (lastNote != null && !string.IsNullOrEmpty(lastNote.DebitNoteNo) && int.TryParse(lastNote.DebitNoteNo.Replace("DN", ""), out int lastNumber))
            {
                nextNoteNo = lastNumber + 1;
            }
            debitNote.DebitNoteNo = $"DN{nextNoteNo:D6}";
            debitNote.Amount = details.Sum(d => d.Amount);
            debitNote.CreatedAt = DateTime.Now;
            debitNote.CreatedBy = GetCurrentUsername();
            debitNote.Status = debitNote.Status ?? "UnApproved";
            debitNote.IsActive = true;

            // Handle BankMasterId safely: Allow null, do not force 0
            // if (debitNote.BankMasterId == null) debitNote.BankMasterId = 0; 

            // Assign details properties
            foreach (var detail in details)
            {
                detail.CreatedAt = DateTime.Now;
            }

            _context.DebitNotes.Add(debitNote);
            await _context.SaveChangesAsync();

            // Store legacy mapping if needed (keeping original logic)
            await StoreBankMasterIdMappingAsync(debitNote.Id, debitNote.BankMasterId ?? 0);

            return (true, $"Debit Note created successfully! {details.Count} detail(s) added.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }
    #endregion

    #region Delete
    public async Task<(bool success, string message)> DeleteDebitNoteAsync(int id)
    {
        try
        {
             var note = await _context.DebitNotes.FindAsync(id);
             if (note == null) return (false, "Not found");
             
             var currentUser = GetCurrentUsername();
             note.IsActive = false;
             note.UpdatedAt = DateTime.Now;
             note.UpdatedBy = currentUser;
             _context.Update(note);
             await _context.SaveChangesAsync();
             return (true, "Deleted successfully");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }
    #endregion

    #region Detail
    public async Task<DebitNote?> GetDebitNoteByIdAsync(int id)
    {
        try
        {
            var note = await _context.DebitNotes
                .Include(d => d.Details)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (note != null)
            {
                 // Populate Polymorphic Names
                 note.CreditAccountName = await GetAccountNameAsync(note.CreditAccountType, note.CreditAccountId);
                 note.DebitAccountName = await GetAccountNameAsync(note.DebitAccountType, note.DebitAccountId);

                 // Legacy Support (Optional)
                 var mappings = await LoadBankMasterIdMappingsAsync();
                 if (mappings.TryGetValue(note.Id, out int mappedBankMasterId))
                 {
                     var account = await _context.BankMasters.FindAsync(mappedBankMasterId);
                     if (account != null) note.BankMaster = account;
                 }
            }
            return note;
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Edit
    public async Task<(bool success, string message)> UpdateDebitNoteAsync(DebitNote model, IFormCollection? form = null)
    {
        try
        {
            var note = await _context.DebitNotes.FindAsync(model.Id);
            if (note == null) return (false, "Not found");
            if (note.Status == "Approved") return (false, "Cannot edit approved note");

            // Update Fields
            note.Unit = model.Unit;
            note.DebitNoteDate = model.DebitNoteDate;
            note.Narration = model.Narration;
            note.Amount = model.Amount;

            // Update Entry For
            note.EntryForId = model.EntryForId;
            note.EntryForName = model.EntryForName;

            // Update Polymorphic Fields
            // If IDs are provided in model. If 0, assume unchanged? 
            // No, binding should provide them.
            if (model.CreditAccountId > 0) 
            {
                note.CreditAccountId = model.CreditAccountId;
                note.CreditAccountType = model.CreditAccountType ?? note.CreditAccountType;
            }
            if (model.DebitAccountId > 0)
            {
                note.DebitAccountId = model.DebitAccountId;
                note.DebitAccountType = model.DebitAccountType ?? note.DebitAccountType;
            }
            
            // Update details?
            // If we are using dummy details, we should update the dummy detail amount too.
            var details = await _context.DebitNoteDetails.Where(d => d.DebitNoteId == note.Id).ToListAsync();
            if (details.Any())
            {
                // If specific details exist, what do we do?
                // If user is editing Amount only, we might break details matching.
                // PROPOSAL: Update the first detail to match Amount if only one detail exists and looks like dummy.
                if (details.Count == 1 && details[0].AccountType == "General")
                {
                    details[0].Amount = note.Amount ?? 0;
                }
            }
            else
            {
                 // Create default detail
                 _context.DebitNoteDetails.Add(new DebitNoteDetail 
                 { 
                     DebitNoteId = note.Id,
                     AccountType = "General", 
                     Amount = note.Amount ?? 0,
                     RefNo = "",
                     HsnSacCode = "",
                     Qty = 0,
                     Rate = 0
                 });
            }

            note.UpdatedAt = DateTime.Now;
            note.UpdatedBy = GetCurrentUsername();

            await _context.SaveChangesAsync();
            return (true, "Updated successfully");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }
    #endregion

    #region Approval
    public async Task<(bool success, string message)> ApproveDebitNoteAsync(int id)
    {
        try
        {
            var note = await _context.DebitNotes.FindAsync(id);
            if (note == null) return (false, "Not found");
            
            var currentUser = GetCurrentUsername();
            note.Status = "Approved";
            note.UpdatedAt = DateTime.Now;
            note.UpdatedBy = currentUser;
            await _context.SaveChangesAsync();
            return (true, "Approved successfully");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> UnapproveDebitNoteAsync(int id)
    {
        try
        {
            var note = await _context.DebitNotes.FindAsync(id);
            if (note == null) return (false, "Not found");
            
            if (note.Status != "Approved") return (false, "Note is not approved");

            var currentUser = GetCurrentUsername();
            note.Status = "UnApproved";
            note.UpdatedAt = DateTime.Now;
            note.UpdatedBy = currentUser;
            await _context.SaveChangesAsync();
            return (true, "Unapproved successfully");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }
    #endregion

    #region Lookups
    public async Task LoadDropdownsAsync(dynamic viewBag)
    {
        try
        {
            var unitList = new List<SelectListItem>
            {
                new SelectListItem { Value = "UNIT-1", Text = "UNIT-1" },
                new SelectListItem { Value = "UNIT-2", Text = "UNIT-2" },
                new SelectListItem { Value = "Abraq Agro Fresh LLP", Text = "Abraq Agro Fresh LLP" }
            };
            viewBag.UnitList = new SelectList(unitList, "Value", "Text");

            var statusList = new List<SelectListItem>
            {
                new SelectListItem { Value = "UnApproved", Text = "UnApproved" },
                new SelectListItem { Value = "Approved", Text = "Approved" },
                new SelectListItem { Value = "Rejected", Text = "Rejected" }
            };
            viewBag.StatusList = new SelectList(statusList, "Value", "Text");

            var accountTypeList = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Select" },
                new SelectListItem { Value = "Discount", Text = "Discount" },
                new SelectListItem { Value = "Return", Text = "Return" },
                new SelectListItem { Value = "Freight Expense", Text = "Freight Expense" },
                new SelectListItem { Value = "Debit Note", Text = "Debit Note" },
                new SelectListItem { Value = "Packing Charges", Text = "Packing Charges" },
                new SelectListItem { Value = "Carriage Expense", Text = "Carriage Expense" },
                new SelectListItem { Value = "TCS on sale (0.1%)", Text = "TCS on sale (0.1%)" }
            };
            viewBag.AccountTypeList = new SelectList(accountTypeList, "Value", "Text");

            var entryProfiles = await _context.EntryForAccounts
                .Where(e => e.TransactionType == "Global" || e.TransactionType == "DebitNote") 
                .OrderBy(e => e.AccountName)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.AccountName
                })
                .ToListAsync();
            viewBag.EntryProfiles = new SelectList(entryProfiles, "Value", "Text");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? entryAccountId = null, string? type = null)
    {
        try
        {
            // USE FRESH CONTEXT to avoid concurrency issues
            using var context = await _contextFactory.CreateDbContextAsync();

            // 1. Fetch Rules dictionary for fast lookup
            var rules = await context.AccountRules
                .Where(r => r.RuleType == "AllowedNature")
                .ToListAsync();

            string? GetRuleValue(string accountType, int accountId, int? entryId)
            {
                if (entryId.HasValue)
                {
                    var specificRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == entryId);
                    if (specificRule != null) return specificRule.Value;
                }
                var defaultRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == null);
                if (defaultRule != null) return defaultRule.Value;
                return null;
            }

            bool CheckRule(string ruleValue)
            {
                if (string.IsNullOrWhiteSpace(ruleValue)) return false;
                if (ruleValue.Equals("Both", StringComparison.OrdinalIgnoreCase)) return true;
                if (ruleValue.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) return false;

                if (string.IsNullOrWhiteSpace(type)) return true; 

                if (ruleValue.Equals("Debit", StringComparison.OrdinalIgnoreCase) && type.Equals("Debit", StringComparison.OrdinalIgnoreCase)) return true;
                if (ruleValue.Equals("Credit", StringComparison.OrdinalIgnoreCase) && type.Equals("Credit", StringComparison.OrdinalIgnoreCase)) return true;

                return false;
            }

            // Helper to check if account is allowed (for global search fallback)
            bool IsAllowed(string accountType, int accountId, string? fallbackType = null, int? fallbackId = null)
            {
                // 1. Check Specific Account Rule
                string? ruleValue = GetRuleValue(accountType, accountId, entryAccountId);
                if (ruleValue != null)
                {
                    return CheckRule(ruleValue);
                }

                // 2. Check Fallback Group Rule
                if (fallbackType != null && fallbackId.HasValue)
                {
                    string? fallbackRuleValue = GetRuleValue(fallbackType, fallbackId.Value, entryAccountId);
                    if (fallbackRuleValue != null)
                    {
                        return CheckRule(fallbackRuleValue);
                    }
                }
                return true; // No rule = Allowed
            }

            // ---------------------------------------------------------
            // LOGIC MATCHING MVC GeneralEntryService
            // ---------------------------------------------------------

            if (entryAccountId.HasValue)
            {
                // STRICT FILTERING: Only show accounts allowed by the Profile's Rules

                // 1. Get Rules for this Profile
                var profileRules = rules.Where(r => r.EntryAccountId == entryAccountId.Value).ToList();

                // 2. Build Allowed ID Sets
                var allowedSubGroupIds = profileRules
                    .Where(r => r.AccountType == "SubGroupLedger" && CheckRule(r.Value))
                    .Select(r => r.AccountId)
                    .ToHashSet();

                var allowedGrowerGroupIds = profileRules
                    .Where(r => r.AccountType == "GrowerGroup" && CheckRule(r.Value))
                    .Select(r => r.AccountId)
                    .ToHashSet();

                var allowedBankMasterIds = profileRules
                    .Where(r => r.AccountType == "BankMaster" && CheckRule(r.Value))
                    .Select(r => r.AccountId)
                    .ToHashSet();

                var allowedFarmerIds = profileRules
                    .Where(r => r.AccountType == "Farmer" && CheckRule(r.Value))
                    .Select(r => r.AccountId)
                    .ToHashSet();

                // 3. Query DB with Allowed List
                var bankMastersQuery = context.BankMasters.Where(bm => bm.IsActive);
                var farmersQuery = context.Farmers.Where(f => f.IsActive);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    bankMastersQuery = bankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
                    farmersQuery = farmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
                }

                // Execute Queries - Filtering by ID sets
                var bankMasters = await bankMastersQuery
                    .Where(bm => allowedBankMasterIds.Contains(bm.Id) || allowedSubGroupIds.Contains(bm.GroupId))
                    .OrderBy(bm => bm.AccountName)
                    .Take(50)
                    .ToListAsync();

                var farmers = await farmersQuery
                    .Where(f => allowedFarmerIds.Contains(f.Id) || allowedGrowerGroupIds.Contains(f.GroupId))
                    .OrderBy(f => f.FarmerName)
                    .Take(50)
                    .ToListAsync();

                var results = new List<LookupItem>();
                results.AddRange(bankMasters.Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster", AccountNumber = bm.AccountNumber }));
                results.AddRange(farmers.Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer", AccountNumber = f.FarmerCode })); // Assuming FarmerCode

                return results.OrderBy(r => r.Name).Take(100).ToList();
            }
            else
            {
                // GLOBAL SEARCH (No Profile Selected) - Fallback to previous logic (fetch then filter)
                
                var bankMastersQuery = context.BankMasters.Where(bm => bm.IsActive);
                var farmersQuery = context.Farmers.Where(f => f.IsActive);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    bankMastersQuery = bankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
                    farmersQuery = farmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
                }

                var bankMasters = await bankMastersQuery.OrderBy(bm => bm.AccountName).Take(200).ToListAsync();
                var farmers = await farmersQuery.OrderBy(f => f.FarmerName).Take(200).ToListAsync();

                var results = new List<LookupItem>();

                results.AddRange(bankMasters
                    .Where(bm => IsAllowed("BankMaster", bm.Id, "SubGroupLedger", bm.GroupId))
                    .Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster", AccountNumber = bm.AccountNumber }));

                results.AddRange(farmers
                    .Where(f => IsAllowed("Farmer", f.Id, "GrowerGroup", f.GroupId))
                    .Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer", AccountNumber = f.FarmerCode }));

                return results.OrderBy(r => r.Name).Take(100).ToList();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Processing
    private List<DebitNoteDetail> GetDetailsFromForm(IFormCollection form)
    {
        try
        {
            var details = new List<DebitNoteDetail>();
            var detailIndex = 0;

            while (form.ContainsKey($"details[{detailIndex}].AccountType"))
            {
                var accountType = form[$"details[{detailIndex}].AccountType"].ToString();
                var amountStr = form[$"details[{detailIndex}].Amount"].ToString();

                if (!string.IsNullOrEmpty(accountType) && !string.IsNullOrEmpty(amountStr) && decimal.TryParse(amountStr, out decimal amount))
                {
                    var detail = new DebitNoteDetail
                    {
                        AccountType = accountType,
                        RefNo = form[$"details[{detailIndex}].RefNo"].ToString(),
                        HsnSacCode = form[$"details[{detailIndex}].HsnSacCode"].ToString(),
                        Qty = decimal.TryParse(form[$"details[{detailIndex}].Qty"].ToString(), out decimal qty) ? qty : null,
                        Rate = decimal.TryParse(form[$"details[{detailIndex}].Rate"].ToString(), out decimal rate) ? rate : null,
                        Amount = amount,
                        CreatedAt = DateTime.Now
                    };
                    details.Add(detail);
                }
                detailIndex++;
            }
            return details;
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Legacy Mappings
    private async Task StoreBankMasterIdMappingAsync(int debitNoteId, int bankMasterId)
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
            var mappingPath = Path.Combine(projectRoot, "Data", "DebitNoteBankMasterMapping.json");
            var directory = Path.GetDirectoryName(mappingPath);
            if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            Dictionary<int, int> mappings = new Dictionary<int, int>();
            if (System.IO.File.Exists(mappingPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(mappingPath);
                if (!string.IsNullOrEmpty(json)) mappings = JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
            }

            mappings[debitNoteId] = bankMasterId;
            var updatedJson = JsonSerializer.Serialize(mappings, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(mappingPath, updatedJson);
        }
        catch { }
    }

    private async Task<Dictionary<int, int>> LoadBankMasterIdMappingsAsync()
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
            var mappingPath = Path.Combine(projectRoot, "Data", "DebitNoteBankMasterMapping.json");
            if (System.IO.File.Exists(mappingPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(mappingPath);
                if (!string.IsNullOrEmpty(json)) return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
            }
        }
        catch { }
        return new Dictionary<int, int>();
    }
    #endregion

    #region Account Helper
    private async Task<string> GetAccountNameAsync(string type, int id)
    {
        try
        {
            if (id == 0) return "N/A";
            
            string typeLower = type?.ToLower() ?? "";

            if (typeLower.Contains("farmer"))
            {
                var f = await _context.Farmers.FindAsync(id);
                return f?.FarmerName ?? "Unknown Farmer";
            }
            if (typeLower.Contains("growergroup"))
            {
                 var g = await _context.GrowerGroups.FindAsync(id);
                 return g?.GroupName ?? "Unknown Group";
            }
            if (typeLower.Contains("bankmaster"))
            {
                var b = await _context.BankMasters.FindAsync(id);
                return b?.AccountName ?? "Unknown Bank";
            }
            if (typeLower.Contains("subgroupledger"))
            {
                var s = await _context.SubGroupLedgers.Include(x => x.MasterGroup).Include(x => x.MasterSubGroup).FirstOrDefaultAsync(x => x.Id == id);
                return s?.Name ?? "Unknown Account";
            }
            if (typeLower.Contains("mastergroup"))
            {
                 var mg = await _context.MasterGroups.FindAsync(id);
                 return mg?.Name ?? "Unknown Group";
            }

            return type + " (ID: " + id + ")";
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Utils
    public async Task PopulateAccountNamesAsync(IEnumerable<DebitNote> notes)
    {
        try
        {
            foreach (var note in notes)
            {
                note.CreditAccountName = await GetAccountNameAsync(note.CreditAccountType, note.CreditAccountId);
                note.DebitAccountName = await GetAccountNameAsync(note.DebitAccountType, note.DebitAccountId);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int?> GetEntryProfileIdAsync(int creditAccountId, string creditType, int debitAccountId, string debitType)
    {
        try
        {
            // specific rule for the credit account
            var creditRule = await _context.AccountRules
                .Where(r => r.AccountType == creditType && r.AccountId == creditAccountId && r.EntryAccountId != null)
                .Select(r => r.EntryAccountId)
                .FirstOrDefaultAsync();

            if (creditRule.HasValue) return creditRule;

            // specific rule for the debit account
            var debitRule = await _context.AccountRules
                .Where(r => r.AccountType == debitType && r.AccountId == debitAccountId && r.EntryAccountId != null)
                .Select(r => r.EntryAccountId)
                .FirstOrDefaultAsync();

            if (debitRule.HasValue) return debitRule;
            
            return null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetEntryProfilesAsync()
    {
        try
        {
            return await _context.EntryForAccounts
                .Where(e => e.TransactionType == "Global" || e.TransactionType == "DebitNote") 
                .OrderBy(e => e.AccountName)
                .Select(e => new LookupItem { Id = e.Id, Name = e.AccountName })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetUnitNamesAsync()
    {
        try
        {
            return await _context.UnitMasters
                .OrderBy(u => u.UnitName)
                .Select(u => u.UnitName ?? "")
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct()
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

