using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Bit.PostgresMigrations.Migrations;

public partial class RemoveU2F : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "U2f");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "U2f",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AppId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Challenge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                KeyHandle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_U2f", x => x.Id);
                table.ForeignKey(
                    name: "FK_U2f_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_U2f_UserId",
            table: "U2f",
            column: "UserId");
    }
}
