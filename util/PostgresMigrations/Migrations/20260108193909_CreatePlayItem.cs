using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class CreatePlayItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlayItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PlayId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayItem", x => x.Id);
                table.CheckConstraint("CK_PlayItem_UserOrOrganization", "(\"UserId\" IS NOT NULL AND \"OrganizationId\" IS NULL) OR (\"UserId\" IS NULL AND \"OrganizationId\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_PlayItem_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayItem_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlayItem_OrganizationId",
            table: "PlayItem",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayItem_PlayId",
            table: "PlayItem",
            column: "PlayId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayItem_UserId",
            table: "PlayItem",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlayItem");
    }
}
