namespace BravoProjects.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? IdNumber { get; set; } // Based on your 'ID Number' column
        public string? PhoneNumber { get; set; }

        public int BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        // Relationship: One student can have many enrollments
        public List<Enrollment> Enrollments { get; set; } = new();
    }
}
