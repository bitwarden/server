CREATE TABLE [dbo].[OrganizationDeleteTask] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
    [TaskType]           TINYINT          NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_TaskType] DEFAULT (0),
    [CreationDate]       DATETIME2(7)     NOT NULL,
    [RevisionDate]       DATETIME2(7)     NOT NULL,
    [StartDate]          DATETIME2(7)     NULL,
    [CompletedDate]      DATETIME2(7)     NULL,
    [ItemsDeletedCount]  BIGINT           NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_ItemsDeletedCount] DEFAULT (0),
    [FailureCount]       INT              NOT NULL CONSTRAINT [DF_OrganizationDeleteTask_FailureCount] DEFAULT (0),
    [LastError]          NVARCHAR(MAX)    NULL,
    CONSTRAINT [PK_OrganizationDeleteTask] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationDeleteTask_CompletedDate_CreationDate]
    ON [dbo].[OrganizationDeleteTask]([CompletedDate] ASC, [CreationDate] ASC);
GO
