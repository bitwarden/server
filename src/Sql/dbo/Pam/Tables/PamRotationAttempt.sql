-- One daemon's try at a job (AtMostOneInFlightAttemptPerJob).
-- ClaimedByDaemonId stays permanent here; the job's own field clears instead.
-- FailureReason is bounded by the caller under the zero-knowledge contract.
CREATE TABLE [dbo].[PamRotationAttempt] (
    [Id]                     UNIQUEIDENTIFIER    NOT NULL,
    [JobId]                  UNIQUEIDENTIFIER    NOT NULL,
    [ClaimedByDaemonId]      UNIQUEIDENTIFIER    NOT NULL,
    [CipherUpdated]          BIT                 NOT NULL,
    [Status]                 TINYINT             NOT NULL,
    [FailureReason]          NVARCHAR(500)       NULL,
    [SyncState]              TINYINT             NULL,
    [SessionTermination]     TINYINT             NULL,
    [CreationDate]           DATETIME2(7)        NOT NULL,
    [ResolvedDate]           DATETIME2(7)        NULL,
    CONSTRAINT [PK_PamRotationAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamRotationAttempt_RotationJob] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PamRotationJob] ([Id])
);
GO

-- Backs PamRotationJob_ReadManyByConfigId and the "no Rotated attempt" timeout/release checks.
CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_JobId_Status]
    ON [dbo].[PamRotationAttempt] ([JobId] ASC, [Status] ASC);
GO

-- Both of PamRotationJob_ReadManyRecentByDaemonId's result sets seek on ClaimedByDaemonId.
CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_ClaimedByDaemonId_JobId]
    ON [dbo].[PamRotationAttempt] ([ClaimedByDaemonId] ASC, [JobId] ASC);
GO
