CREATE TABLE [dbo].[PasswordHealthReportApplication] 
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Uri nvarchar(max),
        CreationDate   DATETIME2(7)     NOT NULL,
        RevisionDate   DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_PasswordHealthReportApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PasswordHealthReportApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    );
GO

CREATE NONCLUSTERED INDEX [IX_PasswordHealthReportApplication_OrganizationId]
        ON [dbo].[PasswordHealthReportApplication] (OrganizationId);
GO