namespace BravoProjects.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } // e.g., "Johannesburg", "Pretoria"
        public string Location { get; set; }

        // Navigation property
        public virtual ICollection<Student> Students { get; set; }
    }
}
