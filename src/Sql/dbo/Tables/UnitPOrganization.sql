CREATE TABLE [dbo].[UnitPOrganization] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [UnitPId]                       UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NULL,
    [CreationDate]                  DATETIME2 (7)       NOT NULL,
    [RevisionDate]                  DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_UnitPOrganization] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UnitPOrganization_UnitP] FOREIGN KEY ([UnitPId]) REFERENCES [dbo].[UnitP] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UnitPOrganization_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
