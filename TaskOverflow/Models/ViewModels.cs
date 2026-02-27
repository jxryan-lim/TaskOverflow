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

        // For drag-and-drop reordering (bonus feature)
        public int SortOrder { get; set; }

        // Foreign key for user
        public string UserId { get; set; } = string.Empty;

        // Navigation property
        public ApplicationUser? User { get; set; }
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
    }

    public class UpcomingTask
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public CategoryType Category { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysRemaining => (DueDate - DateTime.Today).Days;
    }
}