CREATE TABLE [dbo].[OrganizationEventCleanup] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    [RevisionDate]       DATETIME2 (7)    NULL,
    [StartDate]          DATETIME2 (7)    NULL,
    [CompletedDate]      DATETIME2 (7)    NULL,
    [EventsDeletedCount] BIGINT           NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_EventsDeletedCount] DEFAULT (0),
    [Attempts]           INT              NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_Attempts] DEFAULT (0),
    [LastError]          NVARCHAR(MAX)    NULL,
    CONSTRAINT [PK_OrganizationEventCleanup] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationEventCleanup_CompletedDate_CreationDate]
    ON [dbo].[OrganizationEventCleanup]([CompletedDate] ASC, [CreationDate] ASC);
GO
