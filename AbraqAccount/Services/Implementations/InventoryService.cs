using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    #region Material Issue

    public async Task<(List<MaterialIssue> issues, int totalCount, int totalPages)> GetMaterialIssuesAsync(
        string? materialIssueNo, string? deliveredGroup, string? deliveredTo, string? orderBy, 
        string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        try
        {
            var query = _context.MaterialIssues.AsQueryable();

            if (!string.IsNullOrEmpty(materialIssueNo)) query = query.Where(m => m.MaterialIssueNo.Contains(materialIssueNo));
            if (!string.IsNullOrEmpty(deliveredGroup)) query = query.Where(m => m.DeliveredTo.Contains(deliveredGroup));
            if (!string.IsNullOrEmpty(deliveredTo)) query = query.Where(m => m.DeliveredTo.Contains(deliveredTo));
            if (!string.IsNullOrEmpty(orderBy)) query = query.Where(m => m.OrderBy.Contains(orderBy));
            if (!string.IsNullOrEmpty(status)) query = query.Where(m => m.Status == status);
            if (fromDate.HasValue) query = query.Where(m => m.OrderDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(m => m.OrderDate <= toDate.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var issues = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (issues, totalCount, totalPages);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateMaterialIssueAsync(MaterialIssue model, Microsoft.AspNetCore.Http.IFormCollection? form)
    {
        if (form == null) return await CreateMaterialIssueAsync(model);
        try
        {
            var items = GetIssueItemsFromForm(form);
            return await CreateMaterialIssueWithItems(model, items);
        }
        catch (Exception ex)
        {
            return (false, "An error occurred: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> CreateMaterialIssueAsync(MaterialIssue model)
    {
        return await CreateMaterialIssueWithItems(model, model.Items);
    }

    private async Task<(bool success, string message)> CreateMaterialIssueWithItems(MaterialIssue model, List<MaterialIssueItem> items)
    {
        try
        {
            var lastIssue = await _context.MaterialIssues.OrderByDescending(m => m.Id).FirstOrDefaultAsync();
            int nextIssueNo = 1;
            if (lastIssue != null && !string.IsNullOrEmpty(lastIssue.MaterialIssueNo))
            {
                if (int.TryParse(lastIssue.MaterialIssueNo.Replace("MI", ""), out int lastNumber)) nextIssueNo = lastNumber + 1;
            }
            model.MaterialIssueNo = $"MI{nextIssueNo:D6}";

            if (items.Any()) model.Qty = items.Sum(i => i.IssuedQty);
            model.CreatedAt = DateTime.Now;
            model.Status = model.Status ?? "Completed";

            _context.MaterialIssues.Add(model);
            foreach (var item in items)
            {
                item.CreatedAt = DateTime.Now;
                _context.MaterialIssueItems.Add(item);
            }

            await _context.SaveChangesAsync();
            return (true, "Material Issue created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    private List<MaterialIssueItem> GetIssueItemsFromForm(IFormCollection form)
    {
        try
        {
            var items = new List<MaterialIssueItem>();
            var itemIndex = 0;

            while (form.ContainsKey($"items[{itemIndex}].ItemName"))
            {
                var itemName = form[$"items[{itemIndex}].ItemName"].ToString();
                var issuedQtyStr = form[$"items[{itemIndex}].IssuedQty"].ToString();

                if (!string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(issuedQtyStr))
                {
                    if (decimal.TryParse(issuedQtyStr, out decimal issuedQty))
                    {
                        var purchaseItemIdStr = form[$"items[{itemIndex}].PurchaseItemId"].ToString();
                        int? purchaseItemId = null;
                        if (int.TryParse(purchaseItemIdStr, out int pid))
                        {
                            purchaseItemId = pid;
                        }

                        var item = new MaterialIssueItem
                        {
                            PurchaseItemId = purchaseItemId,
                            ItemName = itemName,
                            UOM = form[$"items[{itemIndex}].UOM"].ToString(),
                            BalanceQty = decimal.TryParse(form[$"items[{itemIndex}].BalanceQty"].ToString(), out decimal balanceQty) ? balanceQty : null,
                            IssuedQty = issuedQty,
                            IsReturnable = form[$"items[{itemIndex}].IsReturnable"].ToString() == "true",
                            CreatedAt = DateTime.Now
                        };
                        items.Add(item);
                    }
                }
                itemIndex++;
            }
            return items;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetPurchaseItemsAsync(string? searchTerm)
    {
        try
        {
            var query = _context.PurchaseItems.Where(pi => pi.IsActive).AsQueryable();
            if (!string.IsNullOrEmpty(searchTerm)) 
                query = query.Where(pi => pi.ItemName.Contains(searchTerm) || pi.Code.Contains(searchTerm));
            
            return await query
                .OrderBy(pi => pi.ItemName)
                .Select(pi => new LookupItem { 
                    Id = pi.Id, 
                    Name = pi.ItemName,
                    UOM = pi.UOM
                })
                .Take(50)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }


    #endregion

    #region Material Stock Ledger

    public async Task<List<PurchaseItem>> GetStockLedgerReportAsync(
        DateTime? fromDate, DateTime? toDate, string? itemGroup, 
        string? itemName, string? uom, string? unit, 
        string? vendorName, string? reportType, string? commonSearch)
    {
        try
        {
            // This is a report, so we'll query purchase items and related transactions
            var query = _context.PurchaseItems
                .Include(pi => pi.PurchaseItemGroup)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(itemGroup))
            {
                query = query.Where(pi => pi.PurchaseItemGroup != null && pi.PurchaseItemGroup.Name.Contains(itemGroup));
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                query = query.Where(pi => pi.ItemName.Contains(itemName) || pi.BillingName.Contains(itemName));
            }

            if (!string.IsNullOrEmpty(uom))
            {
                query = query.Where(pi => pi.UOM == uom);
            }

            if (!string.IsNullOrEmpty(commonSearch))
            {
                query = query.Where(pi => 
                    pi.ItemName.Contains(commonSearch) || 
                    pi.BillingName.Contains(commonSearch) ||
                    (pi.PurchaseItemGroup != null && pi.PurchaseItemGroup.Name.Contains(commonSearch)));
            }

            return await query.OrderBy(pi => pi.ItemName).ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task LoadStockLedgerDropdownsAsync(dynamic viewBag)
    {
        try
        {
            var uomList = await _context.PurchaseItems
                .Where(pi => !string.IsNullOrEmpty(pi.UOM))
                .Select(pi => pi.UOM)
                .Distinct()
                .OrderBy(uom => uom)
                .ToListAsync();

            viewBag.UOMList = new SelectList(uomList);

            var unitList = new List<SelectListItem>
            {
                new SelectListItem { Value = "All", Text = "All" },
                new SelectListItem { Value = "Unit1", Text = "Unit 1" },
                new SelectListItem { Value = "Unit2", Text = "Unit 2" }
            };
            viewBag.UnitList = new SelectList(unitList, "Value", "Text");

            var reportTypeList = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--Select--" },
                new SelectListItem { Value = "StockSummary", Text = "Stock Summary" },
                new SelectListItem { Value = "StockMovement", Text = "Stock Movement" },
                new SelectListItem { Value = "StockBalance", Text = "Stock Balance" }
            };
            viewBag.ReportTypeList = new SelectList(reportTypeList, "Value", "Text");
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

