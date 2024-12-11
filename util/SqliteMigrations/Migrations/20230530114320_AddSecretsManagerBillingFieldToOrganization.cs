﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class AddSecretsManagerBillingFieldToOrganization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MaxAutoscaleSmSeats",
            table: "Organization",
            type: "INTEGER",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "MaxAutoscaleSmServiceAccounts",
            table: "Organization",
            type: "INTEGER",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "SmSeats",
            table: "Organization",
            type: "INTEGER",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "SmServiceAccounts",
            table: "Organization",
            type: "INTEGER",
            nullable: true
        );

        migrationBuilder.AddColumn<bool>(
            name: "UsePasswordManager",
            table: "Organization",
            type: "INTEGER",
            nullable: false,
            defaultValue: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MaxAutoscaleSmSeats", table: "Organization");

        migrationBuilder.DropColumn(name: "MaxAutoscaleSmServiceAccounts", table: "Organization");

        migrationBuilder.DropColumn(name: "SmSeats", table: "Organization");

        migrationBuilder.DropColumn(name: "SmServiceAccounts", table: "Organization");

        migrationBuilder.DropColumn(name: "UsePasswordManager", table: "Organization");
    }
}
