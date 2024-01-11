using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class GrantIdWithIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Grant",
            table: "Grant");

        migrationBuilder.AlterColumn<string>(
            name: "Type",
            table: "Grant",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Grant",
            type: "TEXT",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ClientId",
            table: "Grant",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Id",
            table: "Grant",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1)
            .Annotation("Sqlite:Autoincrement", true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_Grant",
            table: "Grant",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_Grant_Key",
            table: "Grant",
            column: "Key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Grant",
            table: "Grant");

        migrationBuilder.DropIndex(
            name: "IX_Grant_Key",
            table: "Grant");

        migrationBuilder.DropColumn(
            name: "Id",
            table: "Grant");

        migrationBuilder.AlterColumn<string>(
            name: "Type",
            table: "Grant",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Grant",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "ClientId",
            table: "Grant",
            type: "TEXT",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 200);

        migrationBuilder.AddPrimaryKey(
            name: "PK_Grant",
            table: "Grant",
            column: "Key");
    }
}
