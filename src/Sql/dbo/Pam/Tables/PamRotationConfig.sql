-- One rotation policy per cipher (OneConfigPerCipher).
-- PasswordPolicy isn't duplicated here; it lives on the shared target system.
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
    -- No cascade; configs must be detached or deleted via PamRotationConfig_DeleteWithJobs first.
    CONSTRAINT [FK_PamRotationConfig_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION
);
GO

-- OneConfigPerCipher.
CREATE UNIQUE NONCLUSTERED INDEX [IX_PamRotationConfig_CipherId]
    ON [dbo].[PamRotationConfig] ([CipherId] ASC);
GO

-- Backs the due-rotation sweep; excludes paused/one-shot/access-end-only configs from the scan.
CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_NextRotationAt]
    ON [dbo].[PamRotationConfig] ([NextRotationAt] ASC)
    WHERE [Enabled] = 1 AND [NextRotationAt] IS NOT NULL;
GO

-- Backs PamRotationConfig_ReadManyByOrganizationId and the Organization cascade delete.
CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_OrganizationId]
    ON [dbo].[PamRotationConfig] ([OrganizationId] ASC);
GO

-- Backs the daemon poll's join, AnyByTargetSystem checks, and DeleteWithAssignments' range lock.
CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_TargetSystemId]
    ON [dbo].[PamRotationConfig] ([TargetSystemId] ASC);
GO
