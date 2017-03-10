CREATE TABLE [dbo].[SubvaultUser] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [SubvaultId]         UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [Admin]              BIT              NOT NULL,
    [ReadOnly]           BIT              NOT NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    [RevisionDate]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_SubvaultUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SubvaultUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id]),
    CONSTRAINT [FK_SubvaultUser_Subvault] FOREIGN KEY ([SubvaultId]) REFERENCES [dbo].[Subvault] ([Id])
);

