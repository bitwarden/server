using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class RemoveProviderOrganizationProviderUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProviderOrganizationProviderUser");

        migrationBuilder.AddColumn<bool>(
            name: "UseEvents",
            table: "Provider",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "Event",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderUserId",
            table: "Event",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseEvents",
            table: "Provider");

        migrationBuilder.DropColumn(
            name: "ProviderId",
            table: "Event");

        migrationBuilder.DropColumn(
            name: "ProviderUserId",
            table: "Event");

        migrationBuilder.CreateTable(
            name: "ProviderOrganizationProviderUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Permissions = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ProviderOrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderOrganizationProviderUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderOrganizationProviderUser_ProviderOrganization_Provid~",
                    column: x => x.ProviderOrganizationId,
                    principalTable: "ProviderOrganization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProviderOrganizationProviderUser_ProviderUser_ProviderUserId",
                    column: x => x.ProviderUserId,
                    principalTable: "ProviderUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganizationProviderUser_ProviderOrganizationId",
            table: "ProviderOrganizationProviderUser",
            column: "ProviderOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganizationProviderUser_ProviderUserId",
            table: "ProviderOrganizationProviderUser",
            column: "ProviderUserId");
    }
}
