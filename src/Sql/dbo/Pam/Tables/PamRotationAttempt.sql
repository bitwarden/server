-- One access connector's try at a job (AtMostOneInFlightAttemptPerJob).
-- ClaimedByAccessConnectorId stays permanent here; the job's own field clears instead.
-- FailureReason is bounded by the caller under the zero-knowledge contract.
CREATE TABLE [dbo].[PamRotationAttempt] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [JobId]                         UNIQUEIDENTIFIER    NOT NULL,
    [ClaimedByAccessConnectorId]    UNIQUEIDENTIFIER    NOT NULL,
    [CipherUpdated]                 BIT                 NOT NULL,
    [Action]                        TINYINT             NOT NULL,
    [FailureReason]                 NVARCHAR(500)       NULL,
    [SyncState]                     TINYINT             NULL,
    [SessionTermination]            TINYINT             NULL,
    [CreationDate]                  DATETIME2(7)        NOT NULL,
    [ResolvedDate]                  DATETIME2(7)        NULL,
    CONSTRAINT [PK_PamRotationAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamRotationAttempt_RotationJob] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PamRotationJob] ([Id])
);
GO

-- Backs PamRotationJob_ReadManyByConfigId and PamRotationAttempt_MarkErrored's retry-budget count.
CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_JobId_Action]
    ON [dbo].[PamRotationAttempt] ([JobId] ASC, [Action] ASC);
GO

-- Both of PamRotationJob_ReadManyRecentByAccessConnectorId's result sets seek on ClaimedByAccessConnectorId.
CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId]
    ON [dbo].[PamRotationAttempt] ([ClaimedByAccessConnectorId] ASC, [JobId] ASC);
GO
