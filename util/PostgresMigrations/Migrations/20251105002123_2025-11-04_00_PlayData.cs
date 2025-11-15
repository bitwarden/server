using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class _20251104_00_PlayData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<short>(
            name: "WaitTimeDays",
            table: "EmergencyAccess",
            type: "smallint",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.CreateTable(
            name: "PlayData",
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
                table.PrimaryKey("PK_PlayData", x => x.Id);
                table.CheckConstraint("CK_PlayData_UserOrOrganization", "([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL)");
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

        migrationBuilder.CreateTable(
            name: "SeededData",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipeName = table.Column<string>(type: "text", nullable: false),
                Data = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SeededData", x => x.Id);
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

        migrationBuilder.DropTable(
            name: "SeededData");

        migrationBuilder.AlterColumn<int>(
            name: "WaitTimeDays",
            table: "EmergencyAccess",
            type: "integer",
            nullable: false,
            oldClrType: typeof(short),
            oldType: "smallint");
    }
}
