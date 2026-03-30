using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddAutofillTriageReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutofillTriageReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                TargetElementRef = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                UserMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                ReportData = table.Column<string>(type: "TEXT", maxLength: 51200, nullable: false),
                ExtensionVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Archived = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutofillTriageReport", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AutofillTriageReport");
    }
}
