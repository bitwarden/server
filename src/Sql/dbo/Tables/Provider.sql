CREATE TABLE [dbo].[Provider] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR (50)    NULL,
    [BusinessName]      NVARCHAR (50)    NULL,
    [BusinessAddress1]  NVARCHAR (50)    NULL,
    [BusinessAddress2]  NVARCHAR (50)    NULL,
    [BusinessAddress3]  NVARCHAR (50)    NULL,
    [BusinessCountry]   VARCHAR (2)      NULL,
    [BusinessTaxNumber] NVARCHAR (30)    NULL,
    [BillingEmail]      NVARCHAR (256)   NULL,
    [Status]            TINYINT          NOT NULL,
    [UseEvents]         BIT              NOT NULL,
    [Enabled]           BIT              NOT NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    [RevisionDate]      DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Provider] PRIMARY KEY CLUSTERED ([Id] ASC)
);
