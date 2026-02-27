\# TaskOverflow - Comprehensive To-Do List Application



\## 📋 Overview



TaskOverflow is a feature-rich to-do list web application built with C# and ASP.NET Core MVC. It allows users to efficiently manage tasks with CRUD operations, filtering, sorting, pagination, and import/export capabilities.



\## ✨ Features



\### Core Functionality

\- \*\*User Authentication\*\* - Register, login, and password reset functionality

\- \*\*Task Management\*\* - Create, read, update, and delete tasks

\- \*\*Task Properties\*\* - Description (required), Due Date (optional), Category (Work/Personal/Urgent)

\- \*\*Status Tracking\*\* - Mark tasks as complete/incomplete



\### Advanced Features

\- \*\*Pagination\*\* - Choose page sizes: 5, 10, 15, or 20 items per page

\- \*\*Search\*\* - Filter tasks by keyword in description

\- \*\*Filter\*\* - View All, Completed, or Incomplete tasks

\- \*\*Sort\*\* - Order by due date (ascending/descending)

&nbsp; - \*Note: Tasks without due dates appear at the end when sorting ascending, beginning when sorting descending\*

\- \*\*Bulk Import\*\* - Import tasks from Excel/CSV files with error handling

\- \*\*Export\*\* - Download tasks as formatted Excel reports



\### Bonus Features (Implemented)

\- \*\*Drag-and-Drop Reordering\*\* - Reorder tasks with persistent SortOrder

\- \*\*Calendar View\*\* - (Optional) View tasks by due date with color-coded categories



\## 🛠️ Tech Stack



\- \*\*Framework\*\*: .NET 8 (ASP.NET Core MVC)

\- \*\*Database\*\*: SQLite with Entity Framework Core

\- \*\*Authentication\*\*: ASP.NET Core Identity

\- \*\*Frontend\*\*: Bootstrap 5, Bootstrap Icons

\- \*\*Data Processing\*\*: EPPlus for Excel import/export



\## 📁 Project Structure



```

TaskOverflow/

├── Controllers/

│   ├── AccountController.cs      # Authentication (login/register)

│   ├── HomeController.cs          # Home page

│   └── TasksController.cs         # Task CRUD and operations

├── Data/

│   └── ApplicationDbContext.cs    # Database context

├── Models/

│   ├── AccountViewModels.cs       # Login/register view models

│   ├── ErrorViewModel.cs          # Error handling

│   └── ViewModels.cs              # Task models and view models

├── Views/

│   ├── Account/                    # Login/register pages

│   ├── Home/                       # Home page

│   ├── Shared/                      # Layout and partials

│   └── Tasks/                       # Task management views

├── wwwroot/

│   └── templates/                   # Sample import templates

├── Migrations/                       # EF Core migrations

├── appsettings.json                   # Configuration

└── Program.cs                          # Application setup

```



\## 🚀 Getting Started



\### Prerequisites

