using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class OrganizationDomainClaim : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationDomain",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Txt = table.Column<string>(type: "text", nullable: true),
                DomainName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                VerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextRunDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                NextRunCount = table.Column<int>(type: "integer", nullable: false)
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
    }
}
