using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                UserGuid = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                UserName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Email = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TwoFactorProviders = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UsesKeyConnector = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ResetPasswordKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GroupName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CollectionName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReadOnly = table.Column<bool>(type: "tinyint(1)", nullable: true),
                HidePasswords = table.Column<bool>(type: "tinyint(1)", nullable: true),
                Manage = table.Column<bool>(type: "tinyint(1)", nullable: true),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
            })
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationMemberBaseDetails");
    }
}
