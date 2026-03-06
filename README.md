# TaskOverflow

A task management web application built with **ASP.NET Core MVC (.NET 8)**. Organise, track, and visualise your tasks with a dashboard, calendar view, timeline, and Excel import/export support.

> Built as a submission for the C# / .NET Technical Assignment.

---

## Features

| Requirement | Status |
|---|---|
| Task CRUD (create, edit, delete, complete) | ✅ |
| Categories — Work, Personal, Urgent | ✅ |
| Pagination (5 / 10 / 15 / 20 per page) | ✅ |
| Search by description | ✅ |
| Filter by status (All / Completed / Incomplete) | ✅ |
| Sort by due date (ascending / descending) | ✅ |
| Local persistence (SQLite via EF Core) | ✅ |
| Bulk import from Excel (.xlsx) | ✅ |
| Export to Excel (.xlsx) | ✅ |
| **Bonus:** Calendar view (FullCalendar) | ✅ |
| **Bonus:** Drag-and-drop reordering (persisted via SortOrder field) | ✅ |

### Sorting — tasks with no due date
Tasks with no due date are sorted to the **end** when sorting ascending, and to the **beginning** when sorting descending, so dated tasks always appear first in the most common use case.

---

## Tech Stack

- **ASP.NET Core MVC** — web framework
- **Entity Framework Core + SQLite** — local persistence (zero config, file-based)
- **ASP.NET Core Identity** — user authentication
- **EPPlus** — Excel import/export
- **FullCalendar 6** — calendar view on the dashboard
- **Bootstrap 5** — UI framework
- **SortableJS** — drag-and-drop reordering

---

## Prerequisites

| Tool | Version | Download |
|---|---|---|
| .NET 8 SDK | 8.0 or later | https://dotnet.microsoft.com/download |
| Git | Any recent version | https://git-scm.com |

> No database server required — the app uses **SQLite**, which stores data in a local file (`TaskOverflow.db`) created automatically on first run.

---

## Running the App

### Option A — Visual Studio (recommended)

1. Clone or download the repository
2. Open `TaskOverflow.sln` in **Visual Studio 2022**
3. Open the **Package Manager Console** (Tools → NuGet Package Manager → Package Manager Console) and run:

```
Update-Database
```

4. Press **F5** (or click the green ▶ Run button) — the browser will open automatically

---

### Option B — Command Line

> **Note:** You'll need the EF Core CLI tools installed once on your machine. If you've never used them before, run this first:
> ```bash
> dotnet tool install --global dotnet-ef
> ```

```bash
# 1. Clone the repo
git clone https://github.com/your-username/TaskOverflow.git

# 2. Navigate into the project folder (note: two levels deep)
cd TaskOverflow\TaskOverflow

# 3. Apply migrations (creates TaskOverflow.db automatically)
dotnet ef database update

# 4. Run
dotnet run
```

The browser will open automatically. If it doesn't, navigate to `https://localhost:7135` manually.

> The repo has a solution folder (`TaskOverflow/`) wrapping the project folder (`TaskOverflow/TaskOverflow/`). Make sure you `cd` into the inner folder before running any `dotnet` commands.

---

## First Time Use

1. Click **Register** to create an account
2. Log in — you'll land on your task list
3. Use **+ New Task** to add your first task
4. Visit the **Dashboard** to see your calendar and completion stats
5. Use **Import** to bulk-add tasks from Excel (download the template first)

---

## Excel Import Format

Download the import template from within the app (**Tasks → Import → Download Template**).

| Column | Required | Notes |
|---|---|---|
| Description | ✅ Yes | Task title / description |
| Category | ✅ Yes | `Work`, `Personal`, or `Urgent` |
| Due Date | No | Format: `YYYY-MM-DD` |
| Start Date | No | Format: `YYYY-MM-DD` |
| End Date | No | Format: `YYYY-MM-DD` |
| Status | No | `Completed` or leave blank |

Invalid rows are skipped and a summary is shown after import.

---

## Project Structure

```
TaskOverflow/
├── Controllers/
│   ├── AccountController.cs          # Login, register, profile, settings
│   ├── HomeController.cs             # Home/landing page
│   └── TasksController.cs            # Task CRUD, import/export, dashboard
├── Data/
│   └── ApplicationDbContext.cs       # EF Core database context
├── Migrations/                       # EF Core migration history
├── Models/
│   ├── AccountViewModels.cs          # Login, register, profile view models
│   ├── ErrorViewModel.cs
│   └── ViewModels.cs                 # Task-related view models
├── Views/
│   ├── Account/
│   │   ├── Login.cshtml
│   │   ├── Profile.cshtml
│   │   ├── Register.cshtml
│   │   └── Settings.cshtml           # Change password
│   ├── Home/
│   │   ├── Index.cshtml
│   │   └── Privacy.cshtml
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   └── Error.cshtml
│   └── Tasks/
│       ├── _TimelineRowInteractive.cshtml
│       ├── Create.cshtml
│       ├── Dashboard.cshtml          # Dashboard + FullCalendar
│       ├── Details.cshtml
│       ├── Edit.cshtml
│       ├── Index.cshtml              # Task list (search, filter, sort, pagination)
│       └── TimelineView.cshtml       # Gantt/timeline view
├── wwwroot/
│   ├── css/
│   │   ├── animations.css
│   │   ├── dark-mode.css
│   │   └── site.css
│   ├── js/
│   └── lib/
└── appsettings.json
```

---

## Troubleshooting

**`dotnet ef` command not found**
```bash
dotnet tool install --global dotnet-ef
```

**Database error on first run**
Make sure you've run `Update-Database` before starting the app. The `TaskOverflow.db` file will be created automatically in the project root.

**Port already in use**
Change the port in `Properties/launchSettings.json`, or run:
```bash
dotnet run --urls "https://localhost:5002"
```
