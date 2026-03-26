using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddAutofillTriageReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutofillTriageReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                TargetElementRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                UserMessage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReportData = table.Column<string>(type: "character varying(51200)", maxLength: 51200, nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutofillTriageReports", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AutofillTriageReports");
    }
}
