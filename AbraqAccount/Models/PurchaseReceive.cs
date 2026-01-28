using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class PurchaseReceive
{
    public int Id { get; set; }
    
    public string ReceiptNo { get; set; } = string.Empty; // Auto-generated
    
    [Required]
    public string PONumber { get; set; } = string.Empty; // PO No./PO Request
    
    [Required]
    public DateTime Date { get; set; } = DateTime.Now;
    
    [Required]
    public int VendorId { get; set; }
    
    [Required]
    public string PurchaseType { get; set; } = string.Empty;
    
    public string? PANNo { get; set; }
    
    public string? FirmType { get; set; }
    
    [Required]
    public DateTime ReceivedDate { get; set; } = DateTime.Now;
    
    [Required]
    public DateTime BillDate { get; set; } = DateTime.Now;
    
    [Required]
    public string VendorGSTNo { get; set; } = string.Empty;
    
    [Required]
    public int ExpenseGroupId { get; set; } // Master Group
    
    [Required]
    public int ExpenseSubGroupId { get; set; } // Master Sub Group
    
    [Required]
    public int ExpenseLedgerId { get; set; } // Sub Group Ledger
    
    [Required]
    public string VehicleNo { get; set; } = string.Empty;
    
    public string? Remarks { get; set; }
    
    [Required]
    public string BillNo { get; set; } = string.Empty;
    
    public string? ScannedCopyBillPath { get; set; } // File path for uploaded bill
    
    public decimal? Qty { get; set; }
    
    public decimal? Amount { get; set; }
    
    public string Status { get; set; } = "Completed"; // Completed, Pending, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public BankMaster? Vendor { get; set; }
    public MasterGroup? ExpenseGroup { get; set; }
    public MasterSubGroup? ExpenseSubGroup { get; set; }
    public SubGroupLedger? ExpenseLedger { get; set; }
    public List<PurchaseReceiveItem> Items { get; set; } = new List<PurchaseReceiveItem>();
}

public class PurchaseReceiveItem
{
    public int Id { get; set; }
    public int PurchaseReceiveId { get; set; }
    
    public int? PurchaseItemId { get; set; }
    
    public string? ItemName { get; set; }
    
    public string? UOM { get; set; }
    
    public decimal Qty { get; set; }
    
    public decimal? UnitPrice { get; set; }
    
    public decimal? Amount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation property
    public PurchaseReceive? PurchaseReceive { get; set; }
    public PurchaseItem? PurchaseItem { get; set; }
}


