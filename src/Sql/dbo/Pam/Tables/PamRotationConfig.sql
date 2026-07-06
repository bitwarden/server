-- The rotation policy for one managed credential (one cipher, invariant OneConfigPerCipher). AccountIdentity is
-- opaque and never parsed by the server -- it is whatever the target-specific daemon connector needs to locate the
-- account (e.g. a UPN or a DB login name). ScheduleCron is nullable: a config that only rotates on access-end
-- (RotateOnAccessEnd = 1), only on demand, or -- on a manual target -- only via a recorded human rotation, need not
-- carry a periodic schedule. PasswordPolicy is NOT duplicated here -- it lives on the target system, shared by every
-- config that rotates against it.
CREATE TABLE [dbo].[PamRotationConfig] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]              UNIQUEIDENTIFIER    NOT NULL,
    [TargetSystemId]        UNIQUEIDENTIFIER    NOT NULL,
    [AccountIdentity]       NVARCHAR(500)       NOT NULL,
    [TerminateSessions]     BIT                 NOT NULL,
    [ScheduleCron]          NVARCHAR(100)       NULL,
    [RotateOnAccessEnd]     BIT                 NOT NULL,
    [NextRotationAt]        DATETIME2(7)        NULL,
    [Enabled]               BIT                 NOT NULL,
    [LastRotationAt]        DATETIME2(7)        NULL,
    [CreationDate]          DATETIME2(7)        NOT NULL,
    [RevisionDate]          DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamRotationConfig] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamRotationConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    -- No cascade: a target system with configs against it must be detached (or the configs deleted via
    -- PamRotationConfig_DeleteWithJobs) before it can be removed.
    CONSTRAINT [FK_PamRotationConfig_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION
);
GO

-- OneConfigPerCipher.
CREATE UNIQUE NONCLUSTERED INDEX [IX_PamRotationConfig_CipherId]
    ON [dbo].[PamRotationConfig] ([CipherId] ASC);
GO

-- Backs the due-rotation sweep (PamRotationConfig_ReadManyDue): filtered so paused/one-shot/access-end-only configs
-- never enter the scan.
CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_NextRotationAt]
    ON [dbo].[PamRotationConfig] ([NextRotationAt] ASC)
    WHERE [Enabled] = 1 AND [NextRotationAt] IS NOT NULL;
GO
