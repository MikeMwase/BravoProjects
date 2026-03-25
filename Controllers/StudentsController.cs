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
    public class StudentsController : Controller
    {
        private readonly BravoProjectsDbContext _context;

        public StudentsController(BravoProjectsDbContext context)
        {
            _context = context;
        }

        // GET: Students
        public async Task<IActionResult> Index(string searchString, int? courseId, int? branchId, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCourse"] = courseId;
            ViewData["CurrentBranch"] = branchId;

            int pageSize = 10;
            int pageIdx = pageNumber ?? 1;

            // 1. Start the query
            var studentsQuery = _context.Students.Include(s => s.Branch).AsQueryable();

            // 2. Role-Based Branch Logic
            if (User.IsInRole("Admin"))
            {
                // Admins can see any branch. If they pick one from the dropdown, filter it.
                if (branchId.HasValue)
                {
                    studentsQuery = studentsQuery.Where(s => s.BranchId == branchId.Value);
                }
            }
            else
            {
                // Staff are forced to see only their assigned branch
                // This assumes you stored "BranchId" as a Claim during login
                var userBranchIdClaim = User.FindFirst("BranchId")?.Value;
                if (int.TryParse(userBranchIdClaim, out int staffBranchId))
                {
                    studentsQuery = studentsQuery.Where(s => s.BranchId == staffBranchId);
                }
            }

            // 3. Apply Search and Course Filters
            if (!string.IsNullOrEmpty(searchString))
            {
                studentsQuery = studentsQuery.Where(s => s.FirstName.Contains(searchString)
                                               || s.LastName.Contains(searchString)
                                               || s.IdNumber.Contains(searchString));
            }

            if (courseId.HasValue)
            {
                studentsQuery = studentsQuery.Where(s => s.Enrollments.Any(e => e.CourseId == courseId.Value));
            }

            // 4. Populate Dropdowns (Admins see all branches, Staff see none or just their own)
            ViewBag.CourseList = new SelectList(await _context.Courses.OrderBy(c => c.Title).ToListAsync(), "Id", "Title", courseId);

            if (User.IsInRole("Admin"))
            {
                ViewBag.BranchList = new SelectList(await _context.Branches.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", branchId);
            }

            // 5. Final Paging
            var count = await studentsQuery.CountAsync();
            var items = await studentsQuery
                .OrderBy(s => s.LastName)
                .Skip((pageIdx - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["PageIndex"] = pageIdx;
            ViewData["HasPreviousPage"] = pageIdx > 1;
            ViewData["HasNextPage"] = pageIdx < (int)Math.Ceiling(count / (double)pageSize);

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Course)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.PaymentRecords)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (student == null) return NotFound();

            // ADD THIS LINE: This provides the data for the "Use Credit" dropdown
            ViewBag.AllCourses = await _context.Courses.OrderBy(c => c.Title).ToListAsync();

            return View(student);
        }

        // GET: Students/Create
        // This is the "One Click" fix. It loads the full form immediately.
        public IActionResult Create()
        {
            // Pack the bag with courses so the checkboxes appear on the first try
            ViewBag.Courses = _context.Courses.OrderBy(c => c.Title).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,IdNumber,PhoneNumber")] Student student, int[] selectedCourseIds, string methodOfPayment, string receiptNo, decimal amountPaidNow)
        {
            // --- 0. BRANCH SECURITY CHECK ---
            // If the user is Staff, we force the Student's branch to match the Staff's claim.
            var branchClaim = User.FindFirst("BranchId")?.Value;
            if (branchClaim != null)
            {
                student.BranchId = int.Parse(branchClaim);
            }
            // Note: If Admin, ensure student.BranchId is handled (either from a dropdown or default)

            // 1. UNIQUE RECEIPT CHECK
            if (!string.IsNullOrEmpty(receiptNo))
            {
                // Using singular 'PaymentRecord' to match your DB fix
                var duplicateReceipt = await _context.PaymentRecords
                    .AnyAsync(r => r.ReceiptNo == receiptNo);

                if (duplicateReceipt)
                {
                    ModelState.AddModelError("receiptNo", "This Receipt Number has already been used.");
                }
            }

            // 2. CHECK FOR EXISTING STUDENT
            // Using singular 'Student'
            var existingStudent = await _context.Students
                .Include(s => s.Enrollments)
                .FirstOrDefaultAsync(s => s.IdNumber == student.IdNumber);

            if (ModelState.IsValid)
            {
                int targetStudentId;
                if (existingStudent != null)
                {
                    targetStudentId = existingStudent.Id;

                    // SECURITY UPDATE: If a student moves branches, update their record to the current branch
                    if (student.BranchId != 0 && existingStudent.BranchId != student.BranchId)
                    {
                        existingStudent.BranchId = student.BranchId;
                        _context.Update(existingStudent);
                    }
                }
                else
                {
                    _context.Add(student);
                    await _context.SaveChangesAsync();
                    targetStudentId = student.Id;
                }

                // 3. ALLOCATION & ENROLLMENT LOGIC
                if (selectedCourseIds != null && selectedCourseIds.Length > 0)
                {
                    decimal remainingMoney = amountPaidNow;

                    // Using singular 'Course'
                    var sortedCourses = await _context.Courses
                        .Where(c => selectedCourseIds.Contains(c.Id))
                        .OrderByDescending(c => c.StandardFee)
                        .ToListAsync();

                    foreach (var course in sortedCourses)
                    {
                        // Check if already enrolled in this specific course
                        bool alreadyEnrolled = existingStudent?.Enrollments.Any(e => e.CourseId == course.Id) ?? false;
                        if (alreadyEnrolled) continue;

                        decimal allocationForThisCourse = 0;
                        string status = "Unpaid";

                        if (remainingMoney >= course.StandardFee)
                        {
                            allocationForThisCourse = course.StandardFee;
                            remainingMoney -= course.StandardFee;
                            status = "Paid";
                        }
                        else if (remainingMoney > 0)
                        {
                            allocationForThisCourse = remainingMoney;
                            remainingMoney = 0;
                            status = "Partial";
                        }

                        // Create the Enrollment Record (Singular)
                        var enrollment = new Enrollment
                        {
                            StudentId = targetStudentId,
                            CourseId = course.Id,
                            Date = DateTime.Now,
                            MethodOfPayment = methodOfPayment ?? "CASH",
                            ReceiptNo = receiptNo,
                            AmountPaid = allocationForThisCourse,
                            PaymentStatus = status,
                            Month = DateTime.Now.ToString("MMMM").ToUpper()
                        };

                        _context.Add(enrollment);
                        await _context.SaveChangesAsync();

                        // 4. CREATE PAYMENT RECORD (Singular)
                        if (allocationForThisCourse > 0)
                        {
                            var paymentRecord = new PaymentRecord
                            {
                                EnrollmentId = enrollment.Id,
                                AmountPaid = allocationForThisCourse,
                                ReceiptNo = receiptNo,
                                PaymentMethod = methodOfPayment ?? "CASH",
                                DatePaid = DateTime.Now,
                                // Using singular 'PaymentBreakdown'
                                Breakdowns = new List<PaymentBreakdown>
                        {
                            new PaymentBreakdown
                            {
                                ExpenseType = course.Title,
                                Amount = allocationForThisCourse
                            }
                        }
                            };
                            _context.Add(paymentRecord);
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }

            // RECOVERY: Reload courses if validation fails
            ViewBag.Courses = _context.Courses.OrderBy(c => c.Title).ToListAsync();
            return View(student);
        }

        // NEW: Process Refund for overpayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRefund(int studentId, decimal amount, int enrollmentId)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enrollment != null)
            {
                // Create negative payment for audit trail
                var refundRecord = new PaymentRecord
                {
                    EnrollmentId = enrollmentId,
                    AmountPaid = -amount,
                    ReceiptNo = "RFND-" + DateTime.Now.ToString("MMddHHmm"),
                    PaymentMethod = "CASH",
                    DatePaid = DateTime.Now
                };

                // Re-sync the enrollment's summary field
                enrollment.AmountPaid = enrollment.Course.StandardFee;
                enrollment.PaymentStatus = "Paid";

                _context.PaymentRecords.Add(refundRecord);
                _context.Update(enrollment);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Refund of {amount:C} successfully recorded.";
            }

            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        // NEW: Remove Enrollment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveEnrollment(int id)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.PaymentRecords)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enrollment != null)
            {
                int studentId = enrollment.StudentId;

                // Remove associated payments first to avoid foreign key errors
                if (enrollment.PaymentRecords.Any())
                {
                    _context.PaymentRecords.RemoveRange(enrollment.PaymentRecords);
                }

                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Course enrollment and history removed.";
                return RedirectToAction(nameof(Details), new { id = studentId });
            }

            return NotFound();
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }
            return View(student);
        }

        // POST: Students/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,IdNumber,PhoneNumber")] Student student)
        {
            if (id != student.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id))
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
            return View(student);
        }


        // GET: Students/ChangeCourse/5
        public async Task<IActionResult> ChangeCourse(int? id)
        {
            if (id == null) return NotFound();

            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .Include(e => e.PaymentRecords)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enrollment == null) return NotFound();

            decimal totalPaid = enrollment.PaymentRecords.Sum(p => p.AmountPaid);

            // Get all courses except the current one
            var availableCourses = await _context.Courses
                .Where(c => c.Id != enrollment.CourseId)
                .OrderBy(c => c.StandardFee)
                .ToListAsync();

            // Pass the list and the total paid to the view
            ViewBag.TotalPaid = totalPaid;
            ViewBag.AvailableCourses = availableCourses;

            return View(enrollment);
        }

        // POST: Students/ChangeCourse
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeCourse(int enrollmentId, int newCourseId)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.PaymentRecords)
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            var newCourse = await _context.Courses.FindAsync(newCourseId);

            if (enrollment != null && newCourse != null)
            {
                decimal totalPaid = enrollment.PaymentRecords.Sum(p => p.AmountPaid);
                decimal newFee = newCourse.StandardFee;
                decimal difference = totalPaid - newFee;

                enrollment.CourseId = newCourseId;

                if (totalPaid >= newFee)
                {
                    enrollment.PaymentStatus = "Paid";
                    enrollment.AmountPaid = newFee;

                    if (difference > 0)
                        TempData["SuccessMessage"] = $"Course swapped! Student has a credit of {difference:C}.";
                    else
                        TempData["SuccessMessage"] = "Course swapped! Enrollment is fully paid.";
                }
                else
                {
                    enrollment.PaymentStatus = "Partial";
                    enrollment.AmountPaid = totalPaid;
                    decimal owing = newFee - totalPaid;
                    TempData["WarningMessage"] = $"Course swapped! Student now owes {owing:C}.";
                }

                _context.Update(enrollment);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Details), new { id = enrollment.StudentId });
            }

            return BadRequest();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnrollWithCredit(int studentId, int sourceEnrollmentId, int newCourseId)
        {
            var source = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.PaymentRecords)
                .FirstOrDefaultAsync(e => e.Id == sourceEnrollmentId);

            var newCourse = await _context.Courses.FindAsync(newCourseId);

            if (source != null && newCourse != null)
            {
                decimal totalPaid = source.PaymentRecords.Sum(p => p.AmountPaid);
                decimal creditAvailable = totalPaid - source.Course.StandardFee;

                if (creditAvailable > 0)
                {
                    string receiptToUse;
                    string paymentStatus;
                    decimal amountToApply;

                    // Logic: Is the credit enough to cover the new course?
                    if (creditAvailable >= newCourse.StandardFee)
                    {
                        // Scenario: Course is fully covered or cheaper
                        receiptToUse = $"{source.ReceiptNo} (Credit Transfer)";
                        paymentStatus = "Paid";
                        amountToApply = newCourse.StandardFee;
                    }
                    else
                    {
                        // Scenario: Course costs MORE than the credit
                        // Generate a new receipt number for the partial payment/new enrollment
                        var lastReceipt = await _context.Enrollments.MaxAsync(e => e.ReceiptNo);
                        int.TryParse(lastReceipt?.Replace("#", ""), out int lastNum);
                        receiptToUse = $"#{lastNum + 1}";

                        paymentStatus = "Partial";
                        amountToApply = creditAvailable;
                    }

                    // 1. Create the new Enrollment
                    var newEnrollment = new Enrollment
                    {
                        StudentId = studentId,
                        CourseId = newCourseId,
                        Date = DateTime.Now,
                        AmountPaid = amountToApply,
                        PaymentStatus = paymentStatus,
                        Month = DateTime.Now.ToString("MMMM").ToUpper(),
                        MethodOfPayment = "Credit Transfer",
                        ReceiptNo = receiptToUse
                    };

                    _context.Enrollments.Add(newEnrollment);
                    await _context.SaveChangesAsync();

                    // 2. Create the transfer records for history
                    _context.PaymentRecords.Add(new PaymentRecord
                    {
                        EnrollmentId = source.Id,
                        AmountPaid = -amountToApply,
                        ReceiptNo = receiptToUse,
                        PaymentMethod = "Credit Transfer",
                        DatePaid = DateTime.Now
                    });

                    _context.PaymentRecords.Add(new PaymentRecord
                    {
                        EnrollmentId = newEnrollment.Id,
                        AmountPaid = amountToApply,
                        ReceiptNo = receiptToUse,
                        PaymentMethod = "Credit Transfer",
                        DatePaid = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Credit applied to {newCourse.Title}. Status: {paymentStatus}";
                }
            }
            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(m => m.Id == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }
    }
}
