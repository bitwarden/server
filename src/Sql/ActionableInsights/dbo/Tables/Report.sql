CREATE TABLE [dbo].[Report]
(
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR(MAX)    NULL,
    [GroupId]        UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Parameters]     NVARCHAR(MAX)    NOT NULL,
    [CreationDate]   DATETIME2(7),
    [RevisionDate]   DATETIME2(7),
    CONSTRAINT [PK_Report] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Report_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Report_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_Report_OrganizationId] ON [dbo].[Report] ([OrganizationId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Report_Group] ON [dbo].[Report] ([GroupId] ASC)
    WHERE [GroupId] IS NOT NULL;
