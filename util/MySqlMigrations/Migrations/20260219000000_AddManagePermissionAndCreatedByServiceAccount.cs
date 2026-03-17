using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddManagePermissionAndCreatedByServiceAccount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Manage",
            table: "AccessPolicy",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        // Backfill: preserve current behavior — user and group policies with Write=1 get Manage=1
        // Service account policies remain Manage=false per spec
        migrationBuilder.Sql(@"
            UPDATE `AccessPolicy`
            SET `Manage` = 1
            WHERE `Write` = 1
              AND `Discriminator` IN (
                'user_project',
                'user_service_account',
                'user_secret',
                'group_project',
                'group_service_account',
                'group_secret'
              )");

        // Enforce permission hierarchy at the DB level
        migrationBuilder.Sql(@"
            ALTER TABLE `AccessPolicy`
                ADD CONSTRAINT `CK_AccessPolicy_ManageImpliesWrite` CHECK (`Manage` = 0 OR `Write` = 1),
                ADD CONSTRAINT `CK_AccessPolicy_WriteImpliesRead` CHECK (`Write` = 0 OR `Read` = 1)");

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByServiceAccountId",
            table: "Project",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

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
            ALTER TABLE `AccessPolicy`
                DROP CONSTRAINT IF EXISTS `CK_AccessPolicy_ManageImpliesWrite`,
                DROP CONSTRAINT IF EXISTS `CK_AccessPolicy_WriteImpliesRead`");

        migrationBuilder.DropColumn(
            name: "Manage",
            table: "AccessPolicy");
    }
}
