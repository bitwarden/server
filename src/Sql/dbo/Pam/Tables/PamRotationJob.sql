-- One rotation offer per config (AtMostOneActiveJobPerConfig).
-- Claim fields clear on every write that ends a claim; a timeout writes nothing, so they stay set.
-- ExpirationDate is persisted so the timeout sweep is a plain range scan.
CREATE TABLE [dbo].[PamRotationJob] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [RotationConfigId]              UNIQUEIDENTIFIER    NOT NULL,
    [Source]                        TINYINT             NOT NULL,
    [Action]                        TINYINT             NOT NULL,
    [ClaimedByAccessConnectorId]    UNIQUEIDENTIFIER    NULL,
    [ClaimedDate]                   DATETIME2(7)        NULL,
    [CreationDate]                  DATETIME2(7)        NOT NULL,
    [NextClaimableDate]             DATETIME2(7)        NOT NULL,
    [ExpirationDate]                DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamRotationJob] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- Cascades, since a job has no OrganizationId and the Organization delete only reaches it through the config.
    CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE CASCADE
);
GO

-- Backs PamRotationJob_ReadManyByConfigId and the active-job checks.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_Action]
    ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [Action] ASC);
GO

-- The timeout sweep (PamRotationJobTimeoutSweep_CreateManyDue): unresolved jobs past ExpirationDate.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_Action_ExpirationDate]
    ON [dbo].[PamRotationJob] ([Action] ASC, [ExpirationDate] ASC);
GO

-- Backs the newest-job lookup in PamRotationConfigDetailsView. Job history is never purged, so without it the
-- organization list read degrades as rotations accumulate. Also serves PamRotationJob_ReadManyByConfigId.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_CreationDate]
    ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [CreationDate] DESC, [Id] DESC)
    INCLUDE ([Action], [ExpirationDate]);
GO

-- The release sweep (PamRotationJob_UpdateManyReleaseExpiredLeases): claimed jobs by claimant, joined to access
-- connector heartbeat.
CREATE NONCLUSTERED INDEX [IX_PamRotationJob_ClaimedByAccessConnectorId_Action]
    ON [dbo].[PamRotationJob] ([ClaimedByAccessConnectorId] ASC, [Action] ASC);
GO
