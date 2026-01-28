using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _context;

    public PurchaseOrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<PurchaseOrder> orders, int totalCount, int totalPages)> GetPurchaseOrdersAsync(
        string? poNumber, string? vendorName, string? status, 
        DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var query = _context.PurchaseOrders
            .Include(p => p.Vendor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(poNumber))
        {
            query = query.Where(p => p.PONumber.Contains(poNumber));
        }

        if (!string.IsNullOrEmpty(vendorName))
        {
            query = query.Where(p => 
                (p.Vendor != null && p.Vendor.AccountName.Contains(vendorName)));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(p => p.PODate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(p => p.PODate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var purchaseOrders = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (purchaseOrders, totalCount, totalPages);
    }

    public async Task<(bool success, string message)> CreatePurchaseOrderAsync(PurchaseOrder purchaseOrder, IFormCollection? form)
    {
        if (form == null) return await CreatePurchaseOrderAsync(purchaseOrder);
        try
        {
            // Get items, misc charges, and T&C from form
            var items = GetItemsFromForm(form);
            var miscCharges = GetMiscChargesFromForm(form);
            var termsConditions = GetTermsConditionsFromForm(form);

            // Generate PO Number
            var lastPO = await _context.PurchaseOrders
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            
            int nextPONumber = 1;
            if (lastPO != null && !string.IsNullOrEmpty(lastPO.PONumber))
            {
                if (int.TryParse(lastPO.PONumber.Replace("PO", ""), out int lastNumber))
                {
                    nextPONumber = lastNumber + 1;
                }
            }
            purchaseOrder.PONumber = $"PO{nextPONumber:D6}";

            // Calculate totals
            purchaseOrder.POQty = items.Sum(i => i.Qty);
            purchaseOrder.Amount = items.Sum(i => i.TotalAmount);
            purchaseOrder.TaxAmount = items.Sum(i => i.GSTAmount) + miscCharges.Sum(m => m.GSTAmount);
            purchaseOrder.TotalAmount = purchaseOrder.Amount + purchaseOrder.TaxAmount + miscCharges.Sum(m => m.TotalAmount);

            purchaseOrder.CreatedAt = DateTime.Now;
            purchaseOrder.Status = purchaseOrder.Status ?? "UnApproved";

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            // Add items
            foreach (var item in items)
            {
                item.PurchaseOrderId = purchaseOrder.Id;
                item.CreatedAt = DateTime.Now;
                _context.PurchaseOrderItems.Add(item);
            }

            // Add misc charges
            foreach (var charge in miscCharges)
            {
                charge.PurchaseOrderId = purchaseOrder.Id;
                charge.CreatedAt = DateTime.Now;
                _context.PurchaseOrderMiscCharges.Add(charge);
            }

            // Add terms and conditions
            foreach (var tc in termsConditions)
            {
                tc.PurchaseOrderId = purchaseOrder.Id;
                tc.CreatedAt = DateTime.Now;
                _context.PurchaseOrderTermsConditions.Add(tc);
            }

            await _context.SaveChangesAsync();

            return (true, "Purchase Order created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    private List<PurchaseOrderItem> GetItemsFromForm(IFormCollection form)
    {
        var items = new List<PurchaseOrderItem>();
        var itemIndex = 0;

        while (form.ContainsKey($"items[{itemIndex}].PurchaseItemId"))
        {
            var purchaseItemIdStr = form[$"items[{itemIndex}].PurchaseItemId"].ToString();
            var purchaseItemGroupIdStr = form[$"items[{itemIndex}].PurchaseItemGroupId"].ToString();
            var qtyStr = form[$"items[{itemIndex}].Qty"].ToString();
            var unitPriceStr = form[$"items[{itemIndex}].UnitPrice"].ToString();
            var discountStr = form[$"items[{itemIndex}].Discount"].ToString();
            var gstStr = form[$"items[{itemIndex}].GST"].ToString();

            if (int.TryParse(purchaseItemIdStr, out int purchaseItemId) && purchaseItemId > 0)
            {
                if (int.TryParse(purchaseItemGroupIdStr, out int purchaseItemGroupId) &&
                    decimal.TryParse(qtyStr, out decimal qty) &&
                    decimal.TryParse(unitPriceStr, out decimal unitPrice))
                {
                    decimal.TryParse(discountStr, out decimal discount);
                    var amount = qty * unitPrice;
                    var totalAmount = amount - discount;
                    decimal gstAmount = 0;
                    
                    if (!string.IsNullOrEmpty(gstStr) && gstStr != "NA")
                    {
                        if (decimal.TryParse(gstStr.Replace("%", ""), out decimal gstPercent))
                        {
                            gstAmount = totalAmount * (gstPercent / 100);
                        }
                    }

                    var item = new PurchaseOrderItem
                    {
                        PurchaseItemGroupId = purchaseItemGroupId,
                        PurchaseItemId = purchaseItemId,
                        ItemDescription = form[$"items[{itemIndex}].ItemDescription"].ToString(),
                        UOM = form[$"items[{itemIndex}].UOM"].ToString() ?? "",
                        Qty = qty,
                        UnitPrice = unitPrice,
                        Amount = amount,
                        Discount = discount,
                        TotalAmount = totalAmount,
                        GST = gstStr ?? "NA",
                        GSTAmount = gstAmount
                    };
                    items.Add(item);
                }
            }
            itemIndex++;
        }

        return items;
    }

    private List<PurchaseOrderMiscCharge> GetMiscChargesFromForm(IFormCollection form)
    {
        var charges = new List<PurchaseOrderMiscCharge>();
        var chargeIndex = 0;

        while (form.ContainsKey($"miscCharges[{chargeIndex}].ExpenseType"))
        {
            var expenseType = form[$"miscCharges[{chargeIndex}].ExpenseType"].ToString();
            var amountStr = form[$"miscCharges[{chargeIndex}].Amount"].ToString();
            var taxStr = form[$"miscCharges[{chargeIndex}].Tax"].ToString();

            if (!string.IsNullOrEmpty(expenseType) && decimal.TryParse(amountStr, out decimal amount))
            {
                decimal gstAmount = 0;
                if (!string.IsNullOrEmpty(taxStr) && taxStr != "Select")
                {
                    if (decimal.TryParse(taxStr.Replace("%", ""), out decimal taxPercent))
                    {
                        gstAmount = amount * (taxPercent / 100);
                    }
                }

                var charge = new PurchaseOrderMiscCharge
                {
                    ExpenseType = expenseType,
                    Amount = amount,
                    Tax = taxStr ?? "Select",
                    GSTAmount = gstAmount,
                    TotalAmount = amount + gstAmount
                };
                charges.Add(charge);
            }
            chargeIndex++;
        }

        return charges;
    }

    private List<PurchaseOrderTermsCondition> GetTermsConditionsFromForm(IFormCollection form)
    {
        var termsConditions = new List<PurchaseOrderTermsCondition>();

        // Get all checked terms and conditions checkboxes
        var tcValues = form["termsConditions"];
        if (tcValues.Count > 0)
        {
            foreach (var tcIdStr in tcValues)
            {
                if (int.TryParse(tcIdStr, out int tcId))
                {
                    termsConditions.Add(new PurchaseOrderTermsCondition
                    {
                        PurchaseOrderTCId = tcId,
                        IsSelected = true
                    });
                }
            }
        }

        return termsConditions;
    }

    public async Task LoadDropdownsAsync(dynamic viewBag)
    {
        var poTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "SELECT" },
            new SelectListItem { Value = "Regular", Text = "Regular" },
            new SelectListItem { Value = "Urgent", Text = "Urgent" }
        };
        viewBag.POType = new SelectList(poTypes, "Value", "Text");

        var statuses = new List<SelectListItem>
        {
            new SelectListItem { Value = "UnApproved", Text = "UnApproved" },
            new SelectListItem { Value = "Approved", Text = "Approved" },
            new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
        };
        viewBag.Status = new SelectList(statuses, "Value", "Text", "UnApproved");

        var gstOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "NA", Text = "NA" },
            new SelectListItem { Value = "5", Text = "5%" },
            new SelectListItem { Value = "12", Text = "12%" },
            new SelectListItem { Value = "18", Text = "18%" },
            new SelectListItem { Value = "28", Text = "28%" }
        };
        viewBag.GSTOptions = new SelectList(gstOptions, "Value", "Text", "NA");

        var taxOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "Select", Text = "Select" },
            new SelectListItem { Value = "5", Text = "5%" },
            new SelectListItem { Value = "12", Text = "12%" },
            new SelectListItem { Value = "18", Text = "18%" },
            new SelectListItem { Value = "28", Text = "28%" }
        };
        viewBag.TaxOptions = new SelectList(taxOptions, "Value", "Text", "Select");

        var vendors = await _context.BankMasters
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterGroup)
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterSubGroup)
            .Where(b => b.IsActive && b.Group != null && (
                b.Group.Name.Contains("Vendor") || 
                b.Group.Name.Contains("Vender") || 
                b.Group.Name.Contains("Creditor") || 
                b.Group.Name.Contains("Supplier") || 
                b.Group.Name.Contains("Sundry") ||
                (b.Group.MasterSubGroup != null && (b.Group.MasterSubGroup.Name.Contains("Vendor") || b.Group.MasterSubGroup.Name.Contains("Vender") || b.Group.MasterSubGroup.Name.Contains("Creditor"))) ||
                (b.Group.MasterGroup != null && (b.Group.MasterGroup.Name.Contains("Vendor") || b.Group.MasterGroup.Name.Contains("Vender") || b.Group.MasterGroup.Name.Contains("Creditor")))
            ))
            .Select(v => new { id = v.Id, name = v.AccountName })
            .ToListAsync();
        viewBag.Vendors = new SelectList(vendors, "id", "name");

        var accountTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "-- Select Type --" },
            new SelectListItem { Value = "Discount", Text = "Discount" },
            new SelectListItem { Value = "Packing Charges", Text = "Packing Charges" },
            new SelectListItem { Value = "Freight Charges", Text = "Freight Charges" },
            new SelectListItem { Value = "Other Charges", Text = "Other Charges" }
        };
        viewBag.AccountTypes = new SelectList(accountTypes, "Value", "Text");
    }

    public async Task<List<PurchaseOrder>> GetPurchaseOrderReportAsync(
        DateTime? fromDate, DateTime? toDate, string? vendorName, 
        string? itemGroup, string? itemName, string? uom, 
        string? billingTo, string? deliveryAddress, string? status)
    {
         var query = _context.PurchaseOrders
            .Include(po => po.Vendor)
            .Include(po => po.Items)
                .ThenInclude(item => item.PurchaseItemGroup)
            .Include(po => po.Items)
                .ThenInclude(item => item.PurchaseItem)
            .AsQueryable();

        if (fromDate.HasValue) query = query.Where(po => po.PODate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(po => po.PODate <= toDate.Value);
        if (!string.IsNullOrEmpty(vendorName)) query = query.Where(po => po.Vendor != null && po.Vendor.AccountName.Contains(vendorName));
        if (!string.IsNullOrEmpty(itemGroup)) query = query.Where(po => po.Items.Any(item => item.PurchaseItemGroup != null && item.PurchaseItemGroup.Name.Contains(itemGroup)));
        if (!string.IsNullOrEmpty(itemName)) query = query.Where(po => po.Items.Any(item => item.PurchaseItem != null && item.PurchaseItem.ItemName.Contains(itemName)));
        if (!string.IsNullOrEmpty(uom)) query = query.Where(po => po.Items.Any(item => item.UOM == uom));
        if (!string.IsNullOrEmpty(billingTo)) query = query.Where(po => po.BillingTo.Contains(billingTo));
        if (!string.IsNullOrEmpty(deliveryAddress)) query = query.Where(po => po.DeliveryAddress.Contains(deliveryAddress));
        if (!string.IsNullOrEmpty(status)) query = query.Where(po => po.Status == status);

        return await query.OrderByDescending(po => po.CreatedAt).ToListAsync();
    }

    public async Task LoadReportDropdownsAsync(dynamic viewBag)
    {
        var uomList = await _context.PurchaseItems
            .Where(pi => !string.IsNullOrEmpty(pi.UOM))
            .Select(pi => pi.UOM)
            .Distinct()
            .OrderBy(uom => uom)
            .ToListAsync();

        viewBag.UOMList = new SelectList(uomList);

        var statusList = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All" },
            new SelectListItem { Value = "Pending", Text = "Pending" },
            new SelectListItem { Value = "Approved", Text = "Approved" },
            new SelectListItem { Value = "Rejected", Text = "Rejected" },
            new SelectListItem { Value = "Completed", Text = "Completed" }
        };
        viewBag.StatusList = new SelectList(statusList, "Value", "Text");
    }

    public async Task<IEnumerable<LookupItem>> GetVendorsAsync(string? searchTerm)
    {
        var query = _context.BankMasters
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterGroup)
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterSubGroup)
            .Where(b => b.IsActive && b.Group != null && (
                b.Group.Name.Contains("Vendor") || 
                b.Group.Name.Contains("Vender") || 
                b.Group.Name.Contains("Creditor") || 
                b.Group.Name.Contains("Supplier") || 
                b.Group.Name.Contains("Sundry") ||
                (b.Group.MasterSubGroup != null && (b.Group.MasterSubGroup.Name.Contains("Vendor") || b.Group.MasterSubGroup.Name.Contains("Vender") || b.Group.MasterSubGroup.Name.Contains("Creditor"))) ||
                (b.Group.MasterGroup != null && (b.Group.MasterGroup.Name.Contains("Vendor") || b.Group.MasterGroup.Name.Contains("Vender") || b.Group.MasterGroup.Name.Contains("Creditor")))
            ))
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(v => v.AccountName.Contains(searchTerm));
        }

        return await query
            .OrderBy(v => v.AccountName)
            .Select(v => new LookupItem { Id = v.Id, Name = v.AccountName })
            .Take(50)
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetItemGroupsAsync()
    {
        return await _context.PurchaseItemGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new LookupItem { Id = g.Id, Name = g.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetItemsByGroupAsync(int groupId)
    {
        return await _context.PurchaseItems
            .Where(i => i.PurchaseItemGroupId == groupId && i.IsActive)
            .OrderBy(i => i.ItemName)
            .Select(i => new LookupItem { 
                Id = i.Id, 
                Name = i.ItemName,
                UOM = i.UOM,
                Rate = i.PurchaseCostingPerNos
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetTermsConditionsAsync()
    {
        return await _context.PurchaseOrderTCs
            .Where(tc => tc.IsActive && tc.TCType == "Annexure")
            .OrderBy(tc => tc.Caption)
            .Select(tc => new LookupItem { Id = tc.Id, Name = tc.Caption })
            .ToListAsync();
    }

    public async Task<(bool success, string message)> CreatePurchaseOrderAsync(PurchaseOrder model)
    {
        try
        {
            // Generate PO Number
            var lastPO = await _context.PurchaseOrders
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            
            int nextPONumber = 1;
            if (lastPO != null && !string.IsNullOrEmpty(lastPO.PONumber))
            {
                if (int.TryParse(lastPO.PONumber.Replace("PO", ""), out int lastNumber))
                {
                    nextPONumber = lastNumber + 1;
                }
            }
            model.PONumber = $"PO{nextPONumber:D6}";
            model.CreatedAt = DateTime.Now;
            model.Status = model.Status ?? "UnApproved";

            _context.PurchaseOrders.Add(model);
            
            foreach (var item in model.Items) { item.CreatedAt = DateTime.Now; }
            foreach (var charge in model.MiscCharges) { charge.CreatedAt = DateTime.Now; }
            foreach (var tc in model.TermsAndConditions) { tc.CreatedAt = DateTime.Now; }

            await _context.SaveChangesAsync();
            return (true, "Purchase Order created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }
}

