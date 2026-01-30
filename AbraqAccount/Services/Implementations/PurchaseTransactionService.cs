using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class PurchaseTransactionService : IPurchaseTransactionService
{
    private readonly AppDbContext _context;

    public PurchaseTransactionService(AppDbContext context)
    {
        _context = context;
    }

    #region Purchase Request

    public async Task<(List<PurchaseRequest> requests, int totalCount, int totalPages)> GetPurchaseRequestsAsync(
        string? poRequestNo, string? itemName, string? requestedBy, 
        string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        try
        {
            var query = _context.PurchaseRequests
                .Include(p => p.RequestedBy)
                .Include(p => p.AssignedTo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(poRequestNo))
            {
                query = query.Where(p => p.PORequestNo.Contains(poRequestNo));
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                query = query.Where(p => p.Items.Any(i => i.ItemName.Contains(itemName)));
            }

            if (!string.IsNullOrEmpty(requestedBy))
            {
                query = query.Where(p => 
                    (p.RequestedBy != null && p.RequestedBy.Username.Contains(requestedBy)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.RequestDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.RequestDate <= toDate.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var purchaseRequests = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (purchaseRequests, totalCount, totalPages);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreatePurchaseRequestAsync(PurchaseRequest request, IFormCollection? form)
    {
        if (form == null) return await CreatePurchaseRequestAsync(request);
        try
        {
            var items = GetRequestItemsFromForm(form);
            return await CreatePurchaseRequestWithItems(request, items);
        }
        catch (Exception ex)
        {
            return (false, "An error occurred: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> CreatePurchaseRequestAsync(PurchaseRequest model)
    {
        return await CreatePurchaseRequestWithItems(model, model.Items);
    }

    public async Task<PurchaseRequest?> GetPurchaseRequestByIdAsync(int id)
    {
        return await _context.PurchaseRequests
            .Include(p => p.RequestedBy)
            .Include(p => p.AssignedTo)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    private async Task<(bool success, string message)> CreatePurchaseRequestWithItems(PurchaseRequest request, List<PurchaseRequestItem> items)
    {
        try
        {
            var lastRequest = await _context.PurchaseRequests.OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            int nextRequestNo = 1;
            if (lastRequest != null && !string.IsNullOrEmpty(lastRequest.PORequestNo))
            {
                if (int.TryParse(lastRequest.PORequestNo.Replace("PR", ""), out int lastNumber)) nextRequestNo = lastNumber + 1;
            }
            request.PORequestNo = $"PR{nextRequestNo:D6}";
            request.CreatedAt = DateTime.Now;
            request.Status = request.Status ?? "Pending";

            _context.PurchaseRequests.Add(request);
            foreach (var item in items)
            {
                item.CreatedAt = DateTime.Now;
                _context.PurchaseRequestItems.Add(item);
            }

            await _context.SaveChangesAsync();
            return (true, "Purchase Request created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    private List<PurchaseRequestItem> GetRequestItemsFromForm(IFormCollection form)
    {
        try
        {
            var items = new List<PurchaseRequestItem>();
            var itemIndex = 0;

            while (form.ContainsKey($"items[{itemIndex}].ItemName"))
            {
                var itemName = form[$"items[{itemIndex}].ItemName"].ToString();
                var uom = form[$"items[{itemIndex}].UOM"].ToString();
                var qtyStr = form[$"items[{itemIndex}].Qty"].ToString();
                var useOfItem = form[$"items[{itemIndex}].UseOfItem"].ToString();

                if (!string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(uom) && 
                    !string.IsNullOrEmpty(qtyStr) && !string.IsNullOrEmpty(useOfItem))
                {
                    if (decimal.TryParse(qtyStr, out decimal qty))
                    {
                        var item = new PurchaseRequestItem
                        {
                            ItemName = itemName,
                            UOM = uom,
                            ItemDescription = form[$"items[{itemIndex}].ItemDescription"].ToString(),
                            Qty = qty,
                            UseOfItem = useOfItem,
                            ItemRemarks = form[$"items[{itemIndex}].ItemRemarks"].ToString(),
                            IsReturnable = form[$"items[{itemIndex}].IsReturnable"].ToString() == "true",
                            IsReusable = form[$"items[{itemIndex}].IsReusable"].ToString() == "true",
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

    public async Task LoadRequestDropdownsAsync(dynamic viewBag)
    {
        try
        {
            var users = await _context.Users
                .OrderBy(u => u.Username)
                .Select(u => new { id = u.Id, username = u.Username })
                .ToListAsync();

            viewBag.RequestedBy = new SelectList(users, "id", "username");
            viewBag.AssignedTo = new SelectList(users, "id", "username");

            var requestTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "SELECT" },
                new SelectListItem { Value = "Urgent", Text = "Urgent" },
                new SelectListItem { Value = "Normal", Text = "Normal" },
                new SelectListItem { Value = "Low Priority", Text = "Low Priority" }
            };
            viewBag.RequestType = new SelectList(requestTypes, "Value", "Text");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetUsersAsync(string? searchTerm)
    {
        try
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(searchTerm)) query = query.Where(u => u.Username.Contains(searchTerm));
            
            return await query
                .OrderBy(u => u.Username)
                .Select(u => new LookupItem { Id = u.Id, Name = u.Username })
                .Take(50)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }


    #endregion

    #region Purchase Receive

    public async Task<(List<PurchaseReceive> receives, int totalCount, int totalPages)> GetPurchaseReceivesAsync(
        string? receiptNo, string? poNumber, string? vendorName, 
        string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        try
        {
            var query = _context.PurchaseReceives
                .Include(p => p.Vendor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(receiptNo)) query = query.Where(p => p.ReceiptNo.Contains(receiptNo));
            if (!string.IsNullOrEmpty(poNumber)) query = query.Where(p => p.PONumber.Contains(poNumber));
            if (!string.IsNullOrEmpty(vendorName)) query = query.Where(p => p.Vendor != null && p.Vendor.AccountName.Contains(vendorName));
            if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
            if (fromDate.HasValue) query = query.Where(p => p.ReceivedDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(p => p.ReceivedDate <= toDate.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var receives = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (receives, totalCount, totalPages);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreatePurchaseReceiveAsync(PurchaseReceive model, Microsoft.AspNetCore.Http.IFormFile? scannedCopyBill, string webRootPath)
    {
        if (scannedCopyBill == null) return await CreatePurchaseReceiveAsync(model);
        try
        {
            // Generate Receipt Number
            var lastReceive = await _context.PurchaseReceives.OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            int nextReceiptNo = 1;
            if (lastReceive != null && !string.IsNullOrEmpty(lastReceive.ReceiptNo))
            {
                if (int.TryParse(lastReceive.ReceiptNo.Replace("PR", ""), out int lastNumber)) nextReceiptNo = lastNumber + 1;
            }
            model.ReceiptNo = $"PR{nextReceiptNo:D6}";

            // Handle file upload
            if (scannedCopyBill != null && scannedCopyBill.Length > 0)
            {
                var uploadsFolder = Path.Combine(webRootPath, "uploads", "bills");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}_{scannedCopyBill.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await scannedCopyBill.CopyToAsync(stream);
                }
                
                model.ScannedCopyBillPath = $"/uploads/bills/{fileName}";
            }

            model.CreatedAt = DateTime.Now;
            model.Status = model.Status ?? "Completed";

            _context.PurchaseReceives.Add(model);
            await _context.SaveChangesAsync();

            return (true, "Purchase Receive created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> CreatePurchaseReceiveAsync(PurchaseReceive model)
    {
        try
        {
            var lastReceive = await _context.PurchaseReceives.OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            int nextReceiptNo = 1;
            if (lastReceive != null && !string.IsNullOrEmpty(lastReceive.ReceiptNo))
            {
                if (int.TryParse(lastReceive.ReceiptNo.Replace("PR", ""), out int lastNumber)) nextReceiptNo = lastNumber + 1;
            }
            model.ReceiptNo = $"PR{nextReceiptNo:D6}";
            model.CreatedAt = DateTime.Now;
            model.Status = model.Status ?? "Completed";

            _context.PurchaseReceives.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Purchase Receive created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task LoadReceiveDropdownsAsync(dynamic viewBag)
    {
        try
        {
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
                .OrderBy(b => b.AccountName)
                .Select(v => new { id = v.Id, name = v.AccountName })
                .ToListAsync();

            viewBag.Vendors = new SelectList(vendors, "id", "name");

            var masterGroups = await _context.MasterGroups
                .OrderBy(mg => mg.Name)
                .Select(mg => new { id = mg.Id, name = mg.Name })
                .ToListAsync();

            viewBag.ExpenseGroups = new SelectList(masterGroups, "id", "name");

            var purchaseTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Purchase Order", Text = "Purchase Order" },
                new SelectListItem { Value = "Purchase Request", Text = "Purchase Request" },
                new SelectListItem { Value = "Direct Purchase", Text = "Direct Purchase" }
            };
            viewBag.PurchaseTypes = new SelectList(purchaseTypes, "Value", "Text");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetVendorsAsync(string? searchTerm)
    {
        try
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

            if (!string.IsNullOrEmpty(searchTerm)) query = query.Where(v => v.AccountName.Contains(searchTerm));
            
            return await query
                .OrderBy(v => v.AccountName)
                .Select(v => new LookupItem { Id = v.Id, Name = v.AccountName })
                .Take(50)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetPONumbersAsync(string? searchTerm)
    {
        try
        {
            var query = _context.PurchaseOrders.AsQueryable();
            if (!string.IsNullOrEmpty(searchTerm)) query = query.Where(po => po.PONumber.Contains(searchTerm));
            
            return await query
                .OrderByDescending(po => po.CreatedAt)
                .Select(po => new LookupItem { Name = po.PONumber }) // PO Number as Name
                .Take(50)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetMasterSubGroupsAsync(int masterGroupId)
    {
        try
        {
            return await _context.MasterSubGroups
                .Where(msg => msg.MasterGroupId == masterGroupId && msg.IsActive)
                .OrderBy(msg => msg.Name)
                .Select(msg => new LookupItem { Id = msg.Id, Name = msg.Name })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetSubGroupLedgersAsync(int masterSubGroupId)
    {
        try
        {
            return await _context.SubGroupLedgers
                .Where(sgl => sgl.MasterSubGroupId == masterSubGroupId && sgl.IsActive)
                .OrderBy(sgl => sgl.Name)
                .Select(sgl => new LookupItem { Id = sgl.Id, Name = sgl.Name })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

