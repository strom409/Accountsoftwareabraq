using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class PaymentSettlement
{
    public int Id { get; set; }
    
    public string PANumber { get; set; } = string.Empty;
    
    [Required]
    public DateTime SettlementDate { get; set; } = DateTime.Now;
    
    [Required]
    public string Type { get; set; } = "Credit"; // Credit, Debit
    
    [Required]
    public int AccountId { get; set; }
    
    [Required]
    public string AccountName { get; set; } = string.Empty; // Stored explicitly
    
    [Required]
    public string AccountType { get; set; } = string.Empty; // Vendor, BankMaster, etc.
    
    [Required]
    public string PaymentType { get; set; } = string.Empty; // Cash, Cheque, etc.
    
    [Required]
    public decimal Amount { get; set; }
    
    public string? RefNo { get; set; }
    
    public string? Narration { get; set; }
    
    public string ApprovalStatus { get; set; } = "Unapproved";
    
    public string PaymentStatus { get; set; } = "Pending";
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    public string? Unit { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Optional: Keep relationships if needed, but primarily relying on stored AccountId
    // We can remove direct Vendor navigation to avoid confusion if it's not always a Vendor
    
    public int? EntryAccountId { get; set; } // Link to EntryForAccounts profile

    // New Columns for consistency
    public int? EntryForId { get; set; }
    public string? EntryForName { get; set; }
}


