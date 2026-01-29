using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class ExpensesIncurred
{
    public int Id { get; set; }
    
    public string VoucherNo { get; set; } = string.Empty; // Auto-generated (e.g., EXP000001)
    
    [Required(ErrorMessage = "Expense Date is required")]
    public DateTime? ExpenseDate { get; set; } = DateTime.Now;
    
    public int? ExpenseGroupId { get; set; } // Expense Group (Master Group)
    
    public int? ExpenseSubGroupId { get; set; } // Expense Sub Group (Master Sub Group)
    
    public int? ExpenseLedgerId { get; set; } // Expense Ledger (Sub Group Ledger)
    
    public int? DebitAccountId { get; set; }
    public string? DebitAccountName { get; set; }
    
    public string? Unit { get; set; }
    
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal? Amount { get; set; }
    
    public decimal TotalDiscount { get; set; } = 0; // Header-level discount
    
    public string? PaymentMode { get; set; } // Cash, Cheque, NEFT, RTGS, etc.
    
    public string? ReferenceNo { get; set; }
    
    public string? Narration { get; set; }
    
    public int? VendorId { get; set; }
    
    public string? VendorName { get; set; }
    
    public string? POType { get; set; } // PO, Non-PO
    
    public string? PANNo { get; set; }
    
    public string? FirmType { get; set; }
    
    [Required(ErrorMessage = "Bill Date is required")]
    public DateTime? BillDate { get; set; } = DateTime.Now;
    
    public string? BillNo { get; set; }
    
    public string? VehicleNo { get; set; }
    
    public string? Remarks { get; set; }
    
    public string Status { get; set; } = "Unapproved"; // Unapproved, Approved
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public MasterGroup? ExpenseGroup { get; set; }
    public MasterSubGroup? ExpenseSubGroup { get; set; }
    public SubGroupLedger? ExpenseLedger { get; set; }
    
    public List<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
    public List<ExpenseMiscCharge> MiscCharges { get; set; } = new List<ExpenseMiscCharge>();
}

