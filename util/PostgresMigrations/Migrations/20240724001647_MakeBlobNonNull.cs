using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class MakeBlobNonNull : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "Value",
            table: "Cache",
            type: "bytea",
            nullable: false,
            defaultValue: new byte[0],
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "Value",
            table: "Cache",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");
    }
}
