using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SendKeyAndDataNotNull : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}
