using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateNullConstraintsAdminConsole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "User",
            keyColumn: "Culture",
            keyValue: null,
            column: "Culture",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Culture",
            table: "User",
            type: "varchar(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(10)",
            oldMaxLength: 10,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "TaxRate",
            keyColumn: "PostalCode",
            keyValue: null,
            column: "PostalCode",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "PostalCode",
            table: "TaxRate",
            type: "varchar(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(10)",
            oldMaxLength: 10,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "TaxRate",
            keyColumn: "Country",
            keyValue: null,
            column: "Country",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Country",
            table: "TaxRate",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ProviderInvoiceItem",
            keyColumn: "PlanName",
            keyValue: null,
            column: "PlanName",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ProviderInvoiceItem",
            keyColumn: "InvoiceId",
            keyValue: null,
            column: "InvoiceId",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ProviderInvoiceItem",
            keyColumn: "ClientName",
            keyValue: null,
            column: "ClientName",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "OrganizationDomain",
            keyColumn: "Txt",
            keyValue: null,
            column: "Txt",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Txt",
            table: "OrganizationDomain",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "OrganizationDomain",
            keyColumn: "DomainName",
            keyValue: null,
            column: "DomainName",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "DomainName",
            table: "OrganizationDomain",
            type: "varchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "OrganizationApiKey",
            keyColumn: "ApiKey",
            keyValue: null,
            column: "ApiKey",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "ApiKey",
            table: "OrganizationApiKey",
            type: "varchar(30)",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldMaxLength: 30,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Organization",
            keyColumn: "Plan",
            keyValue: null,
            column: "Plan",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Plan",
            table: "Organization",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Organization",
            keyColumn: "Name",
            keyValue: null,
            column: "Name",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Organization",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Organization",
            keyColumn: "BillingEmail",
            keyValue: null,
            column: "BillingEmail",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "BillingEmail",
            table: "Organization",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Installation",
            keyColumn: "Key",
            keyValue: null,
            column: "Key",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Installation",
            type: "varchar(150)",
            maxLength: 150,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(150)",
            oldMaxLength: 150,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Installation",
            keyColumn: "Email",
            keyValue: null,
            column: "Email",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Installation",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Group",
            keyColumn: "Name",
            keyValue: null,
            column: "Name",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Group",
            type: "varchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(100)",
            oldMaxLength: 100,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Device",
            keyColumn: "Name",
            keyValue: null,
            column: "Name",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Device",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Device",
            keyColumn: "Identifier",
            keyValue: null,
            column: "Identifier",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Identifier",
            table: "Device",
            type: "varchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "Collection",
            keyColumn: "Name",
            keyValue: null,
            column: "Name",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Collection",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "Scope",
            keyValue: null,
            column: "Scope",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Scope",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "Name",
            keyValue: null,
            column: "Name",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "ApiKey",
            type: "varchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(200)",
            oldMaxLength: 200,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "Key",
            keyValue: null,
            column: "Key",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "EncryptedPayload",
            keyValue: null,
            column: "EncryptedPayload",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Culture",
            table: "User",
            type: "varchar(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(10)",
            oldMaxLength: 10)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "PostalCode",
            table: "TaxRate",
            type: "varchar(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(10)",
            oldMaxLength: 10)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Country",
            table: "TaxRate",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "PlanName",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "ClientName",
            table: "ProviderInvoiceItem",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Txt",
            table: "OrganizationDomain",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "DomainName",
            table: "OrganizationDomain",
            type: "varchar(255)",
            maxLength: 255,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "ApiKey",
            table: "OrganizationApiKey",
            type: "varchar(30)",
            maxLength: 30,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(30)",
            oldMaxLength: 30)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Plan",
            table: "Organization",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Organization",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "BillingEmail",
            table: "Organization",
            type: "varchar(256)",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Installation",
            type: "varchar(150)",
            maxLength: 150,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(150)",
            oldMaxLength: 150)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Installation",
            type: "varchar(256)",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Group",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(100)",
            oldMaxLength: 100)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Device",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Identifier",
            table: "Device",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Collection",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Scope",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "ApiKey",
            type: "varchar(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(200)",
            oldMaxLength: 200)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}
