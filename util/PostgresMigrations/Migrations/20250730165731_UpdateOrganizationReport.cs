﻿using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateOrganizationReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:postgresIndetermanisticCollation", "en-u-ks-primary,en-u-ks-primary,icu,False");

        migrationBuilder.CreateTable(
            name: "Cache",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(449)", maxLength: 449, nullable: false),
                Value = table.Column<byte[]>(type: "bytea", nullable: false),
                ExpiresAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SlidingExpirationInSeconds = table.Column<long>(type: "bigint", nullable: true),
                AbsoluteExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cache", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ClientOrganizationMigrationRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                Seats = table.Column<int>(type: "integer", nullable: false),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                GatewaySubscriptionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                MaxAutoscaleSeats = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientOrganizationMigrationRecord", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Event",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                InstallationId = table.Column<Guid>(type: "uuid", nullable: true),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: true),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ProviderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ProviderOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                DeviceType = table.Column<byte>(type: "smallint", nullable: true),
                IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ActingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                SystemUser = table.Column<byte>(type: "smallint", nullable: true),
                DomainName = table.Column<string>(type: "text", nullable: true),
                SecretId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Event", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Grant",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SubjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ConsumedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Data = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Grant", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Installation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Installation", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Organization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "postgresIndetermanisticCollation"),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                BusinessName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                BusinessAddress1 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                BusinessAddress2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                BusinessAddress3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                BusinessCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                BusinessTaxNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                BillingEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                Seats = table.Column<int>(type: "integer", nullable: true),
                MaxCollections = table.Column<short>(type: "smallint", nullable: true),
                UsePolicies = table.Column<bool>(type: "boolean", nullable: false),
                UseSso = table.Column<bool>(type: "boolean", nullable: false),
                UseKeyConnector = table.Column<bool>(type: "boolean", nullable: false),
                UseScim = table.Column<bool>(type: "boolean", nullable: false),
                UseGroups = table.Column<bool>(type: "boolean", nullable: false),
                UseDirectory = table.Column<bool>(type: "boolean", nullable: false),
                UseEvents = table.Column<bool>(type: "boolean", nullable: false),
                UseTotp = table.Column<bool>(type: "boolean", nullable: false),
                Use2fa = table.Column<bool>(type: "boolean", nullable: false),
                UseApi = table.Column<bool>(type: "boolean", nullable: false),
                UseResetPassword = table.Column<bool>(type: "boolean", nullable: false),
                UseSecretsManager = table.Column<bool>(type: "boolean", nullable: false),
                SelfHost = table.Column<bool>(type: "boolean", nullable: false),
                UsersGetPremium = table.Column<bool>(type: "boolean", nullable: false),
                UseCustomPermissions = table.Column<bool>(type: "boolean", nullable: false),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                GatewaySubscriptionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ReferenceData = table.Column<string>(type: "text", nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                LicenseKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PublicKey = table.Column<string>(type: "text", nullable: true),
                PrivateKey = table.Column<string>(type: "text", nullable: true),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                MaxAutoscaleSeats = table.Column<int>(type: "integer", nullable: true),
                OwnersNotifiedOfAutoscaling = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                UsePasswordManager = table.Column<bool>(type: "boolean", nullable: false),
                SmSeats = table.Column<int>(type: "integer", nullable: true),
                SmServiceAccounts = table.Column<int>(type: "integer", nullable: true),
                MaxAutoscaleSmSeats = table.Column<int>(type: "integer", nullable: true),
                MaxAutoscaleSmServiceAccounts = table.Column<int>(type: "integer", nullable: true),
                LimitCollectionCreation = table.Column<bool>(type: "boolean", nullable: false),
                LimitCollectionDeletion = table.Column<bool>(type: "boolean", nullable: false),
                AllowAdminAccessToAllCollectionItems = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                LimitItemDeletion = table.Column<bool>(type: "boolean", nullable: false),
                UseRiskInsights = table.Column<bool>(type: "boolean", nullable: false),
                UseOrganizationDomains = table.Column<bool>(type: "boolean", nullable: false),
                UseAdminSponsoredFamilies = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organization", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationMemberBaseDetails",
            columns: table => new
            {
                UserGuid = table.Column<Guid>(type: "uuid", nullable: true),
                UserName = table.Column<string>(type: "text", nullable: true),
                Email = table.Column<string>(type: "text", nullable: true),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                UsesKeyConnector = table.Column<bool>(type: "boolean", nullable: false),
                ResetPasswordKey = table.Column<string>(type: "text", nullable: true),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupName = table.Column<string>(type: "text", nullable: true),
                CollectionName = table.Column<string>(type: "text", nullable: true),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: true),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: true),
                Manage = table.Column<bool>(type: "boolean", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
            });

        migrationBuilder.CreateTable(
            name: "Provider",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                BusinessName = table.Column<string>(type: "text", nullable: true),
                BusinessAddress1 = table.Column<string>(type: "text", nullable: true),
                BusinessAddress2 = table.Column<string>(type: "text", nullable: true),
                BusinessAddress3 = table.Column<string>(type: "text", nullable: true),
                BusinessCountry = table.Column<string>(type: "text", nullable: true),
                BusinessTaxNumber = table.Column<string>(type: "text", nullable: true),
                BillingEmail = table.Column<string>(type: "text", nullable: true),
                BillingPhone = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                UseEvents = table.Column<bool>(type: "boolean", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "text", nullable: true),
                GatewaySubscriptionId = table.Column<string>(type: "text", nullable: true),
                DiscountId = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Provider", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TaxRate",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Rate = table.Column<decimal>(type: "numeric", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "User",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, collation: "postgresIndetermanisticCollation"),
                EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                MasterPassword = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                MasterPasswordHint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                SecurityStamp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                TwoFactorRecoveryCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                EquivalentDomains = table.Column<string>(type: "text", nullable: true),
                ExcludedGlobalEquivalentDomains = table.Column<string>(type: "text", nullable: true),
                AccountRevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Key = table.Column<string>(type: "text", nullable: true),
                PublicKey = table.Column<string>(type: "text", nullable: true),
                PrivateKey = table.Column<string>(type: "text", nullable: true),
                Premium = table.Column<bool>(type: "boolean", nullable: false),
                PremiumExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RenewalReminderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                GatewaySubscriptionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ReferenceData = table.Column<string>(type: "text", nullable: true),
                LicenseKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ApiKey = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Kdf = table.Column<byte>(type: "smallint", nullable: false),
                KdfIterations = table.Column<int>(type: "integer", nullable: false),
                KdfMemory = table.Column<int>(type: "integer", nullable: true),
                KdfParallelism = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ForcePasswordReset = table.Column<bool>(type: "boolean", nullable: false),
                UsesKeyConnector = table.Column<bool>(type: "boolean", nullable: false),
                FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                LastFailedLoginDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AvatarColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                LastPasswordChangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastKdfChangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastKeyRotationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastEmailChangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                VerifyDevices = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Collection",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                DefaultUserCollectionEmail = table.Column<string>(type: "text", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "Group",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                ApiKey = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Applications = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ContentEncryptionKey = table.Column<string>(type: "text", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationConnection",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                Config = table.Column<string>(type: "text", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationDomain",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Txt = table.Column<string>(type: "text", nullable: false),
                DomainName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                VerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextRunDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastCheckedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                JobRunCount = table.Column<int>(type: "integer", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationInstallation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationIntegration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                Configuration = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SummaryData = table.Column<string>(type: "text", nullable: false),
                ReportData = table.Column<string>(type: "text", nullable: false),
                ApplicationData = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ContentEncryptionKey = table.Column<string>(type: "text", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationSponsorship",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SponsoringOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                SponsoringOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                SponsoredOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                FriendlyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                OfferedToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                PlanSponsorshipType = table.Column<byte>(type: "smallint", nullable: true),
                LastSyncDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ToDelete = table.Column<bool>(type: "boolean", nullable: false),
                IsAdminInitiated = table.Column<bool>(type: "boolean", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true)
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
                    name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
                    column: x => x.SponsoringOrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PasswordHealthReportApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Uri = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Policy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Project",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "Secret",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "text", nullable: true),
                Value = table.Column<string>(type: "text", nullable: true),
                Note = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "ServiceAccount",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "SsoConfig",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "ProviderInvoiceItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                ClientName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PlanName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                AssignedSeats = table.Column<int>(type: "integer", nullable: false),
                UsedSeats = table.Column<int>(type: "integer", nullable: false),
                Total = table.Column<decimal>(type: "numeric", nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "ProviderOrganization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "text", nullable: true),
                Settings = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "ProviderPlan",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                SeatMinimum = table.Column<int>(type: "integer", nullable: true),
                PurchasedSeats = table.Column<int>(type: "integer", nullable: true),
                AllocatedSeats = table.Column<int>(type: "integer", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "Cipher",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                Favorites = table.Column<string>(type: "text", nullable: true),
                Folders = table.Column<string>(type: "text", nullable: true),
                Attachments = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Reprompt = table.Column<byte>(type: "smallint", nullable: true),
                Key = table.Column<string>(type: "text", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "Device",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PushToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EncryptedUserKey = table.Column<string>(type: "text", nullable: true),
                EncryptedPublicKey = table.Column<string>(type: "text", nullable: true),
                EncryptedPrivateKey = table.Column<string>(type: "text", nullable: true),
                Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
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
            });

        migrationBuilder.CreateTable(
            name: "EmergencyAccess",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GrantorId = table.Column<Guid>(type: "uuid", nullable: false),
                GranteeId = table.Column<Guid>(type: "uuid", nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                KeyEncrypted = table.Column<string>(type: "text", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                WaitTimeDays = table.Column<int>(type: "integer", nullable: false),
                RecoveryInitiatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastNotificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Folder",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Key = table.Column<string>(type: "text", nullable: true),
                ResetPasswordKey = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<short>(type: "smallint", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Permissions = table.Column<string>(type: "text", nullable: true),
                AccessSecretsManager = table.Column<bool>(type: "boolean", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "ProviderUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Email = table.Column<string>(type: "text", nullable: true),
                Key = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Permissions = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Send",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                Key = table.Column<string>(type: "text", nullable: true),
                Password = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Emails = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                MaxAccessCount = table.Column<int>(type: "integer", nullable: true),
                AccessCount = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Disabled = table.Column<bool>(type: "boolean", nullable: false),
                HideEmail = table.Column<bool>(type: "boolean", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "SsoUser",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "postgresIndetermanisticCollation"),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Transaction",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Amount = table.Column<decimal>(type: "numeric", nullable: false),
                Refunded = table.Column<bool>(type: "boolean", nullable: true),
                RefundedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                Details = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PaymentMethodType = table.Column<byte>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "WebAuthnCredential",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PublicKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CredentialId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Counter = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                AaGuid = table.Column<Guid>(type: "uuid", nullable: false),
                EncryptedUserKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EncryptedPrivateKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EncryptedPublicKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                SupportsPrf = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "CollectionGroups",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: false),
                Manage = table.Column<bool>(type: "boolean", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "OrganizationIntegrationConfiguration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<int>(type: "integer", nullable: false),
                Configuration = table.Column<string>(type: "text", nullable: true),
                Template = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Filters = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegrationConfiguration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegrationConfiguration_OrganizationIntegratio~",
                    column: x => x.OrganizationIntegrationId,
                    principalTable: "OrganizationIntegration",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectSecret",
            columns: table => new
            {
                ProjectsId = table.Column<Guid>(type: "uuid", nullable: false),
                SecretsId = table.Column<Guid>(type: "uuid", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "ApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ClientSecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Scope = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                EncryptedPayload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Key = table.Column<string>(type: "text", nullable: false),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKey", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKey_ServiceAccount_ServiceAccountId",
                    column: x => x.ServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "CollectionCipher",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CipherId = table.Column<Guid>(type: "uuid", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "SecurityTask",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CipherId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "AuthRequest",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                RequestDeviceIdentifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                RequestDeviceType = table.Column<byte>(type: "smallint", nullable: false),
                RequestIpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                RequestCountryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ResponseDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                AccessCode = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                PublicKey = table.Column<string>(type: "text", nullable: true),
                Key = table.Column<string>(type: "text", nullable: true),
                MasterPasswordHash = table.Column<string>(type: "text", nullable: true),
                Approved = table.Column<bool>(type: "boolean", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResponseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AuthenticationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "AccessPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                GrantedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                GrantedSecretId = table.Column<Guid>(type: "uuid", nullable: true),
                GrantedServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Read = table.Column<bool>(type: "boolean", nullable: false),
                Write = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Discriminator = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "CollectionUsers",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: false),
                Manage = table.Column<bool>(type: "boolean", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "GroupUser",
            columns: table => new
            {
                GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "Notification",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Priority = table.Column<byte>(type: "smallint", nullable: false),
                Global = table.Column<bool>(type: "boolean", nullable: false),
                ClientType = table.Column<byte>(type: "smallint", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Body = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TaskId = table.Column<Guid>(type: "uuid", nullable: true)
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
            });

        migrationBuilder.CreateTable(
            name: "NotificationStatus",
            columns: table => new
            {
                NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
            });

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
            name: "IX_Notification_ClientType_Global_UserId_OrganizationId_Priori~",
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
            columns: new[] { "Id", "Enabled" })
            .Annotation("Npgsql:IndexInclude", new[] { "UseTotp" });

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
            name: "IX_OrganizationIntegrationConfiguration_OrganizationIntegratio~",
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
            unique: true)
            .Annotation("Npgsql:IndexInclude", new[] { "UserId" });

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
