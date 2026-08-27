using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DerivePamStatusFromAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessRequest_CollectionId_Status",
                table: "AccessRequest");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AccessRequest",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "ResolvedDate",
                table: "AccessRequest",
                newName: "ActionDate");

            migrationBuilder.RenameIndex(
                name: "IX_AccessRequest_RequesterId_CipherId_Status",
                table: "AccessRequest",
                newName: "IX_AccessRequest_RequesterId_CipherId_Action");

            migrationBuilder.RenameIndex(
                name: "IX_AccessRequest_OrganizationId_Status",
                table: "AccessRequest",
                newName: "IX_AccessRequest_OrganizationId_Action");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AccessLease",
                newName: "Action");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_RequesterId_CipherId_Status",
                table: "AccessLease",
                newName: "IX_AccessLease_RequesterId_CipherId_Action");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_NotAfter_Status",
                table: "AccessLease",
                newName: "IX_AccessLease_NotAfter_Action");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_CollectionId_Status",
                table: "AccessLease",
                newName: "IX_AccessLease_CollectionId_Action");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_CipherId_Status",
                table: "AccessLease",
                newName: "IX_AccessLease_CipherId_Action");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequest_CollectionId_Action_NotAfter",
                table: "AccessRequest",
                columns: new[] { "CollectionId", "Action", "NotAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequest_CollectionId_CreationDate",
                table: "AccessRequest",
                columns: new[] { "CollectionId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequest_RequesterId_CreationDate",
                table: "AccessRequest",
                columns: new[] { "RequesterId", "CreationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessRequest_CollectionId_Action_NotAfter",
                table: "AccessRequest");

            migrationBuilder.DropIndex(
                name: "IX_AccessRequest_CollectionId_CreationDate",
                table: "AccessRequest");

            migrationBuilder.DropIndex(
                name: "IX_AccessRequest_RequesterId_CreationDate",
                table: "AccessRequest");

            migrationBuilder.RenameColumn(
                name: "ActionDate",
                table: "AccessRequest",
                newName: "ResolvedDate");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "AccessRequest",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessRequest_RequesterId_CipherId_Action",
                table: "AccessRequest",
                newName: "IX_AccessRequest_RequesterId_CipherId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessRequest_OrganizationId_Action",
                table: "AccessRequest",
                newName: "IX_AccessRequest_OrganizationId_Status");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "AccessLease",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_RequesterId_CipherId_Action",
                table: "AccessLease",
                newName: "IX_AccessLease_RequesterId_CipherId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_NotAfter_Action",
                table: "AccessLease",
                newName: "IX_AccessLease_NotAfter_Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_CollectionId_Action",
                table: "AccessLease",
                newName: "IX_AccessLease_CollectionId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_AccessLease_CipherId_Action",
                table: "AccessLease",
                newName: "IX_AccessLease_CipherId_Status");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequest_CollectionId_Status",
                table: "AccessRequest",
                columns: new[] { "CollectionId", "Status" });
        }
    }
}
