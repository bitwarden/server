CREATE TABLE [dbo].[ProviderUser] (
    [Id]           UNIQUEIDENTIFIER    NOT NULL,
    [ProviderId]   UNIQUEIDENTIFIER    NOT NULL,
    [UserId]       UNIQUEIDENTIFIER    NULL,
    [Email]        NVARCHAR (256)      NULL,
    [Key]          VARCHAR (MAX)       NULL,
    [Status]       TINYINT             NOT NULL,
    [Type]         TINYINT             NOT NULL,
    [Permissions]  NVARCHAR (MAX)      NULL,
    [CreationDate] DATETIME2 (7)       NOT NULL,
    [RevisionDate] DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_ProviderUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProviderUser_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProviderUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
