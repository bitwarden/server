IF OBJECT_ID('[dbo].[Project]') IS NULL
BEGIN
CREATE TABLE [dbo].[Project] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR(MAX) NULL, 
    [CreationDate]      DATETIME2 (7) NULL,
    [RevisionDate]      DATETIME2 (7) NULL, 
    [DeletedDate]       DATETIME2 (7) NULL,
    CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);
END

GO
