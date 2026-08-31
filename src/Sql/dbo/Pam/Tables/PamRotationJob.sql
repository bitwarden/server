-- One offer of rotation work for a config (invariant AtMostOneActiveJobPerConfig -- enforced by PamRotationJob_Create
-- under a range lock, not by a unique index, since Pending/Claimed jobs may still co-exist with terminal ones for the
-- same config across its history). Every transition out of Claimed (retry, release, success, failure, timeout) nulls
-- ClaimedByDaemonId/ClaimedAt -- the executing daemon's identity for a given try lives on PamRotationAttempt instead.
-- ExpiresAt is persisted at creation (CreationDate + JobTtl) rather than derived, so the timeout sweep is a plain
-- range scan.
CREATE TABLE [dbo].[PamRotationJob] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [RotationConfigId]      UNIQUEIDENTIFIER    NOT NULL,
    [Source]                TINYINT             NOT NULL,
    [Status]                TINYINT             NOT NULL,
    [ClaimedByDaemonId]     UNIQUEIDENTIFIER    NULL,
    [ClaimedAt]             DATETIME2(7)        NULL,
    [CreationDate]          DATETIME2(7)        NOT NULL,
    [NextClaimableAt]       DATETIME2(7)        NOT NULL,
    [ExpiresAt]             DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamRotationJob] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- No cascade: PamRotationConfig_DeleteWithJobs deletes a config's jobs (and their attempts) explicitly, in order,
    -- inside one transaction -- deletion is a sproc concern, not a schema-level cascade.
    CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE NO ACTION
);
GO

-- PamRotationJob_ReadManyByConfigId and the active-job checks (PamRotationJob_Create's guard, PamRotationConfig_ReadManyDue).
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_Status]
    ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [Status] ASC);
GO

-- The timeout sweep (PamRotationJob_TimeoutDue): Pending/Claimed jobs past ExpiresAt.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_Status_ExpiresAt]
    ON [dbo].[PamRotationJob] ([Status] ASC, [ExpiresAt] ASC);
GO

-- The release sweep (PamRotationJob_ReleaseExpiredLeases): claimed jobs by claimant, joined to daemon heartbeat.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_ClaimedByDaemonId_Status]
    ON [dbo].[PamRotationJob] ([ClaimedByDaemonId] ASC, [Status] ASC);
GO
