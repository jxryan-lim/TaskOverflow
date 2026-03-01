using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskOverflow.Migrations
{
    /// <inheritdoc />
    public partial class AddNestedSubtaskSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasChildren",
                table: "SubTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentSubTaskId",
                table: "SubTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_ParentSubTaskId",
                table: "SubTasks",
                column: "ParentSubTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_SortOrder",
                table: "SubTasks",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_SubTasks_SubTasks_ParentSubTaskId",
                table: "SubTasks",
                column: "ParentSubTaskId",
                principalTable: "SubTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubTasks_SubTasks_ParentSubTaskId",
                table: "SubTasks");

            migrationBuilder.DropIndex(
                name: "IX_SubTasks_ParentSubTaskId",
                table: "SubTasks");

            migrationBuilder.DropIndex(
                name: "IX_SubTasks_SortOrder",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "HasChildren",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "ParentSubTaskId",
                table: "SubTasks");
        }
    }
}
