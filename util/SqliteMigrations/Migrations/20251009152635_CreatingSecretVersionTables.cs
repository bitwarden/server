﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class CreatingSecretVersionTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecretVersion",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                SecretId = table.Column<Guid>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false),
                VersionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                EditorServiceAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                EditorOrganizationUserId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecretVersion", x => x.Id);
                table.ForeignKey(
                    name: "FK_SecretVersion_OrganizationUser_EditorOrganizationUserId",
                    column: x => x.EditorOrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_SecretVersion_Secret_SecretId",
                    column: x => x.SecretId,
                    principalTable: "Secret",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SecretVersion_ServiceAccount_EditorServiceAccountId",
                    column: x => x.EditorServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersion_EditorOrganizationUserId",
            table: "SecretVersion",
            column: "EditorOrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersion_EditorServiceAccountId",
            table: "SecretVersion",
            column: "EditorServiceAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersion_SecretId",
            table: "SecretVersion",
            column: "SecretId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SecretVersion");
    }
}
