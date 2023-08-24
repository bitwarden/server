using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class ACCESSPOLICY_ADD_DELETE_CASCADE : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccessPolicy_Group_GroupId",
            table: "AccessPolicy");

        migrationBuilder.DropForeignKey(
            name: "FK_AccessPolicy_Project_GrantedProjectId",
            table: "AccessPolicy");

        migrationBuilder.AddForeignKey(
            name: "FK_AccessPolicy_Group_GroupId",
            table: "AccessPolicy",
            column: "GroupId",
            principalTable: "Group",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AccessPolicy_Project_GrantedProjectId",
            table: "AccessPolicy",
            column: "GrantedProjectId",
            principalTable: "Project",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccessPolicy_Group_GroupId",
            table: "AccessPolicy");

        migrationBuilder.DropForeignKey(
            name: "FK_AccessPolicy_Project_GrantedProjectId",
            table: "AccessPolicy");

        migrationBuilder.AddForeignKey(
            name: "FK_AccessPolicy_Group_GroupId",
            table: "AccessPolicy",
            column: "GroupId",
            principalTable: "Group",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_AccessPolicy_Project_GrantedProjectId",
            table: "AccessPolicy",
            column: "GrantedProjectId",
            principalTable: "Project",
            principalColumn: "Id");
    }
}
