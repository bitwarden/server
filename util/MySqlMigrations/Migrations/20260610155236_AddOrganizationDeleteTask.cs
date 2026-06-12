using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TaskType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ItemsDeletedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDeleteTask", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
