namespace BravoProjects.Models
{
    public class EnrollmentViewModel
    {
        public int StudentId { get; set; }
        public DateTime Date { get; set; }
        public string MethodOfPayment { get; set; }
        public string ReceiptNo { get; set; }
        public List<int> SelectedCourseIds { get; set; } // This captures the checkboxes
    }
}
