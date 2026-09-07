-- One rotation offer per config (AtMostOneActiveJobPerConfig).
-- Claimed fields null on every exit; the executing identity lives on PamRotationAttempt.
-- ExpiresAt is persisted at creation so the timeout sweep is a plain range scan.
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
    -- No cascade; PamRotationConfig_DeleteWithJobs deletes jobs/attempts explicitly in one transaction.
    CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE NO ACTION
);
GO

-- Backs PamRotationJob_ReadManyByConfigId and the active-job checks.
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
