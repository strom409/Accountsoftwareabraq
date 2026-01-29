using System;
using System.Collections.Generic;

namespace AbraqAccount.Models
{
    public class LedgerReportResult
    {
        public decimal OpeningBalance { get; set; }
        public List<GeneralEntry> Entries { get; set; } = new();
        public decimal ClosingBalance { get; set; }
    }
}
