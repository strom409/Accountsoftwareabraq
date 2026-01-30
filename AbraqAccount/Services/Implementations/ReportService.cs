using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AbraqAccount.Services.Implementations;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    #region Balance Sheet
    public async Task<BalanceSheetViewModel> GetBalanceSheetAsync(DateTime asOfDate)
    {
        try
        {
            var model = new BalanceSheetViewModel
            {
                AsOfDate = asOfDate
            };

            // 1. Fetch all General Entries up to the date
            // Optimize: Loading necessary fields only
            var entries = await _context.GeneralEntries
                .Where(e => e.EntryDate <= asOfDate)
                .Select(e => new 
                {
                    e.DebitAccountId,
                    e.DebitAccountType,
                    e.CreditAccountId,
                    e.CreditAccountType,
                    e.Amount
                })
                .ToListAsync();

            // 2. Helper to Calculate Balances for a specific Master Group
            // Returns list of MasterSubGroups with their Ledgers and Balances
            async Task<List<BS_GroupViewModel>> GetGroupBalancesAsync(string masterGroupCode, bool isAssetOrExpense)
            {
                var result = new List<BS_GroupViewModel>();

                var masterGroup = await _context.MasterGroups
                    .FirstOrDefaultAsync(g => g.Code == masterGroupCode);

                if (masterGroup == null) return result;

                var subGroups = await _context.MasterSubGroups
                    .Where(sg => sg.MasterGroupId == masterGroup.Id)
                    .ToListAsync();

                foreach (var subGroup in subGroups)
                {
                    var groupViewModel = new BS_GroupViewModel
                    {
                        GroupName = subGroup.Name
                    };

                    // Manually fetch ledgers since navigation property is missing
                    var ledgers = await _context.SubGroupLedgers
                        .Where(l => l.MasterSubGroupId == subGroup.Id)
                        .ToListAsync();

                    if (ledgers != null)
                    {
                        foreach (var ledger in ledgers)
                        {
                            // Calculate Debit Total for this Ledger
                            var debits = entries
                                .Where(e => e.DebitAccountType == "SubGroupLedger" && e.DebitAccountId == ledger.Id)
                                .Sum(e => e.Amount);
                                
                            // Calculate Credit Total
                            var credits = entries
                                .Where(e => e.CreditAccountType == "SubGroupLedger" && e.CreditAccountId == ledger.Id)
                                .Sum(e => e.Amount);

                            decimal balance = 0;
                            if (isAssetOrExpense) // Assets & Expenses: Debit - Credit
                                balance = debits - credits;
                            else // Liabilities & Income: Credit - Debit
                                balance = credits - debits;

                            if (balance != 0)
                            {
                                groupViewModel.Accounts.Add(new BS_AccountViewModel
                                {
                                    AccountName = ledger.Name,
                                    Amount = balance
                                });
                            }
                        }
                    }

                    if (groupViewModel.Accounts.Any())
                    {
                        groupViewModel.TotalAmount = groupViewModel.Accounts.Sum(a => a.Amount);
                        result.Add(groupViewModel);
                    }
                }

                return result;
            }

            // 3. Calculate ASSETS
            // Assuming 'AST' is code for ASSETS
            model.Assets = await GetGroupBalancesAsync("AST", true);
            model.TotalAssets = model.Assets.Sum(g => g.TotalAmount);

            // 4. Calculate LIABILITIES
            // Assuming 'LIB' is code for LIABILITIES
            model.Liabilities = await GetGroupBalancesAsync("LIB", false);
            model.TotalLiabilities = model.Liabilities.Sum(g => g.TotalAmount);

            // 5. Calculate NET PROFIT (Income - Expense)
            // Assuming 'INC' for Income, 'EXP' for Expense
            var incomeGroups = await GetGroupBalancesAsync("INC", false); // Income is Credit nature
            var expenseGroups = await GetGroupBalancesAsync("EXP", true); // Expense is Debit nature

            var totalIncome = incomeGroups.Sum(g => g.TotalAmount);
            var totalExpense = expenseGroups.Sum(g => g.TotalAmount);
            
            model.NetProfitLoss = totalIncome - totalExpense;

            // 6. Final Equation
            model.TotalLiabilitiesAndEquity = model.TotalLiabilities + model.NetProfitLoss;

            return model;
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

