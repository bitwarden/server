CREATE TABLE [dbo].[PasswordHealthReportApplications] 
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Uri nvarchar(max),
        CreationDate   DATETIME2(7)     NOT NULL,
        RevisionDate   DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_PasswordHealthReportApplications] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PasswordHealthApplications_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    );

CREATE NONCLUSTERED INDEX [IX_PasswordHealthReportApplications_OrganizationId]
        ON [dbo].[PasswordHealthReportApplications] (OrganizationId);