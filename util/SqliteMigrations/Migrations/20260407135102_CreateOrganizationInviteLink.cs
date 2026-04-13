using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Code = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                AllowedDomains = table.Column<string>(type: "TEXT", nullable: false),
                EncryptedInviteKey = table.Column<string>(type: "TEXT", nullable: false),
                EncryptedOrgKey = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
            });

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
