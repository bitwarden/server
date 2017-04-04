CREATE TABLE [dbo].[Organization] (
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [UserId]               UNIQUEIDENTIFIER NOT NULL,
    [Name]                 NVARCHAR (50)    NOT NULL,
    [BusinessName]         NVARCHAR (50)    NULL,
    [BillingEmail]         NVARCHAR (50)    NOT NULL,
    [Plan]                 NVARCHAR (20)    NOT NULL,
    [PlanType]             TINYINT          NOT NULL,
    [PlanBasePrice]        MONEY            NOT NULL,
    [PlanUserPrice]        MONEY            NOT NULL,
    [PlanRenewalDate]      DATETIME2 (7)    NULL,
    [PlanTrial]            BIT              NOT NULL,
    [BaseUsers]            SMALLINT         NULL,
    [AdditionalUsers]      SMALLINT         NULL,
    [MaxUsers]             SMALLINT         NULL,
    [StripeCustomerId]     VARCHAR (50)     NULL,
    [StripeSubscriptionId] VARCHAR (50)     NULL,
    [CreationDate]         DATETIME2 (7)    NOT NULL,
    [RevisionDate]         DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Organization] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Organization_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

