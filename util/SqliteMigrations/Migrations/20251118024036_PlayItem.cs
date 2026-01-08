using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class PlayItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlayItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PlayId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
