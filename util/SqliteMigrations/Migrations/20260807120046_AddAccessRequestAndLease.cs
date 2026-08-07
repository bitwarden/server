using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddAccessRequestAndLease : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccessDecision",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AccessRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                DeciderKind = table.Column<byte>(type: "INTEGER", nullable: false),
                ApproverId = table.Column<Guid>(type: "TEXT", nullable: true),
                ConditionKind = table.Column<byte>(type: "INTEGER", nullable: true),
                Verdict = table.Column<byte>(type: "INTEGER", nullable: false),
                Comment = table.Column<string>(type: "TEXT", nullable: true),
                EvaluationContext = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessDecision", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AccessLease",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AccessRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                CollectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                CipherId = table.Column<Guid>(type: "TEXT", nullable: false),
                RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                NotBefore = table.Column<DateTime>(type: "TEXT", nullable: false),
                NotAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevokedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                RevokedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessLease", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessLease_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AccessRequest",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ExtensionOfLeaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                CollectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                CipherId = table.Column<Guid>(type: "TEXT", nullable: false),
                RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                RuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                NotBefore = table.Column<DateTime>(type: "TEXT", nullable: false),
                NotAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                Reason = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessRequest", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessRequest_AccessLease_ExtensionOfLeaseId",
                    column: x => x.ExtensionOfLeaseId,
                    principalTable: "AccessLease",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AccessRequest_AccessRule_RuleId",
                    column: x => x.RuleId,
                    principalTable: "AccessRule",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AccessRequest_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccessDecision_AccessRequestId",
            table: "AccessDecision",
            column: "AccessRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_AccessRequestId",
            table: "AccessLease",
            column: "AccessRequestId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_CollectionId_Status",
            table: "AccessLease",
            columns: new[] { "CollectionId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_NotAfter_Status",
            table: "AccessLease",
            columns: new[] { "NotAfter", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_OrganizationId",
            table: "AccessLease",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_RequesterId_CipherId_Status",
            table: "AccessLease",
            columns: new[] { "RequesterId", "CipherId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_ExtensionOfLeaseId",
            table: "AccessRequest",
            column: "ExtensionOfLeaseId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_OrganizationId_Status",
            table: "AccessRequest",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_RequesterId_CipherId_Status",
            table: "AccessRequest",
            columns: new[] { "RequesterId", "CipherId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_RuleId",
            table: "AccessRequest",
            column: "RuleId");

        migrationBuilder.AddForeignKey(
            name: "FK_AccessDecision_AccessRequest_AccessRequestId",
            table: "AccessDecision",
            column: "AccessRequestId",
            principalTable: "AccessRequest",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AccessLease_AccessRequest_AccessRequestId",
            table: "AccessLease",
            column: "AccessRequestId",
            principalTable: "AccessRequest",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccessLease_AccessRequest_AccessRequestId",
            table: "AccessLease");

        migrationBuilder.DropTable(
            name: "AccessDecision");

        migrationBuilder.DropTable(
            name: "AccessRequest");

        migrationBuilder.DropTable(
            name: "AccessLease");
    }
}
