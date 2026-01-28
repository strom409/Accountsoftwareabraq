using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class PurchaseItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // Auto-generated Inventory Item Code
    public string InventoryType { get; set; } = "Packing Inventory"; // Storage Inventory / Packing Inventory
    [Required(ErrorMessage = "Inventory Item Group is required")]
    public int PurchaseItemGroupId { get; set; } // Foreign key to PurchaseItemGroup
    public string? BillingName { get; set; }
    public string ItemName { get; set; } = string.Empty; // Inventory Item Name
    [Required(ErrorMessage = "UOM is required")]
    public string UOM { get; set; } = string.Empty; // Unit of Measurement
    public decimal? MinimumStock { get; set; }
    public decimal? MaximumStock { get; set; }
    [Required(ErrorMessage = "Purchase Costing Per Nos. is required")]
    public decimal PurchaseCostingPerNos { get; set; } // Purchase Costing Per Nos.
    public decimal? SaleCostingPerNos { get; set; } // Sale Costing Per Nos.
    [Required(ErrorMessage = "GST is required")]
    public string GST { get; set; } = "NA"; // GST(%)
    public bool IsActive { get; set; } = true; // Status
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public PurchaseItemGroup? PurchaseItemGroup { get; set; }
    
    public int? VendorId { get; set; }
    public BankMaster? Vendor { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal OpeningQty { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal InwardQty { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal OutwardQty { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal CurrentStock { get; set; }
}

