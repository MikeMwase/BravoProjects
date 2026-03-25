using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BravoProjects.Models
{
    public class Course
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Course Name")]
        public required string Title { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Standard Fee")]
        public decimal StandardFee { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Course Expense")]
        public decimal CourseExpense { get; set; } // The cost to run this course per student

        public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}