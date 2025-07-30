using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateOrganizationReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Cache",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(449)", maxLength: 449, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Value = table.Column<byte[]>(type: "longblob", nullable: false),
                ExpiresAtTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                SlidingExpirationInSeconds = table.Column<long>(type: "bigint", nullable: true),
                AbsoluteExpiration = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cache", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ClientOrganizationMigrationRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Seats = table.Column<int>(type: "int", nullable: false),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                GatewaySubscriptionId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                MaxAutoscaleSeats = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientOrganizationMigrationRecord", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Event",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                InstallationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                PolicyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ProviderUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ProviderOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                DeviceType = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ActingUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                SystemUser = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                DomainName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SecretId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ServiceAccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Event", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Grant",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SubjectId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SessionId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ClientId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ConsumedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Data = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Grant", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Installation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                LastActivityDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Installation", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Organization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Identifier = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress1 = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress2 = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress3 = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessCountry = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessTaxNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BillingEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Plan = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Seats = table.Column<int>(type: "int", nullable: true),
                MaxCollections = table.Column<short>(type: "smallint", nullable: true),
                UsePolicies = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseSso = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseKeyConnector = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseScim = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseGroups = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseDirectory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseEvents = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseTotp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Use2fa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseApi = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseResetPassword = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseSecretsManager = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SelfHost = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UsersGetPremium = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseCustomPermissions = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                GatewaySubscriptionId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReferenceData = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                LicenseKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PublicKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PrivateKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TwoFactorProviders = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                MaxAutoscaleSeats = table.Column<int>(type: "int", nullable: true),
                OwnersNotifiedOfAutoscaling = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                UsePasswordManager = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SmSeats = table.Column<int>(type: "int", nullable: true),
                SmServiceAccounts = table.Column<int>(type: "int", nullable: true),
                MaxAutoscaleSmSeats = table.Column<int>(type: "int", nullable: true),
                MaxAutoscaleSmServiceAccounts = table.Column<int>(type: "int", nullable: true),
                LimitCollectionCreation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                LimitCollectionDeletion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                AllowAdminAccessToAllCollectionItems = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                LimitItemDeletion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseRiskInsights = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseOrganizationDomains = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UseAdminSponsoredFamilies = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organization", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationMemberBaseDetails",
            columns: table => new
            {
                UserGuid = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                UserName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Email = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TwoFactorProviders = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UsesKeyConnector = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ResetPasswordKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GroupName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CollectionName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReadOnly = table.Column<bool>(type: "tinyint(1)", nullable: true),
                HidePasswords = table.Column<bool>(type: "tinyint(1)", nullable: true),
                Manage = table.Column<bool>(type: "tinyint(1)", nullable: true),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Provider",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessName = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress1 = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress2 = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessAddress3 = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessCountry = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BusinessTaxNumber = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BillingEmail = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BillingPhone = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                UseEvents = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Gateway = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                GatewaySubscriptionId = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DiscountId = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Provider", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "TaxRate",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Country = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                State = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PostalCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Rate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                Active = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "User",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EmailVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                MasterPassword = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                MasterPasswordHint = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Culture = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SecurityStamp = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TwoFactorProviders = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TwoFactorRecoveryCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EquivalentDomains = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExcludedGlobalEquivalentDomains = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                AccountRevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PublicKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PrivateKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Premium = table.Column<bool>(type: "tinyint(1)", nullable: false),
                PremiumExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                RenewalReminderDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                GatewaySubscriptionId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReferenceData = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                LicenseKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ApiKey = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Kdf = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                KdfIterations = table.Column<int>(type: "int", nullable: false),
                KdfMemory = table.Column<int>(type: "int", nullable: true),
                KdfParallelism = table.Column<int>(type: "int", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ForcePasswordReset = table.Column<bool>(type: "tinyint(1)", nullable: false),
                UsesKeyConnector = table.Column<bool>(type: "tinyint(1)", nullable: false),
                FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                LastFailedLoginDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                AvatarColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                LastPasswordChangeDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastKdfChangeDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastKeyRotationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastEmailChangeDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                VerifyDevices = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Collection",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExternalId = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                DefaultUserCollectionEmail = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Collection", x => x.Id);
                table.ForeignKey(
                    name: "FK_Collection_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Group",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExternalId = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Group", x => x.Id);
                table.ForeignKey(
                    name: "FK_Group_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                ApiKey = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationApiKey", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationApiKey_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Applications = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ContentEncryptionKey = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationApplication", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationApplication_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationConnection",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Config = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationConnection", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationConnection_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationDomain",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Txt = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DomainName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                VerifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                NextRunDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                LastCheckedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                JobRunCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationDomain", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationDomain_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationInstallation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                InstallationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationInstallation", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationInstallation_Installation_InstallationId",
                    column: x => x.InstallationId,
                    principalTable: "Installation",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrganizationInstallation_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationIntegration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<int>(type: "int", nullable: false),
                Configuration = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegration_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                SummaryData = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ReportData = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ApplicationData = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ContentEncryptionKey = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationReport", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationReport_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationSponsorship",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SponsoringOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                SponsoringOrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SponsoredOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                FriendlyName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                OfferedToEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PlanSponsorshipType = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                LastSyncDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ValidUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ToDelete = table.Column<bool>(type: "tinyint(1)", nullable: false),
                IsAdminInitiated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Notes = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationSponsorship", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationSponsorship_Organization_SponsoredOrganizationId",
                    column: x => x.SponsoredOrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
                    column: x => x.SponsoringOrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PasswordHealthReportApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Uri = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordHealthReportApplication", x => x.Id);
                table.ForeignKey(
                    name: "FK_PasswordHealthReportApplication_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Policy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Data = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Policy", x => x.Id);
                table.ForeignKey(
                    name: "FK_Policy_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Project",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Project", x => x.Id);
                table.ForeignKey(
                    name: "FK_Project_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Secret",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Value = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Note = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Secret", x => x.Id);
                table.ForeignKey(
                    name: "FK_Secret_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ServiceAccount",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceAccount", x => x.Id);
                table.ForeignKey(
                    name: "FK_ServiceAccount_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "SsoConfig",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Data = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SsoConfig", x => x.Id);
                table.ForeignKey(
                    name: "FK_SsoConfig_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ProviderInvoiceItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                InvoiceId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                InvoiceNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ClientName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PlanName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                AssignedSeats = table.Column<int>(type: "int", nullable: false),
                UsedSeats = table.Column<int>(type: "int", nullable: false),
                Total = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                Created = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderInvoiceItem", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderInvoiceItem_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ProviderOrganization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Settings = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderOrganization", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderOrganization_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProviderOrganization_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ProviderPlan",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                SeatMinimum = table.Column<int>(type: "int", nullable: true),
                PurchasedSeats = table.Column<int>(type: "int", nullable: true),
                AllocatedSeats = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderPlan", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderPlan_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Cipher",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Data = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Favorites = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Folders = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Attachments = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Reprompt = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cipher", x => x.Id);
                table.ForeignKey(
                    name: "FK_Cipher_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Cipher_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Device",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Identifier = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PushToken = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                EncryptedUserKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPublicKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPrivateKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Device", x => x.Id);
                table.ForeignKey(
                    name: "FK_Device_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "EmergencyAccess",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                GrantorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                GranteeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                KeyEncrypted = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                WaitTimeDays = table.Column<int>(type: "int", nullable: false),
                RecoveryInitiatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastNotificationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmergencyAccess", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmergencyAccess_User_GranteeId",
                    column: x => x.GranteeId,
                    principalTable: "User",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_EmergencyAccess_User_GrantorId",
                    column: x => x.GrantorId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Folder",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Folder", x => x.Id);
                table.ForeignKey(
                    name: "FK_Folder_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ResetPasswordKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<short>(type: "smallint", nullable: false),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                ExternalId = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Permissions = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                AccessSecretsManager = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationUser_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrganizationUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ProviderUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Email = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Permissions = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderUser_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProviderUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Send",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Data = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Password = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Emails = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                MaxAccessCount = table.Column<int>(type: "int", nullable: true),
                AccessCount = table.Column<int>(type: "int", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Disabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                HideEmail = table.Column<bool>(type: "tinyint(1)", nullable: true),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Send", x => x.Id);
                table.ForeignKey(
                    name: "FK_Send_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Send_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "SsoUser",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ExternalId = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SsoUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_SsoUser_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SsoUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Transaction",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                Refunded = table.Column<bool>(type: "tinyint(1)", nullable: true),
                RefundedAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                Details = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PaymentMethodType = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                Gateway = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                GatewayId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transaction", x => x.Id);
                table.ForeignKey(
                    name: "FK_Transaction_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Transaction_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Transaction_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "WebAuthnCredential",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PublicKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CredentialId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Counter = table.Column<int>(type: "int", nullable: false),
                Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                AaGuid = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                EncryptedUserKey = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPrivateKey = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPublicKey = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SupportsPrf = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebAuthnCredential", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebAuthnCredential_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "CollectionGroups",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                GroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ReadOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                HidePasswords = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Manage = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionGroups", x => new { x.CollectionId, x.GroupId });
                table.ForeignKey(
                    name: "FK_CollectionGroups_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CollectionGroups_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationIntegrationConfiguration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationIntegrationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                EventType = table.Column<int>(type: "int", nullable: false),
                Configuration = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Template = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Filters = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegrationConfiguration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegrationConfiguration_OrganizationIntegration~",
                    column: x => x.OrganizationIntegrationId,
                    principalTable: "OrganizationIntegration",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ProjectSecret",
            columns: table => new
            {
                ProjectsId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SecretsId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectSecret", x => new { x.ProjectsId, x.SecretsId });
                table.ForeignKey(
                    name: "FK_ProjectSecret_Project_ProjectsId",
                    column: x => x.ProjectsId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectSecret_Secret_SecretsId",
                    column: x => x.SecretsId,
                    principalTable: "Secret",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "ApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ServiceAccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ClientSecretHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Scope = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPayload = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpireAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKey", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKey_ServiceAccount_ServiceAccountId",
                    column: x => x.ServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "CollectionCipher",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionCipher", x => new { x.CollectionId, x.CipherId });
                table.ForeignKey(
                    name: "FK_CollectionCipher_Cipher_CipherId",
                    column: x => x.CipherId,
                    principalTable: "Cipher",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CollectionCipher_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "SecurityTask",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecurityTask", x => x.Id);
                table.ForeignKey(
                    name: "FK_SecurityTask_Cipher_CipherId",
                    column: x => x.CipherId,
                    principalTable: "Cipher",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SecurityTask_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "AuthRequest",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                RequestDeviceIdentifier = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequestDeviceType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                RequestIpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequestCountryName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ResponseDeviceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccessCode = table.Column<string>(type: "varchar(25)", maxLength: 25, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PublicKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                MasterPasswordHash = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Approved = table.Column<bool>(type: "tinyint(1)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ResponseDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                AuthenticationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthRequest", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuthRequest_Device_ResponseDeviceId",
                    column: x => x.ResponseDeviceId,
                    principalTable: "Device",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AuthRequest_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AuthRequest_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "AccessPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                GroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GrantedProjectId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GrantedSecretId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                GrantedServiceAccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ServiceAccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Read = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Write = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Discriminator = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessPolicy", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessPolicy_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AccessPolicy_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AccessPolicy_Project_GrantedProjectId",
                    column: x => x.GrantedProjectId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AccessPolicy_Secret_GrantedSecretId",
                    column: x => x.GrantedSecretId,
                    principalTable: "Secret",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "CollectionUsers",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ReadOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                HidePasswords = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Manage = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionUsers", x => new { x.CollectionId, x.OrganizationUserId });
                table.ForeignKey(
                    name: "FK_CollectionUsers_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CollectionUsers_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "GroupUser",
            columns: table => new
            {
                GroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GroupUser", x => new { x.GroupId, x.OrganizationUserId });
                table.ForeignKey(
                    name: "FK_GroupUser_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GroupUser_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "Notification",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Priority = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Global = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ClientType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Body = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                TaskId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notification", x => x.Id);
                table.ForeignKey(
                    name: "FK_Notification_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Notification_SecurityTask_TaskId",
                    column: x => x.TaskId,
                    principalTable: "SecurityTask",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Notification_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "NotificationStatus",
            columns: table => new
            {
                NotificationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ReadDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                DeletedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationStatus", x => new { x.UserId, x.NotificationId });
                table.ForeignKey(
                    name: "FK_NotificationStatus_Notification_NotificationId",
                    column: x => x.NotificationId,
                    principalTable: "Notification",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_NotificationStatus_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GrantedProjectId",
            table: "AccessPolicy",
            column: "GrantedProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GrantedSecretId",
            table: "AccessPolicy",
            column: "GrantedSecretId");

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

        migrationBuilder.CreateIndex(
            name: "IX_ApiKey_ServiceAccountId",
            table: "ApiKey",
            column: "ServiceAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthRequest_OrganizationId",
            table: "AuthRequest",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthRequest_ResponseDeviceId",
            table: "AuthRequest",
            column: "ResponseDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthRequest_UserId",
            table: "AuthRequest",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Cache_ExpiresAtTime",
            table: "Cache",
            column: "ExpiresAtTime");

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_OrganizationId",
            table: "Cipher",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_UserId",
            table: "Cipher",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ClientOrganizationMigrationRecord_ProviderId_OrganizationId",
            table: "ClientOrganizationMigrationRecord",
            columns: new[] { "ProviderId", "OrganizationId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Collection_OrganizationId",
            table: "Collection",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionCipher_CipherId",
            table: "CollectionCipher",
            column: "CipherId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionGroups_GroupId",
            table: "CollectionGroups",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionUsers_OrganizationUserId",
            table: "CollectionUsers",
            column: "OrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Device_Identifier",
            table: "Device",
            column: "Identifier");

        migrationBuilder.CreateIndex(
            name: "IX_Device_UserId",
            table: "Device",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Device_UserId_Identifier",
            table: "Device",
            columns: new[] { "UserId", "Identifier" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmergencyAccess_GranteeId",
            table: "EmergencyAccess",
            column: "GranteeId");

        migrationBuilder.CreateIndex(
            name: "IX_EmergencyAccess_GrantorId",
            table: "EmergencyAccess",
            column: "GrantorId");

        migrationBuilder.CreateIndex(
            name: "IX_Event_Date_OrganizationId_ActingUserId_CipherId",
            table: "Event",
            columns: new[] { "Date", "OrganizationId", "ActingUserId", "CipherId" });

        migrationBuilder.CreateIndex(
            name: "IX_Folder_UserId",
            table: "Folder",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Grant_ExpirationDate",
            table: "Grant",
            column: "ExpirationDate");

        migrationBuilder.CreateIndex(
            name: "IX_Grant_Key",
            table: "Grant",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Group_OrganizationId",
            table: "Group",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_GroupUser_OrganizationUserId",
            table: "GroupUser",
            column: "OrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_ClientType_Global_UserId_OrganizationId_Priorit~",
            table: "Notification",
            columns: new[] { "ClientType", "Global", "UserId", "OrganizationId", "Priority", "CreationDate" },
            descending: new[] { false, false, false, false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Notification_OrganizationId",
            table: "Notification",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_TaskId",
            table: "Notification",
            column: "TaskId");

        migrationBuilder.CreateIndex(
            name: "IX_Notification_UserId",
            table: "Notification",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationStatus_NotificationId",
            table: "NotificationStatus",
            column: "NotificationId");

        migrationBuilder.CreateIndex(
            name: "IX_Organization_Id_Enabled",
            table: "Organization",
            columns: new[] { "Id", "Enabled" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApiKey_OrganizationId",
            table: "OrganizationApiKey",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_Id",
            table: "OrganizationApplication",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_OrganizationId",
            table: "OrganizationApplication",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationConnection_OrganizationId",
            table: "OrganizationConnection",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationDomain_OrganizationId",
            table: "OrganizationDomain",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInstallation_InstallationId",
            table: "OrganizationInstallation",
            column: "InstallationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInstallation_OrganizationId",
            table: "OrganizationInstallation",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegration_OrganizationId",
            table: "OrganizationIntegration",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegration_OrganizationId_Type",
            table: "OrganizationIntegration",
            columns: new[] { "OrganizationId", "Type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegrationConfiguration_OrganizationIntegration~",
            table: "OrganizationIntegrationConfiguration",
            column: "OrganizationIntegrationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_Id",
            table: "OrganizationReport",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_OrganizationId",
            table: "OrganizationReport",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_SponsoredOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoredOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_SponsoringOrganizationUserId",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_OrganizationId",
            table: "OrganizationUser",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_UserId",
            table: "OrganizationUser",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordHealthReportApplication_Id",
            table: "PasswordHealthReportApplication",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordHealthReportApplication_OrganizationId",
            table: "PasswordHealthReportApplication",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Policy_OrganizationId",
            table: "Policy",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Policy_OrganizationId_Type",
            table: "Policy",
            columns: new[] { "OrganizationId", "Type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Project_DeletedDate",
            table: "Project",
            column: "DeletedDate");

        migrationBuilder.CreateIndex(
            name: "IX_Project_OrganizationId",
            table: "Project",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectSecret_SecretsId",
            table: "ProjectSecret",
            column: "SecretsId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_ProviderId",
            table: "ProviderInvoiceItem",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganization_OrganizationId",
            table: "ProviderOrganization",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganization_ProviderId",
            table: "ProviderOrganization",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderPlan_Id_PlanType",
            table: "ProviderPlan",
            columns: new[] { "Id", "PlanType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProviderPlan_ProviderId",
            table: "ProviderPlan",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderUser_ProviderId",
            table: "ProviderUser",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderUser_UserId",
            table: "ProviderUser",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Secret_DeletedDate",
            table: "Secret",
            column: "DeletedDate");

        migrationBuilder.CreateIndex(
            name: "IX_Secret_OrganizationId",
            table: "Secret",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityTask_CipherId",
            table: "SecurityTask",
            column: "CipherId");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityTask_OrganizationId",
            table: "SecurityTask",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Send_DeletionDate",
            table: "Send",
            column: "DeletionDate");

        migrationBuilder.CreateIndex(
            name: "IX_Send_OrganizationId",
            table: "Send",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Send_UserId",
            table: "Send",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Send_UserId_OrganizationId",
            table: "Send",
            columns: new[] { "UserId", "OrganizationId" });

        migrationBuilder.CreateIndex(
            name: "IX_ServiceAccount_OrganizationId",
            table: "ServiceAccount",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_SsoConfig_OrganizationId",
            table: "SsoConfig",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_SsoUser_OrganizationId",
            table: "SsoUser",
            column: "OrganizationId");

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
            name: "IX_SsoUser_UserId",
            table: "SsoUser",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_OrganizationId",
            table: "Transaction",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_ProviderId",
            table: "Transaction",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_UserId",
            table: "Transaction",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_UserId_OrganizationId_CreationDate",
            table: "Transaction",
            columns: new[] { "UserId", "OrganizationId", "CreationDate" });

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
            name: "IX_WebAuthnCredential_UserId",
            table: "WebAuthnCredential",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccessPolicy");

        migrationBuilder.DropTable(
            name: "ApiKey");

        migrationBuilder.DropTable(
            name: "AuthRequest");

        migrationBuilder.DropTable(
            name: "Cache");

        migrationBuilder.DropTable(
            name: "ClientOrganizationMigrationRecord");

        migrationBuilder.DropTable(
            name: "CollectionCipher");

        migrationBuilder.DropTable(
            name: "CollectionGroups");

        migrationBuilder.DropTable(
            name: "CollectionUsers");

        migrationBuilder.DropTable(
            name: "EmergencyAccess");

        migrationBuilder.DropTable(
            name: "Event");

        migrationBuilder.DropTable(
            name: "Folder");

        migrationBuilder.DropTable(
            name: "Grant");

        migrationBuilder.DropTable(
            name: "GroupUser");

        migrationBuilder.DropTable(
            name: "NotificationStatus");

        migrationBuilder.DropTable(
            name: "OrganizationApiKey");

        migrationBuilder.DropTable(
            name: "OrganizationApplication");

        migrationBuilder.DropTable(
            name: "OrganizationConnection");

        migrationBuilder.DropTable(
            name: "OrganizationDomain");

        migrationBuilder.DropTable(
            name: "OrganizationInstallation");

        migrationBuilder.DropTable(
            name: "OrganizationIntegrationConfiguration");

        migrationBuilder.DropTable(
            name: "OrganizationMemberBaseDetails");

        migrationBuilder.DropTable(
            name: "OrganizationReport");

        migrationBuilder.DropTable(
            name: "OrganizationSponsorship");

        migrationBuilder.DropTable(
            name: "PasswordHealthReportApplication");

        migrationBuilder.DropTable(
            name: "Policy");

        migrationBuilder.DropTable(
            name: "ProjectSecret");

        migrationBuilder.DropTable(
            name: "ProviderInvoiceItem");

        migrationBuilder.DropTable(
            name: "ProviderOrganization");

        migrationBuilder.DropTable(
            name: "ProviderPlan");

        migrationBuilder.DropTable(
            name: "ProviderUser");

        migrationBuilder.DropTable(
            name: "Send");

        migrationBuilder.DropTable(
            name: "SsoConfig");

        migrationBuilder.DropTable(
            name: "SsoUser");

        migrationBuilder.DropTable(
            name: "TaxRate");

        migrationBuilder.DropTable(
            name: "Transaction");

        migrationBuilder.DropTable(
            name: "WebAuthnCredential");

        migrationBuilder.DropTable(
            name: "ServiceAccount");

        migrationBuilder.DropTable(
            name: "Device");

        migrationBuilder.DropTable(
            name: "Collection");

        migrationBuilder.DropTable(
            name: "Group");

        migrationBuilder.DropTable(
            name: "OrganizationUser");

        migrationBuilder.DropTable(
            name: "Notification");

        migrationBuilder.DropTable(
            name: "Installation");

        migrationBuilder.DropTable(
            name: "OrganizationIntegration");

        migrationBuilder.DropTable(
            name: "Project");

        migrationBuilder.DropTable(
            name: "Secret");

        migrationBuilder.DropTable(
            name: "Provider");

        migrationBuilder.DropTable(
            name: "SecurityTask");

        migrationBuilder.DropTable(
            name: "Cipher");

        migrationBuilder.DropTable(
            name: "Organization");

        migrationBuilder.DropTable(
            name: "User");
    }
}
