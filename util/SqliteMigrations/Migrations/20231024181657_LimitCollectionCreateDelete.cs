﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class LimitCollectionCreateDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "LimitCollectionCreationDeletion",
            table: "Organization",
            type: "INTEGER",
            nullable: false,
            defaultValue: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LimitCollectionCreationDeletion", table: "Organization");
    }
}
