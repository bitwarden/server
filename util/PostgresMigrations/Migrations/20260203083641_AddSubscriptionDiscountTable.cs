using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StripeCouponId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                StripeProductIds = table.Column<string>(type: "text", nullable: true),
                PercentOff = table.Column<decimal>(type: "numeric", nullable: true),
                AmountOff = table.Column<long>(type: "bigint", nullable: true),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                Duration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                DurationInMonths = table.Column<int>(type: "integer", nullable: true),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AudienceType = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
