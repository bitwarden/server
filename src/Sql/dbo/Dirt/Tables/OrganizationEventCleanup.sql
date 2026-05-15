CREATE TABLE [dbo].[OrganizationEventCleanup] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
    [QueuedAt]           DATETIME2 (7)    NOT NULL,
    [StartedAt]          DATETIME2 (7)    NULL,
    [LastProgressAt]     DATETIME2 (7)    NULL,
    [CompletedAt]        DATETIME2 (7)    NULL,
    [EventsDeletedCount] BIGINT           NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_EventsDeletedCount] DEFAULT (0),
    [Attempts]           INT              NOT NULL CONSTRAINT [DF_OrganizationEventCleanup_Attempts] DEFAULT (0),
    [LastError]          NVARCHAR(MAX)    NULL,
    CONSTRAINT [PK_OrganizationEventCleanup] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationEventCleanup_CompletedAt_QueuedAt]
    ON [dbo].[OrganizationEventCleanup]([CompletedAt] ASC, [QueuedAt] ASC);
GO
