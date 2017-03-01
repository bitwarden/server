CREATE TABLE [dbo].[Subvault] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [Name]         VARCHAR (MAX)    NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Subvault] PRIMARY KEY CLUSTERED ([Id] ASC)
);

