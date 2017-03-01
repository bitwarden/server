CREATE TABLE [dbo].[Folder] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         VARCHAR (MAX)    NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Folder] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Folder_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

