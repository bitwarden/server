using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class OrganizationDomainClaim : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationDomain",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Txt = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DomainName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                VerifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                NextRunDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                NextRunCount = table.Column<int>(type: "int", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
