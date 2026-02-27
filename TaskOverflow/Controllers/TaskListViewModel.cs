using TaskOverflow.Models;

namespace TaskOverflow.ViewModels
{
    public class TaskListViewModel
    {
        public List<TaskItem> Tasks { get; set; } = new();

        // Pagination properties
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalTasks { get; set; }

        // Filter properties
        public string SearchString { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = "All";
        public string SortOrder { get; set; } = "asc";

        // Page size options
        public List<int> PageSizeOptions => new() { 5, 10, 15, 20 };
    }

    public class TaskOrder
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }

    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int TotalRows { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}