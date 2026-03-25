namespace BravoProjects.Models
{
    public class PaymentRecord
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }

        // Keep this! It ensures your existing 2400 payments remain visible.
        public decimal AmountPaid { get; set; }

        public string ReceiptNo { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime DatePaid { get; set; }

        public virtual Enrollment Enrollment { get; set; }

        // NEW: This allows the 1900 and 500 split
        public virtual ICollection<PaymentBreakdown> Breakdowns { get; set; } = new List<PaymentBreakdown>();
    }

    public class PaymentBreakdown
    {
        public int Id { get; set; }
        public int PaymentRecordId { get; set; }

        public string ExpenseType { get; set; } // e.g., "Course Fee", "Registration", "Admin"
        public decimal Amount { get; set; }

        public virtual PaymentRecord PaymentRecord { get; set; }
    }
}