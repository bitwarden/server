-- One daemon's try at executing a PamRotationJob (invariant AtMostOneInFlightAttemptPerJob is enforced by
-- PamRotationJob_Claim, which inserts the Executing attempt in the same transaction as the claim). Unlike
-- PamRotationJob.ClaimedByDaemonId, this ClaimedByDaemonId is never cleared -- it is the permanent record of who
-- executed this particular try, kept even after the job moves on. FailureReason is bounded and truncated (never
-- rejected) by the caller before write -- the zero-knowledge failure-reason contract forbids forwarding raw
-- target-system error output, since it can echo credentials -- so this column only ever stores the bounded text.
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

-- PamRotationJob_ReadManyByConfigId's attempt result set, and the "no Rotated attempt" checks in the timeout/release
-- sweeps.
CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_JobId_Status]
    ON [dbo].[PamRotationAttempt] ([JobId] ASC, [Status] ASC);
GO
