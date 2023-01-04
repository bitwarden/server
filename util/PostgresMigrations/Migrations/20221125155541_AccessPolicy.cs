using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class AccessPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccessPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                GrantedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                GrantedServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Read = table.Column<bool>(type: "boolean", nullable: false),
                Write = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                Discriminator = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessPolicy", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessPolicy_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AccessPolicy_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AccessPolicy_Project_GrantedProjectId",
                    column: x => x.GrantedProjectId,
                    principalTable: "Project",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId",
                    column: x => x.GrantedServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AccessPolicy_ServiceAccount_ServiceAccountId",
                    column: x => x.ServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GrantedProjectId",
            table: "AccessPolicy",
            column: "GrantedProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GrantedServiceAccountId",
            table: "AccessPolicy",
            column: "GrantedServiceAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GroupId",
            table: "AccessPolicy",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_OrganizationUserId",
            table: "AccessPolicy",
            column: "OrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_ServiceAccountId",
            table: "AccessPolicy",
            column: "ServiceAccountId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccessPolicy");
    }
}
