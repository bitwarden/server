CREATE TABLE [dbo].[User] (
    [Id]                               UNIQUEIDENTIFIER NOT NULL,
    [Name]                             NVARCHAR (50)    NULL,
    [Email]                            NVARCHAR (256)   NOT NULL,
    [EmailVerified]                    BIT              NOT NULL,
    [MasterPassword]                   NVARCHAR (300)   NULL,
    [MasterPasswordHint]               NVARCHAR (50)    NULL,
    [Culture]                          NVARCHAR (10)    NOT NULL,
    [SecurityStamp]                    NVARCHAR (50)    NOT NULL,
    [TwoFactorProviders]               NVARCHAR (MAX)   NULL,
    [TwoFactorRecoveryCode]            NVARCHAR (32)    NULL,
    [EquivalentDomains]                NVARCHAR (MAX)   NULL,
    [ExcludedGlobalEquivalentDomains]  NVARCHAR (MAX)   NULL,
    [AccountRevisionDate]              DATETIME2 (7)    NOT NULL,
    [Key]                              VARCHAR (MAX)    NULL,
    [PublicKey]                        VARCHAR (MAX)    NULL,
    [PrivateKey]                       VARCHAR (MAX)    NULL,
    [Premium]                          BIT              NOT NULL,
    [PremiumExpirationDate]            DATETIME2 (7)    NULL,
    [RenewalReminderDate]              DATETIME2 (7)    NULL,
    [Storage]                          BIGINT           NULL,
    [MaxStorageGb]                     SMALLINT         NULL,
    [Gateway]                          TINYINT          NULL,
    [GatewayCustomerId]                VARCHAR (50)     NULL,
    [GatewaySubscriptionId]            VARCHAR (50)     NULL,
    [ReferenceData]                    NVARCHAR (MAX)   NULL,
    [LicenseKey]                       VARCHAR (100)    NULL,
    [Kdf]                              TINYINT          NOT NULL,
    [KdfIterations]                    INT              NOT NULL,
    [CreationDate]                     DATETIME2 (7)    NOT NULL,
    [RevisionDate]                     DATETIME2 (7)    NOT NULL,
    [ApiKey]                           VARCHAR (30)     NOT NULL,
    [ForcePasswordReset]               BIT              NOT NULL,
    [UsesKeyConnector]                 BIT              NOT NULL,
    [FailedLoginCount]                 INT              CONSTRAINT [D_User_FailedLoginCount] DEFAULT ((0)) NOT NULL,
    [LastFailedLoginDate]              DATETIME2 (7)    NULL,
    [UnknownDeviceVerificationEnabled] BIT              CONSTRAINT [D_User_UnknownDeviceVerificationEnabled] DEFAULT ((1)) NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email]
    ON [dbo].[User]([Email] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_User_Premium_PremiumExpirationDate_RenewalReminderDate]
    ON [dbo].[User]([Premium] ASC, [PremiumExpirationDate] ASC, [RenewalReminderDate] ASC);

