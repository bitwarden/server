using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class NotificationCenter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notification",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Priority = table.Column<byte>(type: "INTEGER", nullable: false),
                Global = table.Column<bool>(type: "INTEGER", nullable: false),
                ClientType = table.Column<byte>(type: "INTEGER", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Body = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notification", x => x.Id);
                table.ForeignKey(
                    name: "FK_Notification_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Notification_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "NotificationStatus",
            columns: table => new
            {
                NotificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ReadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                DeletedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationStatus", x => new { x.UserId, x.NotificationId });
                table.ForeignKey(
                    name: "FK_NotificationStatus_Notification_NotificationId",
                    column: x => x.NotificationId,
                    principalTable: "Notification",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_NotificationStatus_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notification_ClientType_Global_UserId_OrganizationId_Priority_CreationDate",
            table: "Notification",
            columns: new[] { "ClientType", "Global", "UserId", "OrganizationId", "Priority", "CreationDate" },
            descending: new[] { false, false, false, false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Notification_OrganizationId",
            table: "Notification",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_UserId",
            table: "Notification",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationStatus_NotificationId",
            table: "NotificationStatus",
            column: "NotificationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotificationStatus");

        migrationBuilder.DropTable(
            name: "Notification");
    }
}
