﻿CREATE TABLE [dbo].[Provider] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Name]                  NVARCHAR (50)    NULL,
    [BusinessName]          NVARCHAR (50)    NULL,
    [BusinessAddress1]      NVARCHAR (50)    NULL,
    [BusinessAddress2]      NVARCHAR (50)    NULL,
    [BusinessAddress3]      NVARCHAR (50)    NULL,
    [BusinessCountry]       VARCHAR (2)      NULL,
    [BusinessTaxNumber]     NVARCHAR (30)    NULL,
    [BillingEmail]          NVARCHAR (256)   NULL,
    [BillingPhone]          NVARCHAR (50)    NULL,
    [Status]                TINYINT          NOT NULL,
    [UseEvents]             BIT              NOT NULL,
    [Type]                  TINYINT          NOT NULL CONSTRAINT DF_Provider_Type DEFAULT (0),
    [Enabled]               BIT              NOT NULL,
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    [Gateway]               TINYINT          NULL,
    [GatewayCustomerId]     VARCHAR (50)     NULL,
    [GatewaySubscriptionId] VARCHAR (50)     NULL,
    [DiscountId]            VARCHAR (50)     NULL,
    CONSTRAINT [PK_Provider] PRIMARY KEY CLUSTERED ([Id] ASC)
);
