using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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
                UserGuid = table.Column<Guid>(type: "TEXT", nullable: true),
                UserName = table.Column<string>(type: "TEXT", nullable: true),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                TwoFactorProviders = table.Column<string>(type: "TEXT", nullable: true),
                UsesKeyConnector = table.Column<bool>(type: "INTEGER", nullable: false),
                ResetPasswordKey = table.Column<string>(type: "TEXT", nullable: true),
                CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                GroupName = table.Column<string>(type: "TEXT", nullable: true),
                CollectionName = table.Column<string>(type: "TEXT", nullable: true),
                ReadOnly = table.Column<bool>(type: "INTEGER", nullable: true),
                HidePasswords = table.Column<bool>(type: "INTEGER", nullable: true),
                Manage = table.Column<bool>(type: "INTEGER", nullable: true),
                CipherId = table.Column<Guid>(type: "TEXT", nullable: false)
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
