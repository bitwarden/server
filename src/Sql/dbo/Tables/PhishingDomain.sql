CREATE TABLE [dbo].[PhishingDomain] (
    [Id]            UNIQUEIDENTIFIER    NOT NULL,
    [Domain]        NVARCHAR(255)       NOT NULL,
    [CreationDate]  DATETIME2(7)        NOT NULL,
    [RevisionDate]  DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PhishingDomain] PRIMARY KEY CLUSTERED ([Id] ASC)
);

GO

CREATE NONCLUSTERED INDEX [IX_PhishingDomain_Domain]
    ON [dbo].[PhishingDomain]([Domain] ASC); 