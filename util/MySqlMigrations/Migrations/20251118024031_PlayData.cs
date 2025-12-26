using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class PlayData : Migration
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
            oldType: "int");

        migrationBuilder.CreateTable(
            name: "PlayData",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PlayId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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

        migrationBuilder.AlterColumn<int>(
            name: "WaitTimeDays",
            table: "EmergencyAccess",
            type: "int",
            nullable: false,
            oldClrType: typeof(short),
            oldType: "smallint");
    }
}
