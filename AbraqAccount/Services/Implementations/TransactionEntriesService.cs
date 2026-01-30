using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;

namespace AbraqAccount.Services.Implementations;

public class TransactionEntriesService : ITransactionEntriesService
{
    private readonly AppDbContext _context;

    public TransactionEntriesService(AppDbContext context)
    {
        _context = context;
    }

    #region Transaction Retrieval
    public async Task<(
        List<ReceiptEntry>? receiptEntries,
        List<PaymentSettlement>? paymentSettlements,
        List<GeneralEntry>? journalEntries,
        int totalCount,
        int totalPages
    )> GetTransactionsAsync(
        string tabType,
        string? voucherNo,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        try
        {
            List<ReceiptEntry>? receiptEntries = null;
            List<PaymentSettlement>? paymentSettlements = null;
            List<GeneralEntry>? journalEntries = null;
            int totalCount = 0;

            if (tabType == "Receipt")
            {
                var receiptQuery = _context.ReceiptEntries.AsQueryable();

                if (!string.IsNullOrEmpty(voucherNo))
                {
                    receiptQuery = receiptQuery.Where(r => r.VoucherNo.Contains(voucherNo));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    receiptQuery = receiptQuery.Where(r => r.Status == status);
                }

                if (fromDate.HasValue)
                {
                    receiptQuery = receiptQuery.Where(r => r.ReceiptDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    receiptQuery = receiptQuery.Where(r => r.ReceiptDate <= toDate.Value);
                }

                totalCount = await receiptQuery.CountAsync();
                
                receiptEntries = await receiptQuery
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Load account names for display
                foreach (var entry in receiptEntries)
                {
                    await LoadReceiptAccountNamesAsync(entry);
                }
            }
            else if (tabType == "Payment")
            {
                var paymentQuery = _context.PaymentSettlements
                    .AsQueryable();

                if (!string.IsNullOrEmpty(voucherNo))
                {
                    paymentQuery = paymentQuery.Where(p => p.PANumber.Contains(voucherNo));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    paymentQuery = paymentQuery.Where(p => p.ApprovalStatus == status || p.PaymentStatus == status);
                }

                if (fromDate.HasValue)
                {
                    paymentQuery = paymentQuery.Where(p => p.SettlementDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    paymentQuery = paymentQuery.Where(p => p.SettlementDate <= toDate.Value);
                }

                totalCount = await paymentQuery.CountAsync();

                paymentSettlements = await paymentQuery
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            else if (tabType == "Journal")
            {
                var journalQuery = _context.GeneralEntries.AsQueryable();

                if (!string.IsNullOrEmpty(voucherNo))
                {
                    journalQuery = journalQuery.Where(g => g.VoucherNo.Contains(voucherNo));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    journalQuery = journalQuery.Where(g => g.Status == status);
                }

                if (fromDate.HasValue)
                {
                    journalQuery = journalQuery.Where(g => g.EntryDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    journalQuery = journalQuery.Where(g => g.EntryDate <= toDate.Value);
                }

                totalCount = await journalQuery.CountAsync();

                journalEntries = await journalQuery
                    .OrderByDescending(g => g.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Load account names for display
                foreach (var entry in journalEntries)
                {
                    await LoadGeneralEntryAccountNamesAsync(entry);
                }
            }

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return (receiptEntries, paymentSettlements, journalEntries, totalCount, totalPages);
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Helpers
    private async Task LoadReceiptAccountNamesAsync(ReceiptEntry entry)
    {
        try
        {
            if (entry.AccountType == "MasterGroup")
            {
                entry.MasterGroup = await _context.MasterGroups.FindAsync(entry.AccountId);
            }
            else if (entry.AccountType == "MasterSubGroup")
            {
                entry.MasterSubGroup = await _context.MasterSubGroups
                    .Include(msg => msg.MasterGroup)
                    .FirstOrDefaultAsync(msg => msg.Id == entry.AccountId);
            }
            else if (entry.AccountType == "SubGroupLedger")
            {
                entry.SubGroupLedger = await _context.SubGroupLedgers
                    .Include(sgl => sgl.MasterGroup)
                    .Include(sgl => sgl.MasterSubGroup)
                    .FirstOrDefaultAsync(sgl => sgl.Id == entry.AccountId);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    private async Task LoadGeneralEntryAccountNamesAsync(GeneralEntry entry)
    {
        try
        {
            // Load debit account
            if (entry.DebitAccountType == "MasterGroup")
            {
                entry.DebitMasterGroup = await _context.MasterGroups.FindAsync(entry.DebitAccountId);
            }
            else if (entry.DebitAccountType == "MasterSubGroup")
            {
                entry.DebitMasterSubGroup = await _context.MasterSubGroups
                    .Include(msg => msg.MasterGroup)
                    .FirstOrDefaultAsync(msg => msg.Id == entry.DebitAccountId);
            }
            else if (entry.DebitAccountType == "SubGroupLedger")
            {
                entry.DebitSubGroupLedger = await _context.SubGroupLedgers
                    .Include(sgl => sgl.MasterGroup)
                    .Include(sgl => sgl.MasterSubGroup)
                    .FirstOrDefaultAsync(sgl => sgl.Id == entry.DebitAccountId);
            }

            // Load credit account
            if (entry.CreditAccountType == "MasterGroup")
            {
                entry.CreditMasterGroup = await _context.MasterGroups.FindAsync(entry.CreditAccountId);
            }
            else if (entry.CreditAccountType == "MasterSubGroup")
            {
                entry.CreditMasterSubGroup = await _context.MasterSubGroups
                    .Include(msg => msg.MasterGroup)
                    .FirstOrDefaultAsync(msg => msg.Id == entry.CreditAccountId);
            }
            else if (entry.CreditAccountType == "SubGroupLedger")
            {
                entry.CreditSubGroupLedger = await _context.SubGroupLedgers
                    .Include(sgl => sgl.MasterGroup)
                    .Include(sgl => sgl.MasterSubGroup)
                    .FirstOrDefaultAsync(sgl => sgl.Id == entry.CreditAccountId);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region History Logging
    public async Task LogTransactionHistoryAsync(string voucherNo, string voucherType, string action, string user, string? remarks = null, string? oldValues = null, string? newValues = null)
    {
        try
        {
            var history = new TransactionHistory
            {
                VoucherNo = voucherNo,
                VoucherType = voucherType,
                Action = action,
                User = user,
                ActionDate = DateTime.Now,
                Remarks = remarks ?? action,
                OldValues = oldValues,
                NewValues = newValues
            };

            _context.TransactionHistories.Add(history);
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<TransactionHistory>> GetTransactionHistoryAsync(string voucherNo)
    {
        try
        {
            return await _context.TransactionHistories
                .Where(h => h.VoucherNo == voucherNo)
                .OrderByDescending(h => h.ActionDate)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

