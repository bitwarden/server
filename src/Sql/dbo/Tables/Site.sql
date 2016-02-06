CREATE TABLE [dbo].[Site] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [FolderId]     UNIQUEIDENTIFIER NULL,
    [Name]         NVARCHAR (MAX)   NOT NULL,
    [Uri]          NVARCHAR (MAX)   NULL,
    [Username]     NVARCHAR (MAX)   NULL,
    [Password]     NVARCHAR (MAX)   NULL,
    [Notes]        NVARCHAR (MAX)   NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Site] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Site_Folder] FOREIGN KEY ([FolderId]) REFERENCES [dbo].[Folder] ([Id]),
    CONSTRAINT [FK_Site_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Site_UserId]
    ON [dbo].[Site]([UserId] ASC);

