using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbraqAccount.Models;

public class GeneralEntry
{
    public int Id { get; set; }
    
    public string VoucherNo { get; set; } = string.Empty; // Auto-generated (e.g., GE000001)
    
    [Required]
    public DateTime EntryDate { get; set; } = DateTime.Now;
    
    [Required]
    public int DebitAccountId { get; set; } // Master Group, Master Sub Group, or Sub Group Ledger
    
    [Required]
    public string DebitAccountType { get; set; } = string.Empty; // "MasterGroup", "MasterSubGroup", "SubGroupLedger"
    
    [Required]
    public int CreditAccountId { get; set; } // Master Group, Master Sub Group, or Sub Group Ledger
    
    [Required]
    public string CreditAccountType { get; set; } = string.Empty; // "MasterGroup", "MasterSubGroup", "SubGroupLedger"
    
    [Required]
    public decimal Amount { get; set; }
    
    public string? Type { get; set; } // e.g., "Expense to Bank", "Expense to Vendor", "Vendor to Vendor", "Vendor to Expense"
    
    public string? Narration { get; set; }
    
    public string? ReferenceNo { get; set; }
    
    public string? ImagePath { get; set; } // Path to uploaded image
    
    public string Status { get; set; } = "Unapproved"; // Unapproved, Approved
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;
    
    [NotMapped]
    public string? VoucherType { get; set; } // "Receipt Entry", "Debit Note", "Credit Note", "Journal Entry Book"

    [NotMapped]
    public string? PaymentType { get; set; } // "Mobile Pay", "Cash", etc.

    [NotMapped]
    public string DebitAccountName
    {
        get
        {
            if (DebitMasterGroup != null) return DebitMasterGroup.Name;
            if (DebitMasterSubGroup != null) return $"{DebitMasterSubGroup.MasterGroup?.Name ?? ""} - {DebitMasterSubGroup.Name}";
            if (DebitSubGroupLedger != null) return $"{DebitSubGroupLedger.MasterGroup?.Name ?? ""} - {DebitSubGroupLedger.MasterSubGroup?.Name ?? ""} - {DebitSubGroupLedger.Name}";
            if (DebitBankMasterInfo != null) return DebitBankMasterInfo.AccountName;
            if (DebitFarmer != null) return DebitFarmer.FarmerName;
            return "N/A";
        }
    }

    [NotMapped]
    public string CreditAccountName
    {
        get
        {
            if (CreditMasterGroup != null) return CreditMasterGroup.Name;
            if (CreditMasterSubGroup != null) return $"{CreditMasterSubGroup.MasterGroup?.Name ?? ""} - {CreditMasterSubGroup.Name}";
            if (CreditSubGroupLedger != null) return $"{CreditSubGroupLedger.MasterGroup?.Name ?? ""} - {CreditSubGroupLedger.MasterSubGroup?.Name ?? ""} - {CreditSubGroupLedger.Name}";
            if (CreditBankMasterInfo != null) return CreditBankMasterInfo.AccountName;
            if (CreditFarmer != null) return CreditFarmer.FarmerName;
            return "N/A";
        }
    }
    
    // Navigation properties for polymorphic association (loaded manually)
    public MasterGroup? DebitMasterGroup { get; set; }
    public MasterSubGroup? DebitMasterSubGroup { get; set; }
    public SubGroupLedger? DebitSubGroupLedger { get; set; }
    [NotMapped]
    public BankMaster? DebitBankMasterInfo { get; set; }
    
    public MasterGroup? CreditMasterGroup { get; set; }
    public MasterSubGroup? CreditMasterSubGroup { get; set; }
    public SubGroupLedger? CreditSubGroupLedger { get; set; }
    [NotMapped]
    public BankMaster? CreditBankMasterInfo { get; set; }

    [NotMapped]
    public Farmer? DebitFarmer { get; set; }
    [NotMapped]
    public Farmer? CreditFarmer { get; set; }

    public int? PaymentFromSubGroupId { get; set; }
    
    [StringLength(100)]
    public string? PaymentFromSubGroupName { get; set; } // Stores the Profile Name (e.g., "suahibic")

    public SubGroupLedger? PaymentFromSubGroup { get; set; }
    
    public int? EntryAccountId { get; set; } // Link to EntryForAccounts profile

    // New Columns for consistency
    public int? EntryForId { get; set; }
    public string? EntryForName { get; set; }
}


