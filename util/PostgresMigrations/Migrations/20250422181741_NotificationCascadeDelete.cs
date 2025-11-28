using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class NotificationCascadeDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification");

        migrationBuilder.AddForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification",
            column: "TaskId",
            principalTable: "SecurityTask",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification");

        migrationBuilder.AddForeignKey(
            name: "FK_Notification_SecurityTask_TaskId",
            table: "Notification",
            column: "TaskId",
            principalTable: "SecurityTask",
            principalColumn: "Id");
    }
}
