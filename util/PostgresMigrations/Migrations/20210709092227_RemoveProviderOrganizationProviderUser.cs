using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class RemoveProviderOrganizationProviderUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProviderOrganizationProviderUser");

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "Event",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderUserId",
            table: "Event",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                Permissions = table.Column<string>(type: "text", nullable: true),
                ProviderOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderOrganizationProviderUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderOrganizationProviderUser_ProviderOrganization_Provi~",
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
            });

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
