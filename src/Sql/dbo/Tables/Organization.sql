CREATE TABLE [dbo].[Organization] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [Identifier]                    NVARCHAR (50)    NULL,
    [Name]                          NVARCHAR (50)    NOT NULL,
    [BusinessName]                  NVARCHAR (50)    NULL,
    [BusinessAddress1]              NVARCHAR (50)    NULL,
    [BusinessAddress2]              NVARCHAR (50)    NULL,
    [BusinessAddress3]              NVARCHAR (50)    NULL,
    [BusinessCountry]               VARCHAR (2)      NULL,
    [BusinessTaxNumber]             NVARCHAR (30)    NULL,
    [BillingEmail]                  NVARCHAR (256)   NOT NULL,
    [Plan]                          NVARCHAR (50)    NOT NULL,
    [PlanType]                      TINYINT          NOT NULL,
    [Seats]                         INT              NULL,
    [MaxCollections]                SMALLINT         NULL,
    [UsePolicies]                   BIT              NOT NULL,
    [UseSso]                        BIT              NOT NULL,
    [UseGroups]                     BIT              NOT NULL,
    [UseDirectory]                  BIT              NOT NULL,
    [UseEvents]                     BIT              NOT NULL,
    [UseTotp]                       BIT              NOT NULL,
    [Use2fa]                        BIT              NOT NULL,
    [UseApi]                        BIT              NOT NULL,
    [UseResetPassword]              BIT              NOT NULL,
    [SelfHost]                      BIT              NOT NULL,
    [UsersGetPremium]               BIT              NOT NULL,
    [Storage]                       BIGINT           NULL,
    [MaxStorageGb]                  SMALLINT         NULL,
    [Gateway]                       TINYINT          NULL,
    [GatewayCustomerId]             VARCHAR (50)     NULL,
    [GatewaySubscriptionId]         VARCHAR (50)     NULL,
    [ReferenceData]                 NVARCHAR (MAX)   NULL,
    [Enabled]                       BIT              NOT NULL,
    [LicenseKey]                    VARCHAR (100)    NULL,
    [PublicKey]                     VARCHAR (MAX)    NULL,
    [PrivateKey]                    VARCHAR (MAX)    NULL,
    [TwoFactorProviders]            NVARCHAR (MAX)   NULL,
    [ExpirationDate]                DATETIME2 (7)    NULL,
    [CreationDate]                  DATETIME2 (7)    NOT NULL,
    [RevisionDate]                  DATETIME2 (7)    NOT NULL,
    [OwnersNotifiedOfAutoscaling]   DATETIME2(7)     NULL,
    [MaxAutoscaleSeats]             INT              NULL,
    [UseKeyConnector]               BIT              NOT NULL,
    [UseScim]                       BIT              NOT NULL CONSTRAINT [DF_Organization_UseScim] DEFAULT (0),
    CONSTRAINT [PK_Organization] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Organization_Enabled]
    ON [dbo].[Organization]([Id] ASC, [Enabled] ASC)
    INCLUDE ([UseTotp]);

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Organization_Identifier]
    ON [dbo].[Organization]([Identifier] ASC)
    WHERE [Identifier] IS NOT NULL;
