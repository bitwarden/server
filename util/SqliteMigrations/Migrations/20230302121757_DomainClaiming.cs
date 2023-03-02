using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class DomainClaiming : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DomainName",
            table: "Event",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "OrganizationDomain",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Txt = table.Column<string>(type: "TEXT", nullable: true),
                DomainName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                VerifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                NextRunDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastCheckedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                JobRunCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationDomain", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationDomain_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationDomain_OrganizationId",
            table: "OrganizationDomain",
            column: "OrganizationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationDomain");

        migrationBuilder.DropColumn(
            name: "DomainName",
            table: "Event");
    }
}
