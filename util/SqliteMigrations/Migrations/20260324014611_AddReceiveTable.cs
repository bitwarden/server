using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddReceiveTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Receive",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                Data = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Secret = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                MaxUploadCount = table.Column<int>(type: "INTEGER", nullable: true),
                UploadCount = table.Column<int>(type: "INTEGER", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Receive", x => x.Id);
                table.ForeignKey(
                    name: "FK_Receive_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Receive_ExpirationDate",
            table: "Receive",
            column: "ExpirationDate");

        migrationBuilder.CreateIndex(
            name: "IX_Receive_UserId",
            table: "Receive",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Receive");
    }
}
