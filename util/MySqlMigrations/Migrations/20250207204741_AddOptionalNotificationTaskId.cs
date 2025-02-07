using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddOptionalNotifificationTaskId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "TaskId",
            table: "Notification",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_TaskId",
            table: "Notification",
            column: "TaskId");

        migrationBuilder.AddForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification",
            column: "TaskId",
            principalTable: "SecurityTask",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification");

        migrationBuilder.DropIndex(
            name: "IX_Notification_TaskId",
            table: "Notification");

        migrationBuilder.DropColumn(
            name: "TaskId",
            table: "Notification");
    }
}
