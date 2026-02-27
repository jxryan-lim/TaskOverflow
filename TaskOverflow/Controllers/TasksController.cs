using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskOverflow.Data;
using TaskOverflow.Models;
using TaskOverflow.ViewModels;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TaskOverflow.Controllers
{
    [Authorize] // Requires user to be logged in
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TasksController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Tasks
        public async Task<IActionResult> Index(
            string searchString = "",
            string statusFilter = "All",
            string sortOrder = "asc",
            int page = 1,
            int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // Start with user's tasks
            var query = _context.Tasks
                .Where(t => t.UserId == user.Id);

            // Search by description
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t => t.Description.Contains(searchString));
            }

            // Filter by status
            switch (statusFilter)
            {
                case "Completed":
                    query = query.Where(t => t.IsCompleted);
                    break;
                case "Incomplete":
                    query = query.Where(t => !t.IsCompleted);
                    break;
                    // "All" - no filtering
            }

            // Sort by due date
            // Tasks with no due date go to the end for ascending, beginning for descending
            if (sortOrder == "asc")
            {
                query = query.OrderBy(t => t.DueDate ?? DateTime.MaxValue)
                             .ThenBy(t => t.SortOrder);
            }
            else
            {
                query = query.OrderByDescending(t => t.DueDate ?? DateTime.MinValue)
                             .ThenBy(t => t.SortOrder);
            }

            // Get total count for pagination
            var totalItems = await query.CountAsync();

            // Apply pagination
            var tasks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Create view model
            var viewModel = new TaskListViewModel
            {
                Tasks = tasks,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                SearchString = searchString,
                StatusFilter = statusFilter,
                SortOrder = sortOrder,
                TotalTasks = totalItems
            };

            return View(viewModel);
        }

        // GET: Tasks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskItem task)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                task.UserId = user.Id;
                task.CreatedAt = DateTime.UtcNow;

                // Set default sort order (for drag-drop feature)
                var maxSortOrder = await _context.Tasks
                    .Where(t => t.UserId == user.Id)
                    .MaxAsync(t => (int?)t.SortOrder) ?? 0;
                task.SortOrder = maxSortOrder + 1;

                _context.Add(task);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(task);
        }

        // GET: Tasks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaskItem task)
        {
            if (id != task.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _userManager.GetUserAsync(User);
                    var existingTask = await _context.Tasks
                        .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

                    if (existingTask == null)
                    {
                        return NotFound();
                    }

                    // Update fields
                    existingTask.Description = task.Description;
                    existingTask.DueDate = task.DueDate;
                    existingTask.Category = task.Category;
                    existingTask.IsCompleted = task.IsCompleted;
                    existingTask.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Task updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskExists(task.Id))
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
            return View(task);
        }

        // POST: Tasks/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task == null)
            {
                return NotFound();
            }

            task.IsCompleted = !task.IsCompleted;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: Tasks/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task != null)
            {
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Tasks/Import
        [HttpPost]
        public async Task<IActionResult> Import(IFormFile file)
        {
            var user = await _userManager.GetUserAsync(User);
            var results = new ImportResult();

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a file to import.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        // Get max sort order
                        var maxSortOrder = await _context.Tasks
                            .Where(t => t.UserId == user.Id)
                            .MaxAsync(t => (int?)t.SortOrder) ?? 0;

                        for (int row = 2; row <= rowCount; row++) // Skip header row
                        {
                            try
                            {
                                var description = worksheet.Cells[row, 1].Value?.ToString();

                                if (string.IsNullOrWhiteSpace(description))
                                {
                                    results.Errors.Add($"Row {row}: Description is required");
                                    continue;
                                }

                                // Parse due date
                                DateTime? dueDate = null;
                                if (worksheet.Cells[row, 2].Value != null)
                                {
                                    if (DateTime.TryParse(worksheet.Cells[row, 2].Value.ToString(), out var parsedDate))
                                    {
                                        dueDate = parsedDate;
                                    }
                                }

                                // Parse category
                                var categoryStr = worksheet.Cells[row, 3].Value?.ToString() ?? "Personal";
                                if (!Enum.TryParse<CategoryType>(categoryStr, true, out var category))
                                {
                                    category = CategoryType.Personal;
                                }

                                // Parse status
                                var statusStr = worksheet.Cells[row, 4].Value?.ToString() ?? "Incomplete";
                                var isCompleted = statusStr.Equals("Completed", StringComparison.OrdinalIgnoreCase);

                                var task = new TaskItem
                                {
                                    Description = description,
                                    DueDate = dueDate,
                                    Category = category,
                                    IsCompleted = isCompleted,
                                    UserId = user.Id,
                                    CreatedAt = DateTime.UtcNow,
                                    SortOrder = maxSortOrder + results.SuccessCount + 1
                                };

                                _context.Tasks.Add(task);
                                results.SuccessCount++;
                            }
                            catch (Exception ex)
                            {
                                results.Errors.Add($"Row {row}: {ex.Message}");
                            }
                        }

                        await _context.SaveChangesAsync();
                        results.TotalRows = rowCount - 1;

                        TempData["ImportResults"] = System.Text.Json.JsonSerializer.Serialize(results);
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error importing file: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Tasks/Export
        public async Task<IActionResult> Export()
        {
            var user = await _userManager.GetUserAsync(User);
            var tasks = await _context.Tasks
                .Where(t => t.UserId == user.Id)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            // Set EPPlus license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Tasks");

                // Headers
                worksheet.Cells[1, 1].Value = "Description";
                worksheet.Cells[1, 2].Value = "Due Date";
                worksheet.Cells[1, 3].Value = "Category";
                worksheet.Cells[1, 4].Value = "Status";
                worksheet.Cells[1, 5].Value = "Created At";

                // Style headers
                using (var range = worksheet.Cells[1, 1, 1, 5])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }

                // Data
                for (int i = 0; i < tasks.Count; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = tasks[i].Description;
                    worksheet.Cells[i + 2, 2].Value = tasks[i].DueDate?.ToString("yyyy-MM-dd");
                    worksheet.Cells[i + 2, 3].Value = tasks[i].Category.ToString();
                    worksheet.Cells[i + 2, 4].Value = tasks[i].IsCompleted ? "Completed" : "Incomplete";
                    worksheet.Cells[i + 2, 5].Value = tasks[i].CreatedAt.ToString("yyyy-MM-dd HH:mm");
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Tasks_Export_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Bonus: Drag-and-drop reordering
        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] List<TaskOrder> orders)
        {
            var user = await _userManager.GetUserAsync(User);

            foreach (var order in orders)
            {
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == order.Id && t.UserId == user.Id);

                if (task != null)
                {
                    task.SortOrder = order.SortOrder;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }
    }
}