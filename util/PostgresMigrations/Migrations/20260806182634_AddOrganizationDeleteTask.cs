using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationDeleteTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationDeleteTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<byte>(type: "smallint", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemsDeletedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDeleteTask", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDeleteTask_CompletedDate_CreationDate",
                table: "OrganizationDeleteTask",
                columns: new[] { "CompletedDate", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDeleteTask_Id",
                table: "OrganizationDeleteTask",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationDeleteTask");
        }
    }
}
