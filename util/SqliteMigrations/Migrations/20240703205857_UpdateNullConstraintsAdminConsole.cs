using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateNullConstraintsAdminConsole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Culture",
            table: "User",
            type: "TEXT",
            maxLength: 10,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "PostalCode",
            table: "TaxRate",
            type: "TEXT",
            maxLength: 10,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Country",
            table: "TaxRate",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Txt",
            table: "OrganizationDomain",
            type: "TEXT",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "DomainName",
            table: "OrganizationDomain",
            type: "TEXT",
            maxLength: 255,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 255,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ApiKey",
            table: "OrganizationApiKey",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 30,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Plan",
            table: "Organization",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Organization",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "BillingEmail",
            table: "Organization",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Installation",
            type: "TEXT",
            maxLength: 150,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 150,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Installation",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Group",
            type: "TEXT",
            maxLength: 100,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Device",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Identifier",
            table: "Device",
            type: "TEXT",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Collection",
            type: "TEXT",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Scope",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 4000,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 4000,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "TEXT",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 4000,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 4000,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Culture",
            table: "User",
            type: "TEXT",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "PostalCode",
            table: "TaxRate",
            type: "TEXT",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 10);

        migrationBuilder.AlterColumn<string>(
            name: "Country",
            table: "TaxRate",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Txt",
            table: "OrganizationDomain",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "DomainName",
            table: "OrganizationDomain",
            type: "TEXT",
            maxLength: 255,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 255);

        migrationBuilder.AlterColumn<string>(
            name: "ApiKey",
            table: "OrganizationApiKey",
            type: "TEXT",
            maxLength: 30,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 30);

        migrationBuilder.AlterColumn<string>(
            name: "Plan",
            table: "Organization",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Organization",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "BillingEmail",
            table: "Organization",
            type: "TEXT",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256);

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Installation",
            type: "TEXT",
            maxLength: 150,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 150);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Installation",
            type: "TEXT",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Group",
            type: "TEXT",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Device",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Identifier",
            table: "Device",
            type: "TEXT",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Collection",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "Scope",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 4000);

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "TEXT",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 4000);
    }
}
