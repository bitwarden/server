using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ADD_MISSING_INDEXES : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Premium_PremiumExpirationDate_RenewalReminderDate",
                table: "User",
                columns: new[] { "Premium", "PremiumExpirationDate", "RenewalReminderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UserId_OrganizationId_CreationDate",
                table: "Transaction",
                columns: new[] { "UserId", "OrganizationId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SsoUser_OrganizationId_ExternalId",
                table: "SsoUser",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SsoUser_OrganizationId_UserId",
                table: "SsoUser",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Send_DeletionDate",
                table: "Send",
                column: "DeletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Send_UserId_OrganizationId",
                table: "Send",
                columns: new[] { "UserId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Policy_OrganizationId_Type",
                table: "Policy",
                columns: new[] { "OrganizationId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUser_UserId_OrganizationId_Status",
                table: "OrganizationUser",
                columns: new[] { "UserId", "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSponsorship_SponsoringOrganizationUserId",
                table: "OrganizationSponsorship",
                column: "SponsoringOrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_Id_Enabled",
                table: "Organization",
                columns: new[] { "Id", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Grant_ExpirationDate",
                table: "Grant",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Grant_SubjectId_ClientId_Type",
                table: "Grant",
                columns: new[] { "SubjectId", "ClientId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Grant_SubjectId_SessionId_Type",
                table: "Grant",
                columns: new[] { "SubjectId", "SessionId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Event_Date_OrganizationId_ActingUserId_CipherId",
                table: "Event",
                columns: new[] { "Date", "OrganizationId", "ActingUserId", "CipherId" });

            migrationBuilder.CreateIndex(
                name: "IX_Device_Identifier",
                table: "Device",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Device_UserId_Identifier",
                table: "Device",
                columns: new[] { "UserId", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cipher_DeletedDate",
                table: "Cipher",
                column: "DeletedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cipher_UserId_OrganizationId",
                table: "Cipher",
                columns: new[] { "UserId", "OrganizationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Email",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_Premium_PremiumExpirationDate_RenewalReminderDate",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_UserId_OrganizationId_CreationDate",
                table: "Transaction");

            migrationBuilder.DropIndex(
                name: "IX_SsoUser_OrganizationId_ExternalId",
                table: "SsoUser");

            migrationBuilder.DropIndex(
                name: "IX_SsoUser_OrganizationId_UserId",
                table: "SsoUser");

            migrationBuilder.DropIndex(
                name: "IX_Send_DeletionDate",
                table: "Send");

            migrationBuilder.DropIndex(
                name: "IX_Send_UserId_OrganizationId",
                table: "Send");

            migrationBuilder.DropIndex(
                name: "IX_Policy_OrganizationId_Type",
                table: "Policy");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationUser_UserId_OrganizationId_Status",
                table: "OrganizationUser");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationSponsorship_SponsoringOrganizationUserId",
                table: "OrganizationSponsorship");

            migrationBuilder.DropIndex(
                name: "IX_Organization_Id_Enabled",
                table: "Organization");

            migrationBuilder.DropIndex(
                name: "IX_Grant_ExpirationDate",
                table: "Grant");

            migrationBuilder.DropIndex(
                name: "IX_Grant_SubjectId_ClientId_Type",
                table: "Grant");

            migrationBuilder.DropIndex(
                name: "IX_Grant_SubjectId_SessionId_Type",
                table: "Grant");

            migrationBuilder.DropIndex(
                name: "IX_Event_Date_OrganizationId_ActingUserId_CipherId",
                table: "Event");

            migrationBuilder.DropIndex(
                name: "IX_Device_Identifier",
                table: "Device");

            migrationBuilder.DropIndex(
                name: "IX_Device_UserId_Identifier",
                table: "Device");

            migrationBuilder.DropIndex(
                name: "IX_Cipher_DeletedDate",
                table: "Cipher");

            migrationBuilder.DropIndex(
                name: "IX_Cipher_UserId_OrganizationId",
                table: "Cipher");
        }
    }
}