\- \[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

\- \[Visual Studio 2022](https://visualstudio.microsoft.com/) (or any C# editor)

\- \[DB Browser for SQLite](https://sqlitebrowser.org/) (optional, for database viewing)



\### Installation



1\. \*\*Clone the repository\*\*

```bash

git clone https://github.com/yourusername/TaskOverflow.git

cd TaskOverflow

```



2\. \*\*Update database connection\*\* (optional)

The default connection string in `appsettings.json` uses a local SQLite database:

```json

{

&nbsp; "ConnectionStrings": {

&nbsp;   "DefaultConnection": "Data Source=TaskOverflow.db"

&nbsp; }

}

```



3\. \*\*Apply database migrations\*\*

```bash

dotnet ef database update

```

Or in Package Manager Console:

```powershell

Update-Database

```



4\. \*\*Run the application\*\*

```bash

dotnet run

```

Or press F5 in Visual Studio



5\. \*\*Navigate to\*\* `https://localhost:5001` (or the URL shown in console)



\## 📖 Usage Guide



\### First-Time Setup

1\. Click \*\*Register\*\* and create an account

2\. You'll be automatically logged in

3\. Navigate to \*\*My Tasks\*\* to start managing your tasks



\### Managing Tasks

\- \*\*Create Task\*\*: Click "Add Task" and fill in the details

\- \*\*Edit Task\*\*: Click the pencil icon on any task

\- \*\*Delete Task\*\*: Click the trash icon and confirm

\- \*\*Complete Task\*\*: Toggle the switch to mark complete/incomplete



\### Filtering \& Searching

\- \*\*Search\*\*: Type keywords in the search box

\- \*\*Filter\*\*: Select All/Completed/Incomplete from dropdown

\- \*\*Sort\*\*: Choose ascending/descending by due date

\- \*\*Page Size\*\*: Select 5, 10, 15, or 20 items per page



\### Import/Export

\- \*\*Export\*\*: Click the download button → "Export to Excel"

\- \*\*Import\*\*: Click the download button → "Import from Excel"

&nbsp; - Use the provided template in `wwwroot/templates/`

&nbsp; - Columns: Description, Due Date (YYYY-MM-DD), Category (Work/Personal/Urgent), Status (Completed/Incomplete)



\## 🗄️ Database Design



\### Key Tables

\- \*\*AspNetUsers\*\* - User accounts (extended with FirstName/LastName)

\- \*\*AspNetRoles\*\* - Role definitions (Admin, Manager, User)

\- \*\*AspNetUserRoles\*\* - User-role assignments

\- \*\*Tasks\*\* - Task items with foreign key to Users

\- \*\*AspNetUserTokens\*\* - Password reset tokens (kept for security features)



\### Data Persistence Decision

\- \*\*Local Storage\*\*: SQLite with Entity Framework Core

\- Tasks persist across application restarts and page reloads

\- User data is isolated per authenticated user



\## 🔒 Authentication \& Authorization



\- \*\*Identity Framework\*\* handles all authentication

\- \*\*Password Requirements\*\*: Min 6 characters, 1 uppercase, 1 digit

\- \*\*Roles Implemented\*\*: Admin, Manager, User (for view differentiation)

\- \*\*Password Reset\*\*: Fully functional using AspNetUserTokens table



\## 📊 Import/Export Specifications



\### Import Template

```

Description,Due Date,Category,Status

"Complete project documentation",2024-12-31,Work,Incomplete

"Buy groceries",2024-03-15,Personal,Incomplete

"Submit urgent report",2024-03-10,Urgent,Completed

```



\### Error Handling

\- Invalid rows are skipped with detailed error messages

\- Import summary shows success/failure counts

\- Graceful handling of malformed dates or missing data



\### Export Format

Excel (.xlsx) with columns:

\- Description

\- Due Date

\- Category

\- Status

\- Created At (additional metadata)



\## 🎨 UI Framework



\- \*\*Bootstrap 5\*\* for responsive design

\- \*\*Bootstrap Icons\*\* for visual elements

\- \*\*Custom CSS\*\* for additional styling

\- \*\*Toast notifications\*\* for user feedback



\## 🧪 Testing the Application



\### Test Credentials

After seeding (optional), you can use:

\- \*\*Admin\*\*: admin@example.com / Admin@123456

\- \*\*Manager\*\*: manager@example.com / Manager@123456

\- \*\*User\*\*: user@example.com / User@123456



\### Sample Tasks to Create

1\. "Complete project documentation" - Work - Due next week

2\. "Team meeting" - Work - Due tomorrow

3\. "Grocery shopping" - Personal - Due this weekend

4\. "Submit report" - Urgent - Due today



\## 🔧 Configuration



\### appsettings.json

```json

{

&nbsp; "Logging": {

&nbsp;   "LogLevel": {

&nbsp;     "Default": "Information",

&nbsp;     "Microsoft.AspNetCore": "Warning"

&nbsp;   }

&nbsp; },

&nbsp; "AllowedHosts": "\*",

&nbsp; "ConnectionStrings": {

&nbsp;   "DefaultConnection": "Data Source=TaskOverflow.db"

&nbsp; }

}

```



\## 📦 Dependencies



\- Microsoft.AspNetCore.Identity.EntityFrameworkCore (8.0.0)

\- Microsoft.EntityFrameworkCore.Sqlite (8.0.0)

\- Microsoft.EntityFrameworkCore.Tools (8.0.8)

\- EPPlus (8.4.2) - Excel import/export

\- Bootstrap 5 (via CDN)

\- Bootstrap Icons (via CDN)



\## 🚧 Future Enhancements



\- \[ ] Calendar view for task visualization

\- \[ ] Email notifications for upcoming tasks

\- \[ ] Task sharing/collaboration

\- \[ ] Subtasks and checklists

\- \[ ] Task attachments

\- \[ ] Dark mode theme

\- \[ ] Mobile-responsive optimizations



\## 🤝 Contributing



1\. Fork the repository

2\. Create a feature branch (`git checkout -b feature/AmazingFeature`)

3\. Commit changes (`git commit -m 'Add AmazingFeature'`)

4\. Push to branch (`git push origin feature/AmazingFeature`)

5\. Open a Pull Request



\## 📝 License



This project is for educational purposes as part of a technical assignment.



\## 👨‍💻 Author



Your Name

\- GitHub: \[@jxryan-lim](https://github.com/jxryan-lim)



\## 🙏 Acknowledgments



\- Assignment requirements for guiding feature development

\- ASP.NET Core documentation and community

\- Bootstrap for the UI components

\- EPPlus team for Excel processing capabilities



\## 📞 Support



For issues or questions:

1\. Check the existing documentation

2\. Review error messages in the application

3\. Open an issue on GitHub

4\. Contact the development team



---



\*\*Built with ❤️ using C# and .NET 8\*\*

