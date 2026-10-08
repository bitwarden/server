using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentFillApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentFillApprovalRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SealedRequest = table.Column<string>(type: "TEXT", nullable: false),
                    SealedResponse = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFillApprovalRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentFillApprovalRequest_Device_RequestDeviceId",
                        column: x => x.RequestDeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentFillApprovalRequest_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFillApprovalRequest_ExpirationDate",
                table: "AgentFillApprovalRequest",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFillApprovalRequest_RequestDeviceId",
                table: "AgentFillApprovalRequest",
                column: "RequestDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFillApprovalRequest_UserId",
                table: "AgentFillApprovalRequest",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFillApprovalRequest");
        }
    }
}
