using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class _20250609_00_AddMemberAccessReportStoreProceduresql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationMemberBaseDetails",
            columns: table => new
            {
                UserGuid = table.Column<Guid>(type: "uuid", nullable: true),
                UserName = table.Column<string>(type: "text", nullable: true),
                Email = table.Column<string>(type: "text", nullable: true),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                UsesKeyConnector = table.Column<bool>(type: "boolean", nullable: false),
                ResetPasswordKey = table.Column<string>(type: "text", nullable: true),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupName = table.Column<string>(type: "text", nullable: true),
                CollectionName = table.Column<string>(type: "text", nullable: true),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: true),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: true),
                Manage = table.Column<bool>(type: "boolean", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationMemberBaseDetails");
    }
}
