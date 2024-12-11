﻿using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Bit.PostgresMigrations.Migrations;

public partial class Init : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder
            .AlterDatabase()
            .Annotation(
                "Npgsql:CollationDefinition:postgresIndetermanisticCollation",
                "en-u-ks-primary,en-u-ks-primary,icu,False"
            );

        migrationBuilder.CreateTable(
            name: "Event",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: true),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                DeviceType = table.Column<byte>(type: "smallint", nullable: true),
                IpAddress = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                ActingUserId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Event", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Grant",
            columns: table => new
            {
                Key = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                Type = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                SubjectId = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                SessionId = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                ClientId = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                Description = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                ExpirationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                ConsumedDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                Data = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Grant", x => x.Key);
            }
        );

        migrationBuilder.CreateTable(
            name: "Installation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true
                ),
                Key = table.Column<string>(
                    type: "character varying(150)",
                    maxLength: 150,
                    nullable: true
                ),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Installation", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Organization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Identifier = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true,
                    collation: "postgresIndetermanisticCollation"
                ),
                Name = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                BusinessName = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                BusinessAddress1 = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                BusinessAddress2 = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                BusinessAddress3 = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                BusinessCountry = table.Column<string>(
                    type: "character varying(2)",
                    maxLength: 2,
                    nullable: true
                ),
                BusinessTaxNumber = table.Column<string>(
                    type: "character varying(30)",
                    maxLength: 30,
                    nullable: true
                ),
                BillingEmail = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true
                ),
                Plan = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                Seats = table.Column<int>(type: "integer", nullable: true),
                MaxCollections = table.Column<short>(type: "smallint", nullable: true),
                UsePolicies = table.Column<bool>(type: "boolean", nullable: false),
                UseSso = table.Column<bool>(type: "boolean", nullable: false),
                UseGroups = table.Column<bool>(type: "boolean", nullable: false),
                UseDirectory = table.Column<bool>(type: "boolean", nullable: false),
                UseEvents = table.Column<bool>(type: "boolean", nullable: false),
                UseTotp = table.Column<bool>(type: "boolean", nullable: false),
                Use2fa = table.Column<bool>(type: "boolean", nullable: false),
                UseApi = table.Column<bool>(type: "boolean", nullable: false),
                UseResetPassword = table.Column<bool>(type: "boolean", nullable: false),
                SelfHost = table.Column<bool>(type: "boolean", nullable: false),
                UsersGetPremium = table.Column<bool>(type: "boolean", nullable: false),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                GatewaySubscriptionId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                ReferenceData = table.Column<string>(type: "text", nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                LicenseKey = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                ApiKey = table.Column<string>(
                    type: "character varying(30)",
                    maxLength: 30,
                    nullable: true
                ),
                PublicKey = table.Column<string>(type: "text", nullable: true),
                PrivateKey = table.Column<string>(type: "text", nullable: true),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                ExpirationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organization", x => x.Id);
            }
        );

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
                Status = table.Column<byte>(type: "smallint", nullable: false),
                UseEvents = table.Column<bool>(type: "boolean", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Provider", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "TaxRate",
            columns: table => new
            {
                Id = table.Column<string>(
                    type: "character varying(40)",
                    maxLength: 40,
                    nullable: false
                ),
                Country = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                State = table.Column<string>(
                    type: "character varying(2)",
                    maxLength: 2,
                    nullable: true
                ),
                PostalCode = table.Column<string>(
                    type: "character varying(10)",
                    maxLength: 10,
                    nullable: true
                ),
                Rate = table.Column<decimal>(type: "numeric", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "User",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                Email = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false,
                    collation: "postgresIndetermanisticCollation"
                ),
                EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                MasterPassword = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                MasterPasswordHint = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                Culture = table.Column<string>(
                    type: "character varying(10)",
                    maxLength: 10,
                    nullable: true
                ),
                SecurityStamp = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                TwoFactorProviders = table.Column<string>(type: "text", nullable: true),
                TwoFactorRecoveryCode = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: true
                ),
                EquivalentDomains = table.Column<string>(type: "text", nullable: true),
                ExcludedGlobalEquivalentDomains = table.Column<string>(
                    type: "text",
                    nullable: true
                ),
                AccountRevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                Key = table.Column<string>(type: "text", nullable: true),
                PublicKey = table.Column<string>(type: "text", nullable: true),
                PrivateKey = table.Column<string>(type: "text", nullable: true),
                Premium = table.Column<bool>(type: "boolean", nullable: false),
                PremiumExpirationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                RenewalReminderDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                Storage = table.Column<long>(type: "bigint", nullable: true),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                GatewaySubscriptionId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                ReferenceData = table.Column<string>(type: "text", nullable: true),
                LicenseKey = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                ApiKey = table.Column<string>(
                    type: "character varying(30)",
                    maxLength: 30,
                    nullable: false
                ),
                Kdf = table.Column<byte>(type: "smallint", nullable: false),
                KdfIterations = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_User", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Collection",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                ExternalId = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Collection", x => x.Id);
                table.ForeignKey(
                    name: "FK_Collection_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Group",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                AccessAll = table.Column<bool>(type: "boolean", nullable: false),
                ExternalId = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Group", x => x.Id);
                table.ForeignKey(
                    name: "FK_Group_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Policy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Policy", x => x.Id);
                table.ForeignKey(
                    name: "FK_Policy_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SsoConfig",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Data = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SsoConfig", x => x.Id);
                table.ForeignKey(
                    name: "FK_SsoConfig_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "ProviderOrganization",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "text", nullable: true),
                Settings = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderOrganization", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderOrganization_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_ProviderOrganization_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

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
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                DeletedDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                Reprompt = table.Column<byte>(type: "smallint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cipher", x => x.Id);
                table.ForeignKey(
                    name: "FK_Cipher_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_Cipher_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Device",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Identifier = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                PushToken = table.Column<string>(
                    type: "character varying(255)",
                    maxLength: 255,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Device", x => x.Id);
                table.ForeignKey(
                    name: "FK_Device_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "EmergencyAccess",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GrantorId = table.Column<Guid>(type: "uuid", nullable: false),
                GranteeId = table.Column<Guid>(type: "uuid", nullable: true),
                Email = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true
                ),
                KeyEncrypted = table.Column<string>(type: "text", nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                WaitTimeDays = table.Column<int>(type: "integer", nullable: false),
                RecoveryInitiatedDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                LastNotificationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmergencyAccess", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmergencyAccess_User_GranteeId",
                    column: x => x.GranteeId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_EmergencyAccess_User_GrantorId",
                    column: x => x.GrantorId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Folder",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Folder", x => x.Id);
                table.ForeignKey(
                    name: "FK_Folder_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "OrganizationUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Email = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true
                ),
                Key = table.Column<string>(type: "text", nullable: true),
                ResetPasswordKey = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                AccessAll = table.Column<bool>(type: "boolean", nullable: false),
                ExternalId = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                Permissions = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationUser_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_OrganizationUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

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
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderUser_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_ProviderUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

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
                Password = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                MaxAccessCount = table.Column<int>(type: "integer", nullable: true),
                AccessCount = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                ExpirationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: true
                ),
                DeletionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                Disabled = table.Column<bool>(type: "boolean", nullable: false),
                HideEmail = table.Column<bool>(type: "boolean", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Send", x => x.Id);
                table.ForeignKey(
                    name: "FK_Send_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_Send_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SsoUser",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                ExternalId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true,
                    collation: "postgresIndetermanisticCollation"
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SsoUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_SsoUser_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_SsoUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

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
                Details = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                PaymentMethodType = table.Column<byte>(type: "smallint", nullable: true),
                Gateway = table.Column<byte>(type: "smallint", nullable: true),
                GatewayId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transaction", x => x.Id);
                table.ForeignKey(
                    name: "FK_Transaction_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
                table.ForeignKey(
                    name: "FK_Transaction_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "U2f",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                KeyHandle = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                Challenge = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                AppId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                Version = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_U2f", x => x.Id);
                table.ForeignKey(
                    name: "FK_U2f_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "CollectionGroups",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionGroups", x => new { x.CollectionId, x.GroupId });
                table.ForeignKey(
                    name: "FK_CollectionGroups_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_CollectionGroups_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "CollectionCipher",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CipherId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionCipher", x => new { x.CollectionId, x.CipherId });
                table.ForeignKey(
                    name: "FK_CollectionCipher_Cipher_CipherId",
                    column: x => x.CipherId,
                    principalTable: "Cipher",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_CollectionCipher_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "CollectionUsers",
            columns: table => new
            {
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                HidePasswords = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_CollectionUsers",
                    x => new { x.CollectionId, x.OrganizationUserId }
                );
                table.ForeignKey(
                    name: "FK_CollectionUsers_Collection_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collection",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_CollectionUsers_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_CollectionUsers_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "GroupUser",
            columns: table => new
            {
                GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GroupUser", x => new { x.GroupId, x.OrganizationUserId });
                table.ForeignKey(
                    name: "FK_GroupUser_Group_GroupId",
                    column: x => x.GroupId,
                    principalTable: "Group",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_GroupUser_OrganizationUser_OrganizationUserId",
                    column: x => x.OrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_GroupUser_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "ProviderOrganizationProviderUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Permissions = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
                RevisionDate = table.Column<DateTime>(
                    type: "timestamp without time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderOrganizationProviderUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderOrganizationProviderUser_ProviderOrganization_Provi~",
                    column: x => x.ProviderOrganizationId,
                    principalTable: "ProviderOrganization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_ProviderOrganizationProviderUser_ProviderUser_ProviderUserId",
                    column: x => x.ProviderUserId,
                    principalTable: "ProviderUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_OrganizationId",
            table: "Cipher",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(name: "IX_Cipher_UserId", table: "Cipher", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Collection_OrganizationId",
            table: "Collection",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CollectionCipher_CipherId",
            table: "CollectionCipher",
            column: "CipherId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CollectionGroups_GroupId",
            table: "CollectionGroups",
            column: "GroupId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CollectionUsers_OrganizationUserId",
            table: "CollectionUsers",
            column: "OrganizationUserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CollectionUsers_UserId",
            table: "CollectionUsers",
            column: "UserId"
        );

        migrationBuilder.CreateIndex(name: "IX_Device_UserId", table: "Device", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_EmergencyAccess_GranteeId",
            table: "EmergencyAccess",
            column: "GranteeId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_EmergencyAccess_GrantorId",
            table: "EmergencyAccess",
            column: "GrantorId"
        );

        migrationBuilder.CreateIndex(name: "IX_Folder_UserId", table: "Folder", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Group_OrganizationId",
            table: "Group",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_GroupUser_OrganizationUserId",
            table: "GroupUser",
            column: "OrganizationUserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_GroupUser_UserId",
            table: "GroupUser",
            column: "UserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_OrganizationId",
            table: "OrganizationUser",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_UserId",
            table: "OrganizationUser",
            column: "UserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Policy_OrganizationId",
            table: "Policy",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganization_OrganizationId",
            table: "ProviderOrganization",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganization_ProviderId",
            table: "ProviderOrganization",
            column: "ProviderId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganizationProviderUser_ProviderOrganizationId",
            table: "ProviderOrganizationProviderUser",
            column: "ProviderOrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderOrganizationProviderUser_ProviderUserId",
            table: "ProviderOrganizationProviderUser",
            column: "ProviderUserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderUser_ProviderId",
            table: "ProviderUser",
            column: "ProviderId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ProviderUser_UserId",
            table: "ProviderUser",
            column: "UserId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Send_OrganizationId",
            table: "Send",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(name: "IX_Send_UserId", table: "Send", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_SsoConfig_OrganizationId",
            table: "SsoConfig",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SsoUser_OrganizationId",
            table: "SsoUser",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(name: "IX_SsoUser_UserId", table: "SsoUser", column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_OrganizationId",
            table: "Transaction",
            column: "OrganizationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_UserId",
            table: "Transaction",
            column: "UserId"
        );

        migrationBuilder.CreateIndex(name: "IX_U2f_UserId", table: "U2f", column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CollectionCipher");

        migrationBuilder.DropTable(name: "CollectionGroups");

        migrationBuilder.DropTable(name: "CollectionUsers");

        migrationBuilder.DropTable(name: "Device");

        migrationBuilder.DropTable(name: "EmergencyAccess");

        migrationBuilder.DropTable(name: "Event");

        migrationBuilder.DropTable(name: "Folder");

        migrationBuilder.DropTable(name: "Grant");

        migrationBuilder.DropTable(name: "GroupUser");

        migrationBuilder.DropTable(name: "Installation");

        migrationBuilder.DropTable(name: "Policy");

        migrationBuilder.DropTable(name: "ProviderOrganizationProviderUser");

        migrationBuilder.DropTable(name: "Send");

        migrationBuilder.DropTable(name: "SsoConfig");

        migrationBuilder.DropTable(name: "SsoUser");

        migrationBuilder.DropTable(name: "TaxRate");

        migrationBuilder.DropTable(name: "Transaction");

        migrationBuilder.DropTable(name: "U2f");

        migrationBuilder.DropTable(name: "Cipher");

        migrationBuilder.DropTable(name: "Collection");

        migrationBuilder.DropTable(name: "Group");

        migrationBuilder.DropTable(name: "OrganizationUser");

        migrationBuilder.DropTable(name: "ProviderOrganization");

        migrationBuilder.DropTable(name: "ProviderUser");

        migrationBuilder.DropTable(name: "Organization");

        migrationBuilder.DropTable(name: "Provider");

        migrationBuilder.DropTable(name: "User");
    }
}
