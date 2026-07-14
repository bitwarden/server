CREATE TABLE [dbo].[AccessRule] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [Name]              NVARCHAR(256)       NOT NULL,
    [Description]       NVARCHAR(MAX)       NULL,
    [Conditions]        NVARCHAR(MAX)       NOT NULL,
    [SingleActiveLease] BIT                 NOT NULL CONSTRAINT [DF_AccessRule_SingleActiveLease] DEFAULT (0),
    [DefaultLeaseDurationSeconds] INT       NULL,
    [MaxLeaseDurationSeconds]     INT       NULL,
    [Enabled]           BIT                 NOT NULL CONSTRAINT [DF_AccessRule_Enabled] DEFAULT (1),
    [AllowsExtensions]  BIT                 NOT NULL CONSTRAINT [DF_AccessRule_AllowsExtensions] DEFAULT (0),
    [MaxExtensionDurationSeconds] INT       NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    [RevisionDate]      DATETIME2(7)        NOT NULL,
    [LastEditedBy]      UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_AccessRule] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessRule_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

-- A rule's name is unique per organization; a hard delete frees the name naturally.
CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessRule_OrganizationId_Name]
    ON [dbo].[AccessRule] ([OrganizationId] ASC, [Name] ASC);
GO
