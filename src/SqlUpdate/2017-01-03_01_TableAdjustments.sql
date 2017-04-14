-- Get rid of history table

DROP TABLE [History]
GO

-- Setup new tables

CREATE TABLE [dbo].[Favorite] (
    [UserId]   UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_Favorite] PRIMARY KEY CLUSTERED ([UserId] ASC, [CipherId] ASC)
)
GO

CREATE TABLE [dbo].[Folder] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         VARCHAR (MAX)    NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Folder] PRIMARY KEY CLUSTERED ([Id] ASC)
)
GO

CREATE TABLE [dbo].[FolderCipher] (
    [FolderId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_FolderCipher] PRIMARY KEY CLUSTERED ([UserId] ASC, [FolderId] ASC, [CipherId] ASC)
)
GO



