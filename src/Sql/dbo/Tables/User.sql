CREATE TABLE [dbo].[User] (
    [Id]                              UNIQUEIDENTIFIER NOT NULL,
    [Name]                            NVARCHAR (50)    NULL,
    [Email]                           NVARCHAR (50)    NOT NULL,
    [EmailVerified]                   BIT              NOT NULL,
    [MasterPassword]                  NVARCHAR (300)   NOT NULL,
    [MasterPasswordHint]              NVARCHAR (50)    NULL,
    [Culture]                         NVARCHAR (10)    NOT NULL,
    [SecurityStamp]                   NVARCHAR (50)    NOT NULL,
    [TwoFactorProviders]              NVARCHAR (MAX)   NULL,
    [TwoFactorRecoveryCode]           NVARCHAR (32)    NULL,
    [EquivalentDomains]               NVARCHAR (MAX)   NULL,
    [ExcludedGlobalEquivalentDomains] NVARCHAR (MAX)   NULL,
    [AccountRevisionDate]             DATETIME2 (7)    NOT NULL,
    [Key]                             VARCHAR (MAX)    NULL,
    [PublicKey]                       VARCHAR (MAX)    NULL,
    [PrivateKey]                      VARCHAR (MAX)    NULL,
    [Premium]                         BIT              NOT NULL,
    [Storage]                         BIGINT           NULL,
    [MaxStorageGb]                    SMALLINT         NULL,
    [Gateway]                         TINYINT          NULL,
    [GatewayCustomerId]               VARCHAR (50)     NULL,
    [GatewaySubscriptionId]           VARCHAR (50)     NULL,
    [CreationDate]                    DATETIME2 (7)    NOT NULL,
    [RevisionDate]                    DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email]
    ON [dbo].[User]([Email] ASC);

