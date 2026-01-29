using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbraqAccount.Models;

public class ReceiptEntry
{
    public int Id { get; set; }
    
    public string VoucherNo { get; set; } = string.Empty; // Auto-generated (e.g., RCPT/A/25-26/0038)
    
    [Required]
    public DateTime ReceiptDate { get; set; } = DateTime.Now;
    
    public string? MobileNo { get; set; }
    
    [Required]
    public string Type { get; set; } = "Credit"; // Credit, Debit
    
    [Required]
    public int AccountId { get; set; } // Account Master ID
    
    [Required]
    public string AccountType { get; set; } = string.Empty; // MasterGroup, MasterSubGroup, SubGroupLedger
    
    [Required]
    public string PaymentType { get; set; } = string.Empty; // Mobile Pay, Cash, Cheque, UTR, etc.
    
    [Required]
    public decimal Amount { get; set; }
    
    public string? RefNoChequeUTR { get; set; }
    
    public string? Narration { get; set; }
    
    public string Status { get; set; } = "Unapproved"; // Unapproved, Approved
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;

    private string? _accountName;
    [NotMapped]
    public string AccountName
    {
        get
        {
            if (!string.IsNullOrEmpty(_accountName)) return _accountName;
            if (MasterGroup != null) return MasterGroup.Name;
            if (MasterSubGroup != null) return $"{MasterSubGroup.MasterGroup?.Name ?? ""} - {MasterSubGroup.Name}";
            if (SubGroupLedger != null) return $"{SubGroupLedger.MasterGroup?.Name ?? ""} - {SubGroupLedger.MasterSubGroup?.Name ?? ""} - {SubGroupLedger.Name}";
            return "N/A";
        }
        set => _accountName = value;
    }
    
    // Navigation properties
    public MasterGroup? MasterGroup { get; set; }
    public MasterSubGroup? MasterSubGroup { get; set; }
    public SubGroupLedger? SubGroupLedger { get; set; }
    
    public int? PaymentFromSubGroupId { get; set; }
    public SubGroupLedger? PaymentFromSubGroup { get; set; }
    
    public int? EntryAccountId { get; set; } // Link to EntryForAccounts profile

    // New Columns for consistency
    public int? EntryForId { get; set; }
    public string? EntryForName { get; set; }
}


