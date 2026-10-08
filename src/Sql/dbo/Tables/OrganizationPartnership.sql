CREATE TABLE [dbo].[OrganizationPartnership]
(
    [Id]                           UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]               UNIQUEIDENTIFIER NOT NULL,
    [Name]                         NVARCHAR(50)     NOT NULL,
    [Status]                       TINYINT          NOT NULL,
    [SponsoredPlanType]            TINYINT          NOT NULL,
    [BindingMode]                  TINYINT          NOT NULL,
    [IdentityBindingConfiguration] NVARCHAR(MAX)    NULL,
    [RegisteredReturnOrigins]      NVARCHAR(MAX)    NOT NULL,
    [CreationDate]                 DATETIME2(7)     NOT NULL,
    [RevisionDate]                 DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_OrganizationPartnership] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPartnership_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationPartnership_OrganizationId]
    ON [dbo].[OrganizationPartnership]([OrganizationId] ASC);
GO
