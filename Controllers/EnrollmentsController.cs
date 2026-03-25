using BravoProjects.Data;
using BravoProjects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BravoProjects.Controllers
{
    [Authorize] // This locks the whole controller
    public class EnrollmentsController : Controller
    {
        private readonly BravoProjectsDbContext _context;

        public EnrollmentsController(BravoProjectsDbContext context)
        {
            _context = context;
        }

        // GET: Enrollments
        public async Task<IActionResult> Index(DateTime? reportDate, string searchString, int? courseId, int? branchId)
        {
            // 1. Maintain State
            var date = reportDate ?? DateTime.Today;
            ViewData["ReportDate"] = date.ToString("yyyy-MM-dd");
            ViewData["CurrentFilter"] = searchString;
            ViewData["SelectedBranch"] = branchId;

            // 2. Build Enrollment Query
            var query = _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                    .ThenInclude(s => s.Branch) // Crucial for showing the Branch name in the detail table
                .AsQueryable();

            // --- ADMIN OVERRIDE & BRANCH FILTERING ---
            if (User.IsInRole("Admin"))
            {
                query = query.IgnoreQueryFilters(); // Lift the automatic restriction

                // If Admin selected a specific branch from the dropdown
                if (branchId.HasValue)
                {
                    query = query.Where(e => e.Student.BranchId == branchId.Value);
                }

                // Prepare the Branch dropdown list for the Admin
                ViewBag.Branches = new SelectList(_context.Branches, "Id", "Name", branchId);
            }

            // 3. Apply Standard Filters (Search or Date)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(e => e.Course.Title == searchString ||
                                     e.Student.FirstName.Contains(searchString) ||
                                     e.Student.LastName.Contains(searchString));
            }
            else
            {
                query = query.Where(e => e.Date.Date == date.Date);
            }

            if (courseId.HasValue)
            {
                query = query.Where(e => e.CourseId == courseId.Value);
            }

            var enrollments = await query.ToListAsync();

            // 4. Expenses Calculation
            var expenseQuery = _context.DailyExpenses.AsQueryable();
            if (User.IsInRole("Admin"))
            {
                expenseQuery = expenseQuery.IgnoreQueryFilters();
                if (branchId.HasValue)
                {
                    expenseQuery = expenseQuery.Where(e => e.BranchId == branchId.Value);
                }
            }

            var totalExpenses = await expenseQuery
                .Where(e => e.Date.Date == date.Date)
                .SumAsync(x => x.Amount);

            ViewBag.ExpensesTotal = totalExpenses;

            return View(enrollments);
        }

        // GET: Enrollments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var enrollment = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course) // Crucial for getting the Price
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enrollment == null) return NotFound();

            return View(enrollment);
        }

        // GET: Enrollments/Create
        public IActionResult Create(int? studentId)
        {
            // Pre-select the student if the ID was passed from the previous step
            if (studentId.HasValue)
            {
                ViewData["StudentId"] = new SelectList(_context.Students, "Id", "FirstName", studentId);
                // Optional: You can also find the student name to display a "Enrolling: [Name]" message
            }
            else
            {
                ViewData["StudentId"] = new SelectList(_context.Students, "Id", "FirstName");
            }

            ViewData["CourseId"] = new SelectList(_context.Courses, "Id", "Title");
            return View();
        }

        // POST: Enrollments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,StudentId,CourseId,Date,AmountPaid,Balance,ReceiptNo,PaymentStatus,MethodOfPayment")] Enrollment enrollment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(enrollment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "Id", "Title", enrollment.CourseId);
            ViewData["StudentId"] = new SelectList(_context.Students, "Id", "Id", enrollment.StudentId);
            return View(enrollment);
        }

        // GET: Enrollments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment == null)
            {
                return NotFound();
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "Id", "Title", enrollment.CourseId);
            ViewData["StudentId"] = new SelectList(_context.Students, "Id", "Id", enrollment.StudentId);
            return View(enrollment);
        }

        // POST: Enrollments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StudentId,CourseId,Date,AmountPaid,Balance,ReceiptNo,PaymentStatus,MethodOfPayment")] Enrollment enrollment)
        {
            if (id != enrollment.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(enrollment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EnrollmentExists(enrollment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "Id", "Title", enrollment.CourseId);
            ViewData["StudentId"] = new SelectList(_context.Students, "Id", "Id", enrollment.StudentId);
            return View(enrollment);
        }

        // GET: Enrollments/AddPayment/5
        public async Task<IActionResult> AddPayment(int id)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(m => m.Id == id);

            return View(enrollment);
        }

        // POST: Enrollments/AddPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int id, decimal additionalAmount, string newReceiptNo, string method)
        {
            var enrollment = await _context.Enrollments.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == id);

            if (enrollment != null)
            {
                // 1. Create the history record (The Audit Trail)
                var history = new PaymentRecord
                {
                    EnrollmentId = id,
                    AmountPaid = additionalAmount,
                    ReceiptNo = newReceiptNo,
                    PaymentMethod = method,
                    DatePaid = DateTime.Now
                };
                _context.PaymentRecords.Add(history);

                // 2. Update the main Enrollment totals for the badges
                enrollment.AmountPaid += additionalAmount;
                enrollment.PaymentStatus = enrollment.AmountPaid >= enrollment.Course.StandardFee ? "Paid" : "Partial";

                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Students", new { id = enrollment.StudentId });
            }
            return NotFound();
        }

        // GET: Enrollments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (enrollment == null)
            {
                return NotFound();
            }

            return View(enrollment);
        }

        // POST: Enrollments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EnrollmentExists(int id)
        {
            return _context.Enrollments.Any(e => e.Id == id);
        }
    }
}
