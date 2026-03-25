using BravoProjects.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BravoProjects.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            BravoProjectsDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager)
        {
            context.Database.EnsureCreated();

            // --- 1. SEED ROLES ---
            string[] roleNames = { "Admin", "Staff" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // --- 2. SEED ADMIN USERS ---
            var adminUsers = new List<(string Email, string Name)>
            {
                ("daniel.mogano@bravoprojects.co.za", "Daniel Mogano"),
                ("james.mogano@bravoprojects.co.za", "James Mogano"),
                ("mike.nqoko@bravoprojects.co.za", "Sibongile Mike Nqoko")
            };

            foreach (var admin in adminUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(admin.Email);
                if (existingUser == null)
                {
                    var newUser = new IdentityUser
                    {
                        UserName = admin.Email,
                        Email = admin.Email,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(newUser, "BravoAdmin2026!");
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, "Admin");
                    }
                }
            }

            // --- 3. SEED BRANCHES (NEW) ---
            if (!context.Branches.Any())
            {
                var mainBranch = new Branch { Name = "Head Office", Location = "Roodepoort" };
                var jhbBranch = new Branch { Name = "Johannesburg Branch", Location = "Johannesburg" };
                var gerBranch = new Branch { Name = "Germiston Branch", Location = "Germiston" };
                var plkBranch = new Branch { Name = "Polokwane Branch", Location = "Polokwane" };
               
                context.Branches.AddRange(mainBranch, jhbBranch, gerBranch, plkBranch);
                await context.SaveChangesAsync();
            }

            // Get a reference to a branch for student assignment
            var defaultBranch = await context.Branches.FirstAsync();

            // --- 4. SEED COURSES ---
            // Note: Updated to singular 'context.Course'
            if (context.Courses.Any()) return;

            var edc = new Course { Title = "Grade EDC", StandardFee = 1900.00m };
            var gradeB = new Course { Title = "Grade B", StandardFee = 1300.00m };
            var gradeA = new Course { Title = "Grade A", StandardFee = 1400.00m };
            var psiraReg = new Course { Title = "PSIRA Registration", StandardFee = 500.00m };
            // ... [Keep your other course variables here] ...

            context.Courses.AddRange(edc, gradeB, gradeA, psiraReg /* add others */);
            await context.SaveChangesAsync();

            // --- 5. SEED INITIAL STUDENTS & ENROLLMENTS ---
            var student1 = new Student
            {
                FirstName = "Mwamba Nkongolo",
                LastName = "Mwewa",
                IdNumber = "9309096745183",
                PhoneNumber = "0676912327",
                BranchId = defaultBranch.Id // Assigning the seeded branch
            };

            var enrollment1 = new Enrollment
            {
                Student = student1,
                Course = edc,
                Date = new DateTime(2025, 07, 01),
                AmountPaid = 1200.00m,
                ReceiptNo = "3501",
                PaymentStatus = "Partial",
                MethodOfPayment = "CASH"
            };

            // Note: Updated to singular 'context.Enrollment'
            context.Enrollments.Add(enrollment1);
            await context.SaveChangesAsync();
        }
    }
}