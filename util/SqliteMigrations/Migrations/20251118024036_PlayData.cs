using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class PlayData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlayData",
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
                table.PrimaryKey("PK_PlayData", x => x.Id);
                table.CheckConstraint("CK_PlayData_UserOrOrganization", "(\"UserId\" IS NOT NULL AND \"OrganizationId\" IS NULL) OR (\"UserId\" IS NULL AND \"OrganizationId\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_PlayData_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayData_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_OrganizationId",
            table: "PlayData",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_PlayId",
            table: "PlayData",
            column: "PlayId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_UserId",
            table: "PlayData",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlayData");
    }
}
