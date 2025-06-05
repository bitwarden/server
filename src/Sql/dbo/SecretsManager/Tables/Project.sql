﻿CREATE TABLE [dbo].[Project] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR(MAX) NULL, 
    [CreationDate]      DATETIME2 (7),
    [RevisionDate]      DATETIME2 (7), 
    [DeletedDate]       DATETIME2 (7) NULL,
    CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_Project_OrganizationId] ON [dbo].[Project] ([OrganizationId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Project_DeletedDate] ON [dbo].[Project] ([DeletedDate] ASC);
