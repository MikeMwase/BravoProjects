using System.ComponentModel.DataAnnotations;

namespace BravoProjects.Models
{
    public class Enrollment
    {
        public int Id { get; set; }

        // Relationships
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int CourseId { get; set; }
        public Course? Course { get; set; }

        // From your screenshot: Date, Amount, Balance, Receipt, Method
        public string? Month { get; set; } // e.g., "January", "February", etc.
        public DateTime Date { get; set; } = DateTime.Now;

        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }

        public string? ReceiptNo { get; set; }

        public string? PaymentStatus { get; set; } // e.g., "PAID", "PENDING"

        [Required]
        public string? MethodOfPayment { get; set; } // e.g., "CASH", "CARD", "CASH & CARD"

        // ADD THIS LINE: This fixes the CS1061 error in Details.cshtml
        public virtual ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
    }
}
