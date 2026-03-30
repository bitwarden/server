using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PageUrl = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TargetElementRef = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserMessage = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReportData = table.Column<string>(type: "longtext", maxLength: 51200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExtensionVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Archived = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutofillTriageReport", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AutofillTriageReport");
    }
}
