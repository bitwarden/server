using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDiscountTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionDiscount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StripeCouponId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StripeProductIds = table.Column<string>(type: "TEXT", nullable: true),
                    PercentOff = table.Column<decimal>(type: "TEXT", nullable: true),
                    AmountOff = table.Column<long>(type: "INTEGER", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DurationInMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AudienceType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionDiscount", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscount_DateRange",
                table: "SubscriptionDiscount",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDiscount_StripeCouponId",
                table: "SubscriptionDiscount",
                column: "StripeCouponId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionDiscount");
        }
    }
}
