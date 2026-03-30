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
            name: "AutofillTriageReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                TargetElementRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                UserMessage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReportData = table.Column<string>(type: "character varying(51200)", maxLength: 51200, nullable: false),
                ExtensionVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutofillTriageReport", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AutofillTriageReport_CreationDate",
            table: "AutofillTriageReport",
            columns: new[] { "Archived", "CreationDate" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AutofillTriageReport_CreationDate",
            table: "AutofillTriageReport");

        migrationBuilder.DropTable(
            name: "AutofillTriageReport");
    }
}
