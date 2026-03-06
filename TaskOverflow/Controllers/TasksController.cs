using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskOverflow.Data;
using TaskOverflow.Models;
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
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task == null) return NotFound();

            // Load all subtasks (nested)
            var allSubtasks = await _context.SubTasks
                .Where(st => st.TaskItemId == task.Id && !st.IsDeleted)
                .OrderBy(st => st.ParentSubTaskId)
                .ThenBy(st => st.SortOrder)
                .ToListAsync();

            // Build hierarchy (same logic as your old EditHierarchical)
            var viewModelDict = new Dictionary<int, HierarchicalSubTaskViewModel>();
            foreach (var subtask in allSubtasks)
            {
                viewModelDict[subtask.Id] = new HierarchicalSubTaskViewModel
                {
                    Id = subtask.Id,
                    Title = subtask.Title,
                    IsCompleted = subtask.IsCompleted,
                    SortOrder = subtask.SortOrder,
                    CreatedAt = subtask.CreatedAt,
                    ParentSubTaskId = subtask.ParentSubTaskId,
                    HasChildren = allSubtasks.Any(st => st.ParentSubTaskId == subtask.Id),
                    StartDate = subtask.StartDate,
                    EndDate = subtask.EndDate,
                    EstimatedHours = subtask.EstimatedHours,
                    ColorCode = subtask.ColorCode,
                    Children = new List<HierarchicalSubTaskViewModel>()
                };
            }

            var rootSubtasks = new List<HierarchicalSubTaskViewModel>();
            foreach (var subtask in allSubtasks)
            {
                var vm = viewModelDict[subtask.Id];
                if (subtask.ParentSubTaskId.HasValue && viewModelDict.ContainsKey(subtask.ParentSubTaskId.Value))
                    viewModelDict[subtask.ParentSubTaskId.Value].Children.Add(vm);
                else
                    rootSubtasks.Add(vm);
            }

            foreach (var vm in viewModelDict.Values)
                vm.Children = vm.Children.OrderBy(c => c.SortOrder).ToList();

            var viewModel = new TaskEditHierarchicalViewModel
            {
                Id = task.Id,
                Description = task.Description,
                Category = task.Category,
                DueDate = task.DueDate,
                IsCompleted = task.IsCompleted,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                SubTasks = rootSubtasks.OrderBy(st => st.SortOrder).ToList()
            };

            return View(viewModel);
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaskEditHierarchicalViewModel model)
        {
            if (id != model.Id) return NotFound();

            // Only validate the fields we actually edit
            ModelState.Remove("SubTasks"); // ignore subtask validation

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

                if (task == null) return NotFound();

                task.Description = model.Description;
                task.DueDate = model.DueDate;
                task.Category = model.Category;
                task.IsCompleted = model.IsCompleted;
                task.StartDate = model.StartDate;   // timeline fields
                task.EndDate = model.EndDate;
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Task updated!";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // If validation fails, reload the view properly
            return RedirectToAction(nameof(Edit), new { id });
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
                .Include(t => t.SubTasks)  // Include subtasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task != null)
            {
                // Soft delete the task
                task.IsDeleted = true;
                task.DeletedAt = DateTime.UtcNow;

                // Soft delete all subtasks too
                foreach (var subtask in task.SubTasks)
                {
                    subtask.IsDeleted = true;
                    subtask.DeletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task and its subtasks moved to trash!";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Trash()
        {
            var user = await _userManager.GetUserAsync(User);

            // First get deleted tasks
            var deletedTasks = await _context.Tasks
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id && t.IsDeleted)
                .OrderByDescending(t => t.DeletedAt)
                .ToListAsync();

            // Then manually load subtasks for each task
            foreach (var task in deletedTasks)
            {
                await _context.Entry(task)
                    .Collection(t => t.SubTasks)
                    .Query()
                    .IgnoreQueryFilters()
                    .LoadAsync();
            }

            return View(deletedTasks);
        }

        // POST: Tasks/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id && t.IsDeleted);

            if (task != null)
            {
                task.IsDeleted = false;
                task.DeletedAt = null;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task restored successfully!";
            }

            return RedirectToAction(nameof(Trash));
        }

        // POST: Tasks/PermanentDelete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id && t.IsDeleted);

            if (task != null)
            {
                _context.Tasks.Remove(task); // Actually delete from database
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task permanently deleted!";
            }

            return RedirectToAction(nameof(Trash));
        }

        // GET: Tasks/Details/5 - Show task with subtasks
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .Include(t => t.SubTasks.OrderBy(st => st.SortOrder))
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task == null)
            {
                return NotFound();
            }

            var viewModel = new TaskDetailViewModel
            {
                Task = task,
                SubTasks = task.SubTasks.ToList(),
                NewSubTask = new CreateSubTaskViewModel()
            };

            return View(viewModel);
        }

        // POST: Tasks/AddSubTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSubTask(int taskId, CreateSubTaskViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == user.Id);

                if (task == null)
                {
                    return NotFound();
                }

                var maxSortOrder = task.SubTasks.Max(st => (int?)st.SortOrder) ?? 0;

                var subTask = new SubTask
                {
                    Title = model.Title,
                    TaskItemId = taskId,
                    CreatedAt = DateTime.UtcNow,
                    SortOrder = maxSortOrder + 1
                };

                _context.SubTasks.Add(subTask);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Subtask added successfully!";
                return RedirectToAction(nameof(Details), new { id = taskId });
            }

            // If validation fails, reload the task details page
            return RedirectToAction(nameof(Details), new { id = taskId });
        }

        // POST: Tasks/ToggleSubTask/5
        [HttpPost]
        public async Task<IActionResult> ToggleSubTask(int id)
        {
            var subTask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (subTask == null)
            {
                return NotFound();
            }

            // Verify ownership
            var user = await _userManager.GetUserAsync(User);
            if (subTask.TaskItem?.UserId != user.Id)
            {
                return Unauthorized();
            }

            subTask.IsCompleted = !subTask.IsCompleted;
            await _context.SaveChangesAsync();

            // Check if all subtasks are completed, optionally auto-complete parent
            var allCompleted = await _context.SubTasks
                .Where(st => st.TaskItemId == subTask.TaskItemId)
                .AllAsync(st => st.IsCompleted);

            if (allCompleted)
            {
                var parentTask = await _context.Tasks.FindAsync(subTask.TaskItemId);
                if (parentTask != null && !parentTask.IsCompleted)
                {
                    parentTask.IsCompleted = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }

        // POST: Tasks/DeleteSubTask/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubTask(int id)
        {
            var subTask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (subTask == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (subTask.TaskItem?.UserId != user.Id)
            {
                return Unauthorized();
            }

            _context.SubTasks.Remove(subTask);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Subtask deleted successfully!";
            return RedirectToAction(nameof(Details), new { id = subTask.TaskItemId });
        }

        // POST: Tasks/Import
        // Expected columns (row 4 = header, data from row 5 — matches the template sheet):
        //   Col 1: Description *   Col 2: Category *   Col 3: Due Date
        //   Col 4: Start Date      Col 5: End Date      Col 6: Status
        // Also accepts the old 5-column layout (Description, Due Date, Category, Status, Created At).
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

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Only .xlsx or .xls files are supported.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                ExcelPackage.License.SetNonCommercialPersonal("TaskOverflow");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var package = new ExcelPackage(stream);

                // Prefer "Import Template" sheet; fall back to first sheet
                var worksheet = package.Workbook.Worksheets["Import Template"]
                             ?? package.Workbook.Worksheets[0];

                if (worksheet?.Dimension == null)
                {
                    TempData["ErrorMessage"] = "The uploaded file appears to be empty.";
                    return RedirectToAction(nameof(Index));
                }

                var maxSortOrder = await _context.Tasks
                    .Where(t => t.UserId == user.Id)
                    .MaxAsync(t => (int?)t.SortOrder) ?? 0;

                // Auto-detect header row and column layout
                int headerRow = DetectHeaderRow(worksheet);
                bool isNewLayout = DetectNewLayout(worksheet, headerRow);
                int dataStartRow = headerRow + 1;
                int totalRows = worksheet.Dimension.Rows;

                for (int row = dataStartRow; row <= totalRows; row++)
                {
                    try
                    {
                        string? description;
                        string? categoryStr;
                        string? dueDateStr;
                        string? startDateStr = null;
                        string? endDateStr = null;
                        string? statusStr;

                        if (isNewLayout)
                        {
                            // New template: Description | Category | Due Date | Start Date | End Date | Status
                            description = worksheet.Cells[row, 1].Value?.ToString();
                            categoryStr = worksheet.Cells[row, 2].Value?.ToString() ?? "Personal";
                            dueDateStr = worksheet.Cells[row, 3].Value?.ToString();
                            startDateStr = worksheet.Cells[row, 4].Value?.ToString();
                            endDateStr = worksheet.Cells[row, 5].Value?.ToString();
                            statusStr = worksheet.Cells[row, 6].Value?.ToString() ?? "Incomplete";
                        }
                        else
                        {
                            // Legacy layout: Description | Due Date | Category | Status | Created At
                            description = worksheet.Cells[row, 1].Value?.ToString();
                            dueDateStr = worksheet.Cells[row, 2].Value?.ToString();
                            categoryStr = worksheet.Cells[row, 3].Value?.ToString() ?? "Personal";
                            statusStr = worksheet.Cells[row, 4].Value?.ToString() ?? "Incomplete";
                        }

                        if (string.IsNullOrWhiteSpace(description))
                        {
                            // Skip genuinely blank rows silently; flag rows with partial data
                            bool rowHasData = Enumerable.Range(1, 6).Any(c => !string.IsNullOrWhiteSpace(worksheet.Cells[row, c].Value?.ToString()));
                            if (rowHasData) results.Errors.Add($"Row {row}: Description is required.");
                            results.TotalRows++;
                            continue;
                        }

                        if (description.Length > 500)
                        {
                            results.Errors.Add($"Row {row}: Description exceeds 500 characters — truncated.");
                            description = description[..500];
                        }

                        if (!Enum.TryParse<CategoryType>(categoryStr, ignoreCase: true, out var category))
                        {
                            results.Errors.Add($"Row {row}: Unknown category '{categoryStr}' — defaulted to Personal.");
                            category = CategoryType.Personal;
                        }

                        DateTime? ParseDate(string? raw)
                        {
                            if (string.IsNullOrWhiteSpace(raw)) return null;
                            return DateTime.TryParse(raw, out var d) ? d : null;
                        }

                        var dueDate = ParseDate(dueDateStr);
                        var startDate = ParseDate(startDateStr);
                        var endDate = ParseDate(endDateStr);
                        var isCompleted = (statusStr ?? "").Contains("Completed", StringComparison.OrdinalIgnoreCase)
                                       && !statusStr!.Contains("Incomplete", StringComparison.OrdinalIgnoreCase);

                        _context.Tasks.Add(new TaskItem
                        {
                            Description = description,
                            Category = category,
                            DueDate = dueDate,
                            StartDate = startDate,
                            EndDate = endDate,
                            IsCompleted = isCompleted,
                            UserId = user.Id,
                            CreatedAt = DateTime.UtcNow,
                            SortOrder = maxSortOrder + results.SuccessCount + 1
                        });

                        results.SuccessCount++;
                        results.TotalRows++;
                    }
                    catch (Exception ex)
                    {
                        results.Errors.Add($"Row {row}: {ex.Message}");
                        results.TotalRows++;
                    }
                }

                await _context.SaveChangesAsync();
                TempData["ImportResults"] = System.Text.Json.JsonSerializer.Serialize(results);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error reading file: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Detect which row contains the column headers (looks for "Description" in col 1, rows 1-6)
        private static int DetectHeaderRow(OfficeOpenXml.ExcelWorksheet ws)
        {
            for (int r = 1; r <= Math.Min(6, ws.Dimension.Rows); r++)
            {
                var val = ws.Cells[r, 1].Value?.ToString() ?? "";
                if (val.StartsWith("Description", StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return 1; // default: assume row 1
        }

        // Returns true if the header row matches the new 6-column template layout
        private static bool DetectNewLayout(OfficeOpenXml.ExcelWorksheet ws, int headerRow)
        {
            var col2 = ws.Cells[headerRow, 2].Value?.ToString() ?? "";
            return col2.StartsWith("Category", StringComparison.OrdinalIgnoreCase);
        }

        // GET: Tasks/Export
        public async Task<IActionResult> Export(string statusFilter = "All", string searchString = "")
        {
            var user = await _userManager.GetUserAsync(User);

            var query = _context.Tasks
                .Where(t => t.UserId == user.Id && !t.IsDeleted);

            if (!string.IsNullOrEmpty(searchString))
                query = query.Where(t => t.Description.Contains(searchString));

            switch (statusFilter)
            {
                case "Completed": query = query.Where(t => t.IsCompleted); break;
                case "Incomplete": query = query.Where(t => !t.IsCompleted); break;
            }

            var tasks = await query.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToListAsync();

            ExcelPackage.License.SetNonCommercialPersonal("TaskOverflow");

            using var package = new ExcelPackage();

            // ── Sheet 1: Tasks ──────────────────────────────────────────────
            var ws = package.Workbook.Worksheets.Add("Tasks");

            // Title row
            ws.Cells[1, 1].Value = "TaskOverflow – Task Export";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(0x1a, 0x73, 0xe8));
            ws.Cells[1, 1, 1, 8].Merge = true;

            ws.Cells[2, 1].Value = $"Exported: {DateTime.Now:dd MMM yyyy HH:mm}   |   Filter: {statusFilter}   |   Total: {tasks.Count}";
            ws.Cells[2, 1].Style.Font.Italic = true;
            ws.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            ws.Cells[2, 1, 2, 8].Merge = true;

            // Header row (row 4)
            string[] headers = { "#", "Description", "Category", "Status", "Due Date", "Start Date", "End Date", "Created At" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cells[4, c + 1];
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x1a, 0x73, 0xe8));
                cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
            }

            // Data rows
            for (int i = 0; i < tasks.Count; i++)
            {
                int row = i + 5;
                var t = tasks[i];
                bool isEven = i % 2 == 0;

                var rowRange = ws.Cells[row, 1, row, 8];
                if (isEven)
                {
                    rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0xf0, 0xf4, 0xff));
                }

                ws.Cells[row, 1].Value = i + 1;
                ws.Cells[row, 2].Value = t.Description;

                var catCell = ws.Cells[row, 3];
                catCell.Value = t.Category.ToString();
                catCell.Style.Font.Color.SetColor(t.Category switch
                {
                    CategoryType.Urgent => System.Drawing.Color.FromArgb(0xdc, 0x35, 0x45),
                    CategoryType.Work => System.Drawing.Color.FromArgb(0x0d, 0x6e, 0xfd),
                    CategoryType.Personal => System.Drawing.Color.FromArgb(0x0d, 0xcb, 0xf5),
                    _ => System.Drawing.Color.Black
                });
                catCell.Style.Font.Bold = true;

                var statusCell = ws.Cells[row, 4];
                statusCell.Value = t.IsCompleted ? "✓ Completed" : "○ Incomplete";
                statusCell.Style.Font.Color.SetColor(t.IsCompleted
                    ? System.Drawing.Color.FromArgb(0x19, 0x87, 0x54)
                    : System.Drawing.Color.FromArgb(0xfd, 0x7e, 0x14));

                if (t.DueDate.HasValue)
                {
                    ws.Cells[row, 5].Value = t.DueDate.Value.ToString("yyyy-MM-dd");
                    if (t.DueDate.Value.Date < DateTime.Today && !t.IsCompleted)
                        ws.Cells[row, 5].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                }

                if (t.StartDate.HasValue) ws.Cells[row, 6].Value = t.StartDate.Value.ToString("yyyy-MM-dd");
                if (t.EndDate.HasValue) ws.Cells[row, 7].Value = t.EndDate.Value.ToString("yyyy-MM-dd");
                ws.Cells[row, 8].Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");

                // Row bottom border
                ws.Cells[row, 1, row, 8].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Hair;
                ws.Cells[row, 1, row, 8].Style.Border.Bottom.Color.SetColor(System.Drawing.Color.LightGray);
            }

            // Totals summary row
            int summaryRow = tasks.Count + 5;
            int completed = tasks.Count(t => t.IsCompleted);
            int overdue = tasks.Count(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value.Date < DateTime.Today);

            ws.Cells[summaryRow, 1].Value = "Summary";
            ws.Cells[summaryRow, 1].Style.Font.Bold = true;
            ws.Cells[summaryRow, 2].Value = $"Total: {tasks.Count}  |  Completed: {completed}  |  Pending: {tasks.Count - completed}  |  Overdue: {overdue}";
            ws.Cells[summaryRow, 1, summaryRow, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[summaryRow, 1, summaryRow, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0xe8, 0xf0, 0xfe));
            ws.Cells[summaryRow, 1, summaryRow, 8].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

            // Column widths
            ws.Column(1).Width = 5;
            ws.Column(2).Width = 45;
            ws.Column(3).Width = 13;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 13;
            ws.Column(6).Width = 13;
            ws.Column(7).Width = 13;
            ws.Column(8).Width = 18;

            // Freeze header
            ws.View.FreezePanes(5, 1);

            // ── Sheet 2: Import Template ────────────────────────────────────
            AddImportTemplateSheet(package);

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"TaskOverflow_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: Tasks/DownloadTemplate
        public IActionResult DownloadTemplate()
        {
            ExcelPackage.License.SetNonCommercialPersonal("TaskOverflow");
            using var package = new ExcelPackage();
            AddImportTemplateSheet(package);

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TaskOverflow_Import_Template.xlsx");
        }

        private void AddImportTemplateSheet(ExcelPackage package)
        {
            var ws = package.Workbook.Worksheets.Add("Import Template");

            // Title
            ws.Cells[1, 1].Value = "TaskOverflow – Import Template";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 13;
            ws.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(0x1a, 0x73, 0xe8));
            ws.Cells[1, 1, 1, 6].Merge = true;

            ws.Cells[2, 1].Value = "Fill in rows below the header. Required: Description, Category. Optional: Due Date, Start Date, End Date, Status.";
            ws.Cells[2, 1].Style.Font.Italic = true;
            ws.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            ws.Cells[2, 1, 2, 6].Merge = true;

            // Headers row 4
            var templateHeaders = new[] { "Description *", "Category *", "Due Date", "Start Date", "End Date", "Status" };
            var headerNotes = new[] { "Required. Max 500 chars.", "Work | Personal | Urgent", "yyyy-MM-dd (optional)", "yyyy-MM-dd (optional)", "yyyy-MM-dd (optional)", "Completed or Incomplete (default: Incomplete)" };

            for (int c = 0; c < templateHeaders.Length; c++)
            {
                var cell = ws.Cells[4, c + 1];
                cell.Value = templateHeaders[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x1a, 0x73, 0xe8));
                cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                cell.AddComment(headerNotes[c], "TaskOverflow");
            }

            // 3 example rows
            object[][] examples =
            {
                new object[] { "Review Q2 financial reports",      "Work",     "2026-04-15", "2026-04-01", "2026-04-15", "Incomplete" },
                new object[] { "Book dentist appointment",          "Personal", "2026-03-20", "",           "",           "Incomplete" },
                new object[] { "Fix production login bug ASAP",    "Urgent",   "2026-03-10", "2026-03-07", "2026-03-10", "Incomplete" },
            };

            for (int r = 0; r < examples.Length; r++)
            {
                for (int c = 0; c < examples[r].Length; c++)
                {
                    ws.Cells[r + 5, c + 1].Value = examples[r][c];
                }
                // Light stripe
                if (r % 2 == 0)
                {
                    ws.Cells[r + 5, 1, r + 5, 6].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[r + 5, 1, r + 5, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0xf0, 0xf4, 0xff));
                }
            }

            ws.Column(1).Width = 42;
            ws.Column(2).Width = 13;
            ws.Column(3).Width = 13;
            ws.Column(4).Width = 13;
            ws.Column(5).Width = 13;
            ws.Column(6).Width = 22;

            ws.View.FreezePanes(5, 1);
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);

            var allTasks = await _context.Tasks
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .ToListAsync();

            var today = DateTime.Today;
            var overdue = allTasks.Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value.Date < today).ToList();
            var completed = allTasks.Where(t => t.IsCompleted).ToList();

            var vm = new DashboardViewModel
            {
                TotalTasks = allTasks.Count,
                CompletedTasks = completed.Count,
                PendingTasks = allTasks.Count(t => !t.IsCompleted),
                OverdueTasks = overdue.Count,
                TasksByCategory = allTasks
                    .GroupBy(t => t.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),

                // Tasks WITH a due date → calendar events
                UpcomingTasks = allTasks
                    .Where(t => t.DueDate.HasValue)
                    .OrderBy(t => t.DueDate)
                    .Select(t => new UpcomingTask
                    {
                        Id = t.Id,
                        Description = t.Description,
                        Category = t.Category,
                        IsCompleted = t.IsCompleted,
                        DueDate = t.DueDate!.Value,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate
                    })
                    .ToList(),

                // Tasks WITHOUT a due date → unscheduled panel
                UnscheduledTasks = allTasks
                    .Where(t => !t.DueDate.HasValue)
                    .OrderBy(t => t.Category)
                    .Select(t => new UnscheduledTask
                    {
                        Id = t.Id,
                        Description = t.Description,
                        Category = t.Category,
                        IsCompleted = t.IsCompleted
                    })
                    .ToList()
            };

            return View(vm);
        }

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

        // POST: Tasks/ToggleSubTaskStatus
        [HttpPost]
        public async Task<IActionResult> ToggleSubTaskStatus([FromBody] SubtaskStatusUpdate model)
        {
            var subTask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == model.Id);

            if (subTask == null)
            {
                return NotFound();
            }

            // Verify ownership
            var user = await _userManager.GetUserAsync(User);
            if (subTask.TaskItem?.UserId != user.Id)
            {
                return Unauthorized();
            }

            subTask.IsCompleted = model.IsCompleted;
            await _context.SaveChangesAsync();

            // Check if all subtasks are completed
            var allCompleted = await _context.SubTasks
                .Where(st => st.TaskItemId == subTask.TaskItemId && !st.IsDeleted)
                .AllAsync(st => st.IsCompleted);

            return Ok(new { allCompleted });
        }

        // POST: Tasks/ReorderSubtasks
        [HttpPost]
        public async Task<IActionResult> ReorderSubtasks([FromBody] List<SubtaskOrder> orders)
        {
            foreach (var order in orders)
            {
                var subTask = await _context.SubTasks.FindAsync(order.Id);
                if (subTask != null)
                {
                    subTask.SortOrder = order.SortOrder;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST: Tasks/UpdateSubtaskTitle
        [HttpPost]
        public async Task<IActionResult> UpdateSubtaskTitle([FromBody] UpdateSubtaskTitleRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            var subtask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == request.Id && st.TaskItem.UserId == user.Id);

            if (subtask == null)
            {
                return NotFound();
            }

            subtask.Title = request.Title;
            await _context.SaveChangesAsync();
            return Ok();
        }


        private void AddChildren(HierarchicalSubTaskViewModel parent, List<SubTask> allSubtasks, int level)
        {
            var children = allSubtasks
                .Where(st => st.ParentSubTaskId == parent.Id)
                .OrderBy(st => st.SortOrder)
                .Select(st => new HierarchicalSubTaskViewModel
                {
                    Id = st.Id,
                    Title = st.Title,
                    IsCompleted = st.IsCompleted,
                    SortOrder = st.SortOrder,
                    CreatedAt = st.CreatedAt,
                    ParentSubTaskId = st.ParentSubTaskId,
                    HasChildren = allSubtasks.Any(c => c.ParentSubTaskId == st.Id),
                    Level = level
                }).ToList();

            parent.Children = children;

            foreach (var child in children)
            {
                AddChildren(child, allSubtasks, level + 1);
            }
        }

        // POST: Tasks/AddNestedSubTask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNestedSubTask(int taskId, CreateNestedSubTaskViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == user.Id);

                if (task == null)
                {
                    return NotFound();
                }

                // Get max sort order for this parent
                var maxSortOrder = await _context.SubTasks
                    .Where(st => st.TaskItemId == taskId && st.ParentSubTaskId == model.ParentSubTaskId)
                    .MaxAsync(st => (int?)st.SortOrder) ?? 0;

                var subTask = new SubTask
                {
                    Title = model.Title,
                    TaskItemId = taskId,
                    ParentSubTaskId = model.ParentSubTaskId,
                    CreatedAt = DateTime.UtcNow,
                    SortOrder = maxSortOrder + 1
                };

                _context.SubTasks.Add(subTask);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Subtask added successfully!";
                return RedirectToAction(nameof(Edit), new { id = taskId });
            }

            return RedirectToAction(nameof(Edit), new { id = taskId });
        }

        // POST: Tasks/ToggleNestedSubTask/5
        [HttpPost]
        public async Task<IActionResult> ToggleNestedSubTask(int id)
        {
            var subTask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (subTask == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (subTask.TaskItem?.UserId != user.Id)
            {
                return Unauthorized();
            }

            subTask.IsCompleted = !subTask.IsCompleted;
            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: Tasks/DeleteNestedSubTask/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNestedSubTask(int id)
        {
            var subTask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .Include(st => st.Children)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (subTask == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (subTask.TaskItem?.UserId != user.Id)
            {
                return Unauthorized();
            }

            // Recursively soft delete all children
            await SoftDeleteSubTaskAndChildren(subTask);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Subtask and all its children deleted successfully!";
            return RedirectToAction(nameof(Edit), new { id = subTask.TaskItemId });
        }

        private async Task SoftDeleteSubTaskAndChildren(SubTask subtask)
        {
            // Load children if not already loaded
            if (!subtask.Children.Any())
            {
                await _context.Entry(subtask)
                    .Collection(st => st.Children)
                    .LoadAsync();
            }

            subtask.IsDeleted = true;
            subtask.DeletedAt = DateTime.UtcNow;

            foreach (var child in subtask.Children)
            {
                await SoftDeleteSubTaskAndChildren(child);
            }
        }

        // POST: Tasks/ReorderNestedSubtasks
        [HttpPost]
        public async Task<IActionResult> ReorderNestedSubtasks([FromBody] List<NestedSubtaskOrder> orders)
        {
            foreach (var order in orders)
            {
                var subTask = await _context.SubTasks.FindAsync(order.Id);
                if (subTask != null)
                {
                    subTask.SortOrder = order.SortOrder;
                    if (order.ParentSubTaskId != subTask.ParentSubTaskId)
                    {
                        subTask.ParentSubTaskId = order.ParentSubTaskId;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // GET: Tasks/Timeline
        public async Task<IActionResult> Timeline(int weeks = 4)
        {
            var user = await _userManager.GetUserAsync(User);

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(weeks * 7);

            var tasks = await _context.Tasks
                .Where(t => t.UserId == user.Id && !t.IsDeleted)
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                .ToListAsync();

            // Create timeline groups (by week or month)
            var groups = new List<TimelineGroup>();

            // Group by week
            for (int i = 0; i < weeks; i++)
            {
                var weekStart = startDate.AddDays(i * 7);
                var weekEnd = weekStart.AddDays(6);

                var group = new TimelineGroup
                {
                    GroupName = $"Week {i + 1}: {weekStart:MMM d} - {weekEnd:MMM d}",
                    Items = new List<TimelineItem>()
                };

                // Add tasks that fall within this week
                foreach (var task in tasks.Where(t => t.DueDate.HasValue &&
                                                      t.DueDate.Value.Date >= weekStart &&
                                                      t.DueDate.Value.Date <= weekEnd))
                {
                    var item = new TimelineItem
                    {
                        Id = task.Id,
                        Title = task.Description,
                        StartDate = task.DueDate,
                        Category = task.Category,
                        IsCompleted = task.IsCompleted,
                        Progress = CalculateProgress(task),
                        SubItems = task.SubTasks.Select(st => new TimelineItem
                        {
                            Id = st.Id,
                            Title = st.Title,
                            IsCompleted = st.IsCompleted,
                            Category = task.Category
                        }).ToList()
                    };
                    group.Items.Add(item);
                }

                // Add overdue tasks to first week
                if (i == 0)
                {
                    var overdueTasks = tasks.Where(t => t.DueDate.HasValue &&
                                                        t.DueDate.Value.Date < startDate &&
                                                        !t.IsCompleted);
                    foreach (var task in overdueTasks)
                    {
                        group.Items.Add(new TimelineItem
                        {
                            Id = task.Id,
                            Title = task.Description + " (Overdue)",
                            StartDate = task.DueDate,
                            Category = task.Category,
                            IsCompleted = task.IsCompleted,
                            Progress = CalculateProgress(task),
                            IsOverdue = true
                        });
                    }
                }

                if (group.Items.Any())
                {
                    groups.Add(group);
                }
            }

            // Add "No Due Date" group
            var noDateTasks = tasks.Where(t => !t.DueDate.HasValue);
            if (noDateTasks.Any())
            {
                groups.Add(new TimelineGroup
                {
                    GroupName = "Unscheduled",
                    Items = noDateTasks.Select(t => new TimelineItem
                    {
                        Id = t.Id,
                        Title = t.Description,
                        Category = t.Category,
                        IsCompleted = t.IsCompleted,
                        Progress = CalculateProgress(t)
                    }).ToList()
                });
            }

            var viewModel = new TimelineViewModel
            {
                Groups = groups,
                StartDate = startDate,
                EndDate = endDate,
                VisibleDates = Enumerable.Range(0, weeks * 7)
                    .Select(offset => startDate.AddDays(offset))
                    .ToList()
            };

            return View(viewModel);
        }

        // GET: Tasks/TimelineView/5
        public async Task<IActionResult> TimelineView(int id, TimelineZoomLevel zoom = TimelineZoomLevel.Days)
        {
            var user = await _userManager.GetUserAsync(User);
            var task = await _context.Tasks
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);

            if (task == null)
            {
                return NotFound();
            }

            // Use the main task's dates as the anchor for the timeline
            DateTime timelineStart;
            DateTime timelineEnd;

            if (task.StartDate.HasValue && task.EndDate.HasValue)
            {
                // If main task has both dates, use them as the anchor
                timelineStart = task.StartDate.Value;
                timelineEnd = task.EndDate.Value;

                // Add some padding (10% on each side) for better visibility
                var duration = (timelineEnd - timelineStart).TotalHours;
                var padding = duration * 0.1; // 10% padding

                timelineStart = timelineStart.AddHours(-padding);
                timelineEnd = timelineEnd.AddHours(padding);
            }
            else
            {
                // If main task doesn't have dates, default to a 7-day view centered on today
                timelineStart = DateTime.Today.AddDays(-3);
                timelineEnd = DateTime.Today.AddDays(4);
            }

            // Ensure minimum range based on zoom level
            var minHours = zoom switch
            {
                TimelineZoomLevel.Hours => 24,      // 1 day minimum for hours view
                TimelineZoomLevel.HalfDays => 48,    // 2 days for half-days
                TimelineZoomLevel.Days => 168,       // 7 days for days view
                TimelineZoomLevel.Weeks => 336,       // 2 weeks for weeks view
                TimelineZoomLevel.Months => 720,      // 30 days for months view
                _ => 168
            };

            if ((timelineEnd - timelineStart).TotalHours < minHours)
            {
                // Center the minimum range around the main task's start date if available
                if (task.StartDate.HasValue)
                {
                    timelineStart = task.StartDate.Value.AddHours(-minHours / 2);
                    timelineEnd = task.StartDate.Value.AddHours(minHours / 2);
                }
                else
                {
                    timelineEnd = timelineStart.AddHours(minHours);
                }
            }

            // Generate headers based on zoom level
            var dateHeaders = GenerateDateHeaders(timelineStart, timelineEnd, zoom);

            // Build hierarchical timeline items
            var timelineItems = new List<TimelineItemViewModel>();

            // Add the main task
            timelineItems.Add(new TimelineItemViewModel
            {
                Id = task.Id,
                Title = task.Description,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                IsCompleted = task.IsCompleted,
                Level = 0,
                ColorCode = "#4361ee",
                ParentSubTaskId = null, // Main task has no parent
                Children = new List<TimelineItemViewModel>()
            });

            // Build subtask hierarchy
            var rootSubtasks = task.SubTasks.Where(st => st.ParentSubTaskId == null).OrderBy(st => st.SortOrder);

            foreach (var rootSubtask in rootSubtasks)
            {
                timelineItems.Add(BuildTimelineItem(rootSubtask, task.SubTasks.ToList(), 1));
            }

            var viewModel = new TaskTimelineViewModel
            {
                TaskId = task.Id,
                TaskTitle = task.Description,
                TaskStartDate = task.StartDate,
                TaskEndDate = task.EndDate,
                TimelineItems = timelineItems,
                TimelineStartDate = timelineStart,
                TimelineEndDate = timelineEnd,
                DateHeaders = dateHeaders,
                ZoomLevel = zoom,
                CellWidth = GetCellWidth(zoom)
            };

            return View(viewModel);
        }

        // POST: Tasks/SetItemDate
        [HttpPost]
        public async Task<IActionResult> SetItemDate([FromBody] SetItemDateRequest request)
        {
            var user = await _userManager.GetUserAsync(User);

            Console.WriteLine($"SetItemDate called - ID: {request.Id}, Type: {request.Type}, IsStart: {request.IsStart}, Date: {request.Date}");

            // Parse the date carefully to preserve the exact time
            DateTime dateToSave;

            if (request.Date.Kind == DateTimeKind.Utc)
            {
                // If it's UTC, convert to local
                dateToSave = request.Date.ToLocalTime();
            }
            else
            {
                // If it's unspecified or local, use as is
                dateToSave = request.Date;
            }

            // Round to nearest hour to match your hourly grid
            dateToSave = new DateTime(
                dateToSave.Year,
                dateToSave.Month,
                dateToSave.Day,
                dateToSave.Hour,
                0,
                0,
                DateTimeKind.Local
            );

            if (request.Type?.ToLower() == "task")
            {
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == user.Id);

                if (task == null)
                {
                    Console.WriteLine($"Task not found with ID: {request.Id}");
                    return NotFound();
                }

                if (request.IsStart)
                {
                    task.StartDate = dateToSave;
                    Console.WriteLine($"Updated task {request.Id} StartDate to {dateToSave}");
                }
                else
                {
                    task.EndDate = dateToSave;
                    Console.WriteLine($"Updated task {request.Id} EndDate to {dateToSave}");
                }

                task.UpdatedAt = DateTime.UtcNow;
            }
            else // subtask
            {
                var subtask = await _context.SubTasks
                    .Include(st => st.TaskItem)
                    .FirstOrDefaultAsync(st => st.Id == request.Id && st.TaskItem.UserId == user.Id);

                if (subtask == null)
                {
                    Console.WriteLine($"Subtask not found with ID: {request.Id}");
                    return NotFound();
                }

                if (request.IsStart)
                {
                    subtask.StartDate = dateToSave;
                    Console.WriteLine($"Updated subtask {request.Id} StartDate to {dateToSave}");
                }
                else
                {
                    subtask.EndDate = dateToSave;
                    Console.WriteLine($"Updated subtask {request.Id} EndDate to {dateToSave}");
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST: Tasks/UpdateSubtaskDetails
        [HttpPost]
        public async Task<IActionResult> UpdateSubtaskDetails([FromBody] UpdateSubtaskDetailsRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            var subtask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == request.Id && st.TaskItem.UserId == user.Id);

            if (subtask == null) return NotFound();

            subtask.Title = request.Title;
            subtask.StartDate = request.StartDate;
            subtask.EndDate = request.EndDate;
            subtask.EstimatedHours = request.EstimatedHours;
            subtask.ColorCode = request.ColorCode;
            subtask.IsCompleted = request.IsCompleted;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // POST: Tasks/SetSubtaskColor
        [HttpPost]
        public async Task<IActionResult> SetSubtaskColor([FromBody] SetSubtaskColorRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            var subtask = await _context.SubTasks
                .Include(st => st.TaskItem)
                .FirstOrDefaultAsync(st => st.Id == request.Id && st.TaskItem.UserId == user.Id);

            if (subtask == null) return NotFound();

            subtask.ColorCode = request.Color;
            await _context.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// POST /Tasks/UpdateDueDate
        /// Called when the user drags a calendar event to a new day (or drops an
        /// unscheduled chip onto the calendar).  Optionally also sets the hour.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDueDate([FromBody] UpdateDueDateRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = _userManager.GetUserId(User);
            var task = await _context.Tasks
                                       .FirstOrDefaultAsync(t => t.Id == req.TaskId
                                                               && t.UserId == userId);
            if (task is null) return NotFound();

            if (!DateTime.TryParse(req.NewDate, out var parsedEnd))
                return BadRequest("Invalid date format. Expected yyyy-MM-dd.");

            int hour = req.Hour ?? task.DueDate?.Hour ?? 0;
            int minute = task.DueDate?.Minute ?? 0;

            // Always update DueDate (the drop target / end date)
            task.DueDate = new DateTime(parsedEnd.Year, parsedEnd.Month, parsedEnd.Day, hour, minute, 0);

            // If a new start date was provided (multi-day drag/resize), update StartDate too
            if (!string.IsNullOrEmpty(req.NewStart) && DateTime.TryParse(req.NewStart, out var parsedStart))
            {
                task.StartDate = new DateTime(parsedStart.Year, parsedStart.Month, parsedStart.Day, hour, minute, 0);
                task.EndDate = task.DueDate;  // keep EndDate in sync with DueDate
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, dueDate = task.DueDate });
        }

        /// <summary>
        /// POST /Tasks/UpdateDueHour
        /// Legacy endpoint — changes only the hour within the same day.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDueHour([FromBody] UpdateDueHourRequest req)
        {
            var userId = _userManager.GetUserId(User);
            var task = await _context.Tasks
                                       .FirstOrDefaultAsync(t => t.Id == req.TaskId
                                                               && t.UserId == userId);
            if (task is null)
                return NotFound();

            if (task.DueDate.HasValue)
            {
                task.DueDate = new DateTime(
                    task.DueDate.Value.Year,
                    task.DueDate.Value.Month,
                    task.DueDate.Value.Day,
                    req.Hour, 0, 0);
                task.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        private List<DateTime> GenerateDateHeaders(DateTime start, DateTime end, TimelineZoomLevel zoom)
        {
            var headers = new List<DateTime>();
            var current = start;

            // Cap the number of headers to prevent performance issues
            int maxHeaders = 500; // Maximum number of cells to display

            switch (zoom)
            {
                case TimelineZoomLevel.Hours:
                    while (current <= end && headers.Count < maxHeaders)
                    {
                        headers.Add(current);
                        current = current.AddHours(1);
                    }
                    break;
                case TimelineZoomLevel.HalfDays:
                    while (current <= end && headers.Count < maxHeaders)
                    {
                        headers.Add(current);
                        current = current.AddHours(12);
                    }
                    break;
                case TimelineZoomLevel.Days:
                    while (current <= end && headers.Count < maxHeaders)
                    {
                        headers.Add(current);
                        current = current.AddDays(1);
                    }
                    break;
                case TimelineZoomLevel.Weeks:
                    while (current <= end && headers.Count < maxHeaders)
                    {
                        headers.Add(current);
                        current = current.AddDays(7);
                    }
                    break;
                case TimelineZoomLevel.Months:
                    while (current <= end && headers.Count < maxHeaders)
                    {
                        headers.Add(current);
                        current = current.AddMonths(1);
                    }
                    break;
            }

            return headers;
        }

        private int GetCellWidth(TimelineZoomLevel zoom)
        {
            return zoom switch
            {
                TimelineZoomLevel.Hours => 60,      // Wider for hours to see text
                TimelineZoomLevel.HalfDays => 70,
                TimelineZoomLevel.Days => 40,
                TimelineZoomLevel.Weeks => 80,
                TimelineZoomLevel.Months => 100,
                _ => 40
            };
        }

        private TimelineItemViewModel BuildTimelineItem(SubTask subtask, List<SubTask> allSubtasks, int level)
        {
            var item = new TimelineItemViewModel
            {
                Id = subtask.Id,
                Title = subtask.Title,
                StartDate = subtask.StartDate,
                EndDate = subtask.EndDate,
                EstimatedHours = subtask.EstimatedHours,
                IsCompleted = subtask.IsCompleted,
                Level = level,
                ColorCode = subtask.ColorCode ?? GetDefaultColor(level),
                ParentSubTaskId = subtask.ParentSubTaskId,  // CRITICAL: This must be set!
                Children = new List<TimelineItemViewModel>()
            };

            // Debug log to verify
            Console.WriteLine($"Building item: Id={subtask.Id}, Title={subtask.Title}, ParentSubTaskId={subtask.ParentSubTaskId}, Level={level}");

            var children = allSubtasks
                .Where(st => st.ParentSubTaskId == subtask.Id)
                .OrderBy(st => st.SortOrder);

            foreach (var child in children)
            {
                item.Children.Add(BuildTimelineItem(child, allSubtasks, level + 1));
            }

            return item;
        }

        private string GetDefaultColor(int level)
        {
            var colors = new[] { "#4d7cff", "#20c997", "#ff6b6b", "#ffd166", "#9c88ff", "#4cd3c2" };
            return colors[level % colors.Length];
        }

        private int CalculateProgress(TaskItem task)
        {
            if (!task.SubTasks.Any()) return task.IsCompleted ? 100 : 0;
            var total = task.SubTasks.Count;
            var completed = task.SubTasks.Count(st => st.IsCompleted);
            return (int)((double)completed / total * 100);
        }

        public class NestedSubtaskOrder
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
            public int? ParentSubTaskId { get; set; }
        }

        public class SubtaskStatusUpdate
        {
            public int Id { get; set; }
            public bool IsCompleted { get; set; }
        }

        public class SubtaskOrder
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
        }

        public class SetItemDateRequest
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public bool IsStart { get; set; }
        }

        public class UpdateSubtaskDetailsRequest
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public decimal? EstimatedHours { get; set; }
            public string? ColorCode { get; set; }
            public bool IsCompleted { get; set; }
        }

        public class SetSubtaskColorRequest
        {
            public int Id { get; set; }
            public string Color { get; set; } = string.Empty;
        }

        public class UpdateSubtaskTitleRequest
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        public class UpdateDueDateRequest
        {
            public int TaskId { get; set; }
            public string NewDate { get; set; } = string.Empty; // yyyy-MM-dd  (maps to DueDate / EndDate)
            public string? NewStart { get; set; }                  // yyyy-MM-dd  (maps to StartDate) — optional
            public int? Hour { get; set; }
        }

        // Kept for backwards-compat with the old time-grid endpoint
        public class UpdateDueHourRequest
        {
            public int TaskId { get; set; }
            public int Hour { get; set; }
        }
    }
}