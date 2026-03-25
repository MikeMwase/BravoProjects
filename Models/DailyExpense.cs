using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BravoProjects.Models
{
    public class DailyExpense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Expense Date")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        public string Description { get; set; } // e.g., "Stationery for office"

        [Required]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than zero")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required]
        public string Category { get; set; } // e.g., Petrol, Refreshments, Maintenance

        public string Reference { get; set; } // Slip number or vendor name

        // --- THE SECURITY FIX: BRANCH LINKING ---

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        public virtual Branch Branch { get; set; }
    }
}