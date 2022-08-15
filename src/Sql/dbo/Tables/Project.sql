
CREATE TABLE [dbo].[Project] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NULL,
    [Name]              NVARCHAR(MAX), 
    [CreationDate]      DATETIME2 (7),
    [RevisionDate]      DATETIME2 (7), 
    [DeletedDate]       DATETIME2 (7),
    CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);
