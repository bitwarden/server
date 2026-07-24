using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AccessRequestId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                DeciderKind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                ApproverId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ConditionKind = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                Verdict = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Comment = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EvaluationContext = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessDecision", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "AccessLease",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AccessRequestId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RequesterId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                NotBefore = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                NotAfter = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevokedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                RevokedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "AccessRequest",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ExtensionOfLeaseId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RequesterId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RuleId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                NotBefore = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                NotAfter = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Reason = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
