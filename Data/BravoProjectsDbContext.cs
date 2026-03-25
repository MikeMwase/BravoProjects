using BravoProjects.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace BravoProjects.Data
{
    public class BravoProjectsDbContext : IdentityDbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BravoProjectsDbContext(
            DbContextOptions<BravoProjectsDbContext> options,
            IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }
        public DbSet<DailyExpense> DailyExpenses { get; set; }
        public DbSet<PaymentBreakdown> PaymentBreakdown { get; set; }
        public DbSet<Branch> Branches { get; set; }

        private int? GetCurrentUserBranchId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("BranchId")?.Value;
            return int.TryParse(claim, out int id) ? id : null;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Map to your existing singular DB table names
            modelBuilder.Entity<Student>().ToTable("Student");
            modelBuilder.Entity<DailyExpense>().ToTable("DailyExpense");
            modelBuilder.Entity<Branch>().ToTable("Branch");
            modelBuilder.Entity<Course>().ToTable("Course");
            modelBuilder.Entity<Enrollment>().ToTable("Enrollment");
            modelBuilder.Entity<PaymentRecord>().ToTable("PaymentRecord");
            modelBuilder.Entity<PaymentBreakdown>().ToTable("PaymentBreakdown");

            // ✅ Branch security filters — lambda is evaluated per query at request time
            modelBuilder.Entity<Student>()
                .HasQueryFilter(s => GetCurrentUserBranchId() == null
                                  || s.BranchId == GetCurrentUserBranchId());

            modelBuilder.Entity<DailyExpense>()
                .HasQueryFilter(de => GetCurrentUserBranchId() == null
                                   || de.BranchId == GetCurrentUserBranchId());
        }
    }
}