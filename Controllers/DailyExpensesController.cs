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
    [Authorize(Roles = "Admin,Staff")]
    public class DailyExpensesController : Controller
    {
        private readonly BravoProjectsDbContext _context;

        public DailyExpensesController(BravoProjectsDbContext context)
        {
            _context = context;
        }

        // GET: DailyExpenses
        public async Task<IActionResult> Index()
        {
            return View(await _context.DailyExpenses.ToListAsync());
        }

        // GET: DailyExpenses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dailyExpense = await _context.DailyExpenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (dailyExpense == null)
            {
                return NotFound();
            }

            return View(dailyExpense);
        }

        // GET: DailyExpenses/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DailyExpenses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Date,Description,Amount,Category,Reference")] DailyExpense dailyExpense)
        {
            if (ModelState.IsValid)
            {
                _context.Add(dailyExpense);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(dailyExpense);
        }

        // GET: DailyExpenses/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dailyExpense = await _context.DailyExpenses.FindAsync(id);
            if (dailyExpense == null)
            {
                return NotFound();
            }
            return View(dailyExpense);
        }

        // POST: DailyExpenses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Date,Description,Amount,Category,Reference")] DailyExpense dailyExpense)
        {
            if (id != dailyExpense.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(dailyExpense);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DailyExpenseExists(dailyExpense.Id))
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
            return View(dailyExpense);
        }

        // GET: DailyExpenses/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dailyExpense = await _context.DailyExpenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (dailyExpense == null)
            {
                return NotFound();
            }

            return View(dailyExpense);
        }

        // POST: DailyExpenses/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dailyExpense = await _context.DailyExpenses.FindAsync(id);
            if (dailyExpense != null)
            {
                _context.DailyExpenses.Remove(dailyExpense);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DailyExpenseExists(int id)
        {
            return _context.DailyExpenses.Any(e => e.Id == id);
        }
    }
}
