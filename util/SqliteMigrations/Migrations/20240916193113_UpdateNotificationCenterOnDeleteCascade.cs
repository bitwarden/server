using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateNotificationCenterOnDeleteCascade : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_NotificationStatus",
            table: "NotificationStatus");

        migrationBuilder.DropIndex(
            name: "IX_NotificationStatus_NotificationId",
            table: "NotificationStatus");

        migrationBuilder.AddPrimaryKey(
            name: "PK_NotificationStatus",
            table: "NotificationStatus",
            columns: new[] { "NotificationId", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_NotificationStatus_UserId",
            table: "NotificationStatus",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_NotificationStatus",
            table: "NotificationStatus");

        migrationBuilder.DropIndex(
            name: "IX_NotificationStatus_UserId",
            table: "NotificationStatus");

        migrationBuilder.AddPrimaryKey(
            name: "PK_NotificationStatus",
            table: "NotificationStatus",
            columns: new[] { "UserId", "NotificationId" });

        migrationBuilder.CreateIndex(
            name: "IX_NotificationStatus_NotificationId",
            table: "NotificationStatus",
            column: "NotificationId");
    }
}
