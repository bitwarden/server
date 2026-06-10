CREATE TABLE [dbo].[AccessDecision] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [AccessRequestId]       UNIQUEIDENTIFIER    NOT NULL,
    [DeciderKind]           TINYINT             NOT NULL,
    [ApproverId]            UNIQUEIDENTIFIER    NULL,
    [ConditionKind]         NVARCHAR(50)        NULL,
    [Verdict]               TINYINT             NOT NULL,
    [Comment]               NVARCHAR(MAX)       NULL,
    [EvaluationContext]     NVARCHAR(MAX)       NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_AccessDecision] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessDecision_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessDecision_AccessRequestId]
    ON [dbo].[AccessDecision] ([AccessRequestId] ASC);
GO
