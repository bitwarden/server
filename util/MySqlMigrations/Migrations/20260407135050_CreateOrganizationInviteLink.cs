using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class CreateOrganizationInviteLink : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationInviteLink",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Code = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AllowedDomains = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedInviteKey = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedOrgKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationInviteLink", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationInviteLink_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInviteLink_Code",
            table: "OrganizationInviteLink",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInviteLink_OrganizationId",
            table: "OrganizationInviteLink",
            column: "OrganizationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationInviteLink");
    }
}
