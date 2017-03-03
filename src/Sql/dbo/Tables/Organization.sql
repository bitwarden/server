CREATE TABLE [dbo].[Organization] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [UserId]           UNIQUEIDENTIFIER NOT NULL,
    [Name]             NVARCHAR (50)    NOT NULL,
    [Plan]             NVARCHAR (20)    NOT NULL,
    [PlanType]         TINYINT          NOT NULL,
    [PlanPrice]        MONEY            NOT NULL,
    [PlanRenewalPrice] MONEY            NOT NULL,
    [PlanRenewalDate]  DATETIME2 (7)    NULL,
    [PlanTrial]        BIT              NOT NULL,
    [MaxUsers]         SMALLINT         NULL,
    [CreationDate]     DATETIME2 (7)    NOT NULL,
    [RevisionDate]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Organization] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Organization_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

