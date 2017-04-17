-- Get rid of history table

DROP TABLE [History]
GO

-- Setup new tables

CREATE TABLE [dbo].[Folder] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         VARCHAR (MAX)    NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Folder] PRIMARY KEY CLUSTERED ([Id] ASC)
)
GO

-- Setup new columns for cipher changes

ALTER TABLE [dbo].[Cipher]
ADD [Favorites] VARCHAR(MAX) NULL
GO

ALTER TABLE [dbo].[Cipher]
ADD [Folders] VARCHAR(MAX) NULL
GO

