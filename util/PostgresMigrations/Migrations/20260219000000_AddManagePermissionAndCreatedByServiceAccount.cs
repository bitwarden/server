using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddManagePermissionAndCreatedByServiceAccount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("SET lock_timeout = '5s'");
        migrationBuilder.AddColumn<bool>(
            name: "Manage",
            table: "AccessPolicy",
            type: "boolean",
            nullable: false,
            defaultValue: false);
        migrationBuilder.Sql("RESET lock_timeout");

        // Backfill: preserve current behavior — user and group policies with Write=true get Manage=true
        // Service account policies remain Manage=false per spec
        migrationBuilder.Sql(@"
            UPDATE ""AccessPolicy""
            SET ""Manage"" = TRUE
            WHERE ""Write"" = TRUE
              AND ""Discriminator"" IN (
                'user_project',
                'user_service_account',
                'user_secret',
                'group_project',
                'group_service_account',
                'group_secret'
              )");

        // Enforce permission hierarchy at the DB level
        migrationBuilder.Sql("SET lock_timeout = '5s'");
        migrationBuilder.Sql(@"
            ALTER TABLE ""AccessPolicy""
                ADD CONSTRAINT ""CK_AccessPolicy_ManageImpliesWrite"" CHECK (""Manage"" = FALSE OR ""Write"" = TRUE),
                ADD CONSTRAINT ""CK_AccessPolicy_WriteImpliesRead"" CHECK (""Write"" = FALSE OR ""Read"" = TRUE)");
        migrationBuilder.Sql("RESET lock_timeout");

        migrationBuilder.Sql("SET lock_timeout = '5s'");
        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByServiceAccountId",
            table: "Project",
            type: "uuid",
            nullable: true);
        migrationBuilder.Sql("RESET lock_timeout");

        migrationBuilder.CreateIndex(
            name: "IX_Project_CreatedByServiceAccountId",
            table: "Project",
            column: "CreatedByServiceAccountId");

        migrationBuilder.AddForeignKey(
            name: "FK_Project_ServiceAccount_CreatedByServiceAccountId",
            table: "Project",
            column: "CreatedByServiceAccountId",
            principalTable: "ServiceAccount",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Project_ServiceAccount_CreatedByServiceAccountId",
            table: "Project");

        migrationBuilder.DropIndex(
            name: "IX_Project_CreatedByServiceAccountId",
            table: "Project");

        migrationBuilder.DropColumn(
            name: "CreatedByServiceAccountId",
            table: "Project");

        migrationBuilder.Sql(@"
            ALTER TABLE ""AccessPolicy""
                DROP CONSTRAINT IF EXISTS ""CK_AccessPolicy_ManageImpliesWrite"",
                DROP CONSTRAINT IF EXISTS ""CK_AccessPolicy_WriteImpliesRead""");

        migrationBuilder.DropColumn(
            name: "Manage",
            table: "AccessPolicy");
    }
}
