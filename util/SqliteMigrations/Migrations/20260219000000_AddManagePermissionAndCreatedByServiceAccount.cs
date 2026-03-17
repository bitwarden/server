using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddManagePermissionAndCreatedByServiceAccount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Manage",
            table: "AccessPolicy",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        // Backfill: preserve current behavior — user and group policies with Write=1 get Manage=1
        // Service account policies remain Manage=false per spec
        migrationBuilder.Sql(@"
            UPDATE ""AccessPolicy""
            SET ""Manage"" = 1
            WHERE ""Write"" = 1
              AND ""Discriminator"" IN (
                'user_project',
                'user_service_account',
                'user_secret',
                'group_project',
                'group_service_account',
                'group_secret'
              )");

        // SQLite does not support ALTER TABLE ... ADD CONSTRAINT.
        // Check constraints on existing tables require a full table rebuild (12-step SQLite procedure),
        // which is not used in this project. The equivalent constraints are:
        //   CK_AccessPolicy_ManageImpliesWrite: "Manage" = 0 OR "Write" = 1
        //   CK_AccessPolicy_WriteImpliesRead:   "Write" = 0 OR "Read" = 1
        // These are enforced via HasCheckConstraint in AccessPolicyEntityTypeConfiguration,
        // which applies them at database creation time for SQLite.

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByServiceAccountId",
            table: "Project",
            type: "TEXT",
            nullable: true);

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

        migrationBuilder.DropColumn(
            name: "Manage",
            table: "AccessPolicy");
    }
}
