using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class PurchaseOrder
{
    public int Id { get; set; }
    
    public string PONumber { get; set; } = string.Empty; // Auto-generated
    
    [Required]
    public DateTime PODate { get; set; } = DateTime.Now;
    
    [Required]
    public string POType { get; set; } = string.Empty;
    
    [Required]
    public int VendorId { get; set; }
    
    [Required]
    public DateTime ExpectedReceivedDate { get; set; }
    
    public string? VendorReference { get; set; }
    
    [Required]
    public string BillingTo { get; set; } = string.Empty;
    
    [Required]
    public string DeliveryAddress { get; set; } = string.Empty;
    
    public string? Remarks { get; set; }
    
    public decimal POQty { get; set; }
    
    public decimal Amount { get; set; }
    
    public decimal TaxAmount { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public string Status { get; set; } = "UnApproved"; // UnApproved, Approved, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public BankMaster? Vendor { get; set; }
    public List<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public List<PurchaseOrderMiscCharge> MiscCharges { get; set; } = new List<PurchaseOrderMiscCharge>();
    public List<PurchaseOrderTermsCondition> TermsAndConditions { get; set; } = new List<PurchaseOrderTermsCondition>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int PurchaseItemGroupId { get; set; }
    public int PurchaseItemId { get; set; }
    public string? ItemDescription { get; set; }
    public string UOM { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string GST { get; set; } = "NA";
    public decimal GSTAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public PurchaseOrder? PurchaseOrder { get; set; }
    public PurchaseItemGroup? PurchaseItemGroup { get; set; }
    public PurchaseItem? PurchaseItem { get; set; }
}

public class PurchaseOrderMiscCharge
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Tax { get; set; } = "Select";
    public decimal GSTAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation property
    public PurchaseOrder? PurchaseOrder { get; set; }
}

public class PurchaseOrderTermsCondition
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int PurchaseOrderTCId { get; set; } // Reference to PurchaseOrderTC table
    public bool IsSelected { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Models.PurchaseOrderTC? TermsAndConditions { get; set; }
}


