using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class Net8Sync : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem");

        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceNumber",
            table: "ProviderInvoiceItem",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Discriminator",
            table: "AccessPolicy",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceNumber",
            table: "ProviderInvoiceItem",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Discriminator",
            table: "AccessPolicy",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34);

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem",
            columns: new[] { "Id", "InvoiceId" },
            unique: true);
    }
}
