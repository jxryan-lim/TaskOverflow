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

            // Collect all dates from task and subtasks
            var allDates = new List<DateTime>();

            if (task.StartDate.HasValue) allDates.Add(task.StartDate.Value);
            if (task.EndDate.HasValue) allDates.Add(task.EndDate.Value);

            foreach (var subtask in task.SubTasks)
            {
                if (subtask.StartDate.HasValue) allDates.Add(subtask.StartDate.Value);
                if (subtask.EndDate.HasValue) allDates.Add(subtask.EndDate.Value);
            }

            DateTime timelineStart;
            DateTime timelineEnd;

            if (allDates.Any())
            {
                // Add padding around the actual data
                timelineStart = allDates.Min().AddDays(-2);
                timelineEnd = allDates.Max().AddDays(2);
            }
            else
            {
                // Default to current week if no dates
                timelineStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                timelineEnd = timelineStart.AddDays(7);
            }

            // Ensure minimum range based on zoom level
            var minDays = zoom switch
            {
                TimelineZoomLevel.Hours => 1,
                TimelineZoomLevel.HalfDays => 3,
                TimelineZoomLevel.Days => 7,
                TimelineZoomLevel.Weeks => 28,
                TimelineZoomLevel.Months => 60,
                _ => 7
            };

            if ((timelineEnd - timelineStart).TotalDays < minDays)
            {
                timelineEnd = timelineStart.AddDays(minDays);
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

            if (request.Type == "task")
            {
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == user.Id);

                if (task == null) return NotFound();

                if (request.IsStart)
                {
                    task.StartDate = request.Date;
                }
                else
                {
                    task.EndDate = request.Date;
                }

                task.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var subtask = await _context.SubTasks
                    .Include(st => st.TaskItem)
                    .FirstOrDefaultAsync(st => st.Id == request.Id && st.TaskItem.UserId == user.Id);

                if (subtask == null) return NotFound();

                if (request.IsStart)
                {
                    subtask.StartDate = request.Date;
                }
                else
                {
                    subtask.EndDate = request.Date;
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

        private List<DateTime> GenerateDateHeaders(DateTime start, DateTime end, TimelineZoomLevel zoom)
        {
            var headers = new List<DateTime>();
            var current = start;

            switch (zoom)
            {
                case TimelineZoomLevel.Hours:
                    while (current <= end)
                    {
                        headers.Add(current);
                        current = current.AddHours(1);
                    }
                    break;
                case TimelineZoomLevel.HalfDays:
                    while (current <= end)
                    {
                        headers.Add(current);
                        current = current.AddHours(12);
                    }
                    break;
                case TimelineZoomLevel.Days:
                    while (current <= end)
                    {
                        headers.Add(current);
                        current = current.AddDays(1);
                    }
                    break;
                case TimelineZoomLevel.Weeks:
                    while (current <= end)
                    {
                        headers.Add(current);
                        current = current.AddDays(7);
                    }
                    break;
                case TimelineZoomLevel.Months:
                    while (current <= end)
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
                Children = new List<TimelineItemViewModel>()
            };

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
            public string Type { get; set; } = string.Empty; // "task" or "subtask"
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
    }
}