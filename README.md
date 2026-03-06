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
| Local persistence (SQL Server via EF Core) | ✅ |
| Bulk import from Excel (.xlsx) | ✅ |
| Export to Excel (.xlsx) | ✅ |
| **Bonus:** Calendar view (FullCalendar) | ✅ |
| **Bonus:** Drag-and-drop reordering (persisted via SortOrder field) | ✅ |

### Sorting — tasks with no due date
Tasks with no due date are sorted to the **end** when sorting ascending, and to the **beginning** when sorting descending, so dated tasks always appear first in the most common use case.

---

## Tech Stack

- **ASP.NET Core MVC** — web framework
- **Entity Framework Core + SQL Server** — local persistence
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
| SQL Server | Any edition (Express is fine) | https://www.microsoft.com/en-us/sql-server/sql-server-downloads |
| Git | Any recent version | https://git-scm.com |

---

## Running the App

### Option A — Visual Studio (recommended)

1. Clone or download the repository
2. Open `TaskOverflow.sln` in **Visual Studio 2022**
3. Open `appsettings.json` and update the connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=TaskOverflow;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

> If using SQL Server Express, change `Server=localhost` to `Server=localhost\SQLEXPRESS`

4. Open the **Package Manager Console** (Tools → NuGet Package Manager → Package Manager Console) and run:

```
Update-Database
```

5. Press **F5** (or click the green ▶ Run button) — the browser will open automatically

---

### Option B — Command Line

```bash
# 1. Clone the repo
git clone https://github.com/your-username/TaskOverflow.git
cd TaskOverflow

# 2. Update the connection string in appsettings.json (see above)

# 3. Apply migrations
dotnet ef database update

# 4. Run
dotnet run
```

Then open `https://localhost:5001` in your browser.

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
│   ├── AccountController.cs    # Login, register, profile, password
│   └── TasksController.cs      # Task CRUD, import/export, dashboard
├── Views/
│   ├── Tasks/
│   │   ├── Index.cshtml         # Task list (search, filter, sort, pagination)
│   │   ├── Create.cshtml        # New task form
│   │   ├── Edit.cshtml          # Edit task form
│   │   ├── Details.cshtml       # Task detail view
│   │   ├── Dashboard.cshtml     # Dashboard + FullCalendar
│   │   ├── TimelineView.cshtml  # Gantt/timeline view
│   │   └── _TimelineRowInteractive.cshtml
│   └── Account/
│       ├── Register.cshtml
│       └── Profile.cshtml
├── Models/                      # Data models and ViewModels
├── Data/                        # ApplicationDbContext (EF Core)
└── appsettings.json
```

---

## Troubleshooting

**`dotnet ef` command not found**
```bash
dotnet tool install --global dotnet-ef
```

**Database connection error**
- Make sure SQL Server is running (check Services or SQL Server Configuration Manager)
- Double-check the connection string in `appsettings.json`
- Re-run `Update-Database` in Package Manager Console

**Port already in use**
Change the port in `Properties/launchSettings.json`, or run:
```bash
dotnet run --urls "https://localhost:5002"
```
