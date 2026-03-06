using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TaskOverflow.Models
{
    // ========== ENUMS ==========
    public enum CategoryType
    {
        [Display(Name = "Work")]
        Work = 1,

        [Display(Name = "Personal")]
        Personal = 2,

        [Display(Name = "Urgent")]
        Urgent = 3
    }

    // ========== ENTITY MODELS ==========
    public class TaskItem
    {
        public int Id { get; set; }

        [Display(Name = "Title")]
        public string? Title { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Task Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public CategoryType Category { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Completed")]
        public bool IsCompleted { get; set; }

        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted At")]
        public DateTime? DeletedAt { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        // For drag-and-drop reordering (bonus feature)
        public int SortOrder { get; set; }

        // Foreign key for user
        public string UserId { get; set; } = string.Empty;

        // Navigation property
        public ApplicationUser? User { get; set; }

        // Subtasks navigation property
        public ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();
    }

    public class SubTask
    {
        public int Id { get; set; }

        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public int SortOrder { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        // Soft delete properties
        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted At")]
        public DateTime? DeletedAt { get; set; }

        // For nested structure - self-reference
        public int? ParentSubTaskId { get; set; }

        [Display(Name = "Has Children")]
        public bool HasChildren { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Estimated Hours")]
        public decimal? EstimatedHours { get; set; }

        [Display(Name = "Color")]
        public string? ColorCode { get; set; }

        // Foreign key to parent task
        public int TaskItemId { get; set; }

        // Navigation properties
        public TaskItem? TaskItem { get; set; }

        // Self-reference for nested subtasks
        public SubTask? ParentSubTask { get; set; }
        public ICollection<SubTask> Children { get; set; } = new List<SubTask>();
    }

    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [PersonalData]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}".Trim();

        // Navigation property
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    // ========== VIEW MODELS ==========
    public class TaskListViewModel
    {
        public List<TaskItem> Tasks { get; set; } = new();

        // Pagination properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalTasks { get; set; }

        // Filter properties
        public string SearchString { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = "All";
        public string SortOrder { get; set; } = "asc";

        // Page size options
        public List<int> PageSizeOptions => new() { 5, 10, 15, 20 };

        // Computed properties for UI
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int StartPage => Math.Max(1, CurrentPage - 2);
        public int EndPage => Math.Min(TotalPages, CurrentPage + 2);
    }

    public class TaskCreateViewModel
    {
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500)]
        [Display(Name = "Task Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public CategoryType Category { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }
    }

    public class TaskEditViewModel : TaskCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Completed")]
        public bool IsCompleted { get; set; }
    }

    public class TaskDetailViewModel
    {
        public TaskItem Task { get; set; } = new();
        public List<SubTask> SubTasks { get; set; } = new();
        public CreateSubTaskViewModel NewSubTask { get; set; } = new();
    }

    public class CreateSubTaskViewModel
    {
        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;
    }

    public class TaskEditHierarchicalViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500)]
        [Display(Name = "Task Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public CategoryType Category { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Completed")]
        public bool IsCompleted { get; set; }

        // NEW: Task timeline properties
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Hierarchical subtasks
        public List<HierarchicalSubTaskViewModel> SubTasks { get; set; } = new();
    }

    public class HierarchicalSubTaskViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ParentSubTaskId { get; set; }
        public bool HasChildren { get; set; }
        public int Level { get; set; } // For indentation

        // NEW: Timeline properties
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Estimated Hours")]
        public decimal? EstimatedHours { get; set; }

        [Display(Name = "Color")]
        public string? ColorCode { get; set; }

        public List<HierarchicalSubTaskViewModel> Children { get; set; } = new();
    }

    public class CreateNestedSubTaskViewModel
    {
        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public int? ParentSubTaskId { get; set; }
    }

    // ========== TIMELINE MODELS ==========
    public enum TimelineZoomLevel
    {
        Hours = 0,
        HalfDays = 1,
        Days = 2,
        Weeks = 3,
        Months = 4
    }

    public class TimelineViewModel
    {
        public List<TimelineGroup> Groups { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DateTime> VisibleDates { get; set; } = new();
    }

    public class TimelineGroup
    {
        public string GroupName { get; set; } = string.Empty;
        public List<TimelineItem> Items { get; set; } = new();
    }

    public class TimelineItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public CategoryType Category { get; set; }
        public bool IsCompleted { get; set; }
        public int Progress { get; set; }
        public bool IsOverdue { get; set; }  // Add this line if missing
        public List<TimelineItem> SubItems { get; set; } = new();
    }

    public class TaskTimelineViewModel
    {
        public int TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public DateTime? TaskStartDate { get; set; }
        public DateTime? TaskEndDate { get; set; }
        public List<TimelineItemViewModel> TimelineItems { get; set; } = new();
        public DateTime TimelineStartDate { get; set; }
        public DateTime TimelineEndDate { get; set; }
        public List<DateTime> DateHeaders { get; set; } = new();
        public TimelineZoomLevel ZoomLevel { get; set; } = TimelineZoomLevel.Days;
        public int CellWidth { get; set; } = 40; // Pixels per time unit
    }

    public class TimelineItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? EstimatedHours { get; set; }
        public bool IsCompleted { get; set; }
        public int Level { get; set; }
        public string? ColorCode { get; set; }
        public int? ParentSubTaskId { get; set; }
        public List<TimelineItemViewModel> Children { get; set; } = new();
    }

    // ========== IMPORT/EXPORT MODELS ==========
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int TotalRows { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<TaskItem> ImportedTasks { get; set; } = new();

        public bool HasErrors => Errors != null && Errors.Count > 0;
        public string Summary => $"Successfully imported {SuccessCount} of {TotalRows} tasks";
    }

    public class ExportTaskModel
    {
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    // ========== API/REQUEST MODELS ==========
    public class TaskOrder
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }

    public class ToggleStatusRequest
    {
        public int Id { get; set; }
        public bool IsCompleted { get; set; }
    }

    // ========== DASHBOARD MODELS ==========
    public class DashboardViewModel
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public Dictionary<CategoryType, int> TasksByCategory { get; set; } = new();

        public List<UpcomingTask> UpcomingTasks { get; set; } = new();

        public List<UnscheduledTask> UnscheduledTasks { get; set; } = new();
    }

    public class UpcomingTask
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public CategoryType Category { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? StartDate { get; set; }  // ← add this
        public DateTime? EndDate { get; set; }  // ← add this
        public int DaysRemaining => (DueDate - DateTime.Today).Days;
    }

    public class UnscheduledTask
    {
        public int Id { get; set; }
        public string? Description { get; set; }
        public CategoryType Category { get; set; }
        public bool IsCompleted { get; set; }
    }

}