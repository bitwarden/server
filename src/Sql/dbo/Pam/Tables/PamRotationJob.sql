-- One rotation offer per config (AtMostOneActiveJobPerConfig).
-- Claim fields clear on every write that ends a claim; a timeout writes nothing, so a timed-out claim keeps them.
-- ExpiresAt is persisted at creation so the timeout sweep is a plain range scan.
CREATE TABLE [dbo].[PamRotationJob] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [RotationConfigId]              UNIQUEIDENTIFIER    NOT NULL,
    [Source]                        TINYINT             NOT NULL,
    [Action]                        TINYINT             NOT NULL,
    [ClaimedByAccessConnectorId]    UNIQUEIDENTIFIER    NULL,
    [ClaimedAt]                     DATETIME2(7)        NULL,
    [CreationDate]                  DATETIME2(7)        NOT NULL,
    [NextClaimableAt]               DATETIME2(7)        NOT NULL,
    [ExpiresAt]                     DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamRotationJob] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- No cascade; PamRotationConfig_DeleteWithJobs deletes jobs/attempts explicitly in one transaction.
    CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE NO ACTION
);
GO

-- Backs PamRotationJob_ReadManyByConfigId and the active-job checks.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_Action]
    ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [Action] ASC);
GO

-- The timeout sweep (PamRotationJob_TimeoutDue): unresolved jobs past ExpiresAt.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_Action_ExpiresAt]
    ON [dbo].[PamRotationJob] ([Action] ASC, [ExpiresAt] ASC);
GO

-- The release sweep (PamRotationJob_ReleaseExpiredLeases): claimed jobs by claimant, joined to access connector heartbeat.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_ClaimedByAccessConnectorId_Action]
    ON [dbo].[PamRotationJob] ([ClaimedByAccessConnectorId] ASC, [Action] ASC);
GO
