-- Access ends whose rotation trigger is still owed (spec pending_access_end), one row per ended lease. Written in the
-- transaction that ends the lease and deleted once the trigger has run, so the rotation sweep retries a failed trigger.
CREATE TABLE [dbo].[PamPendingAccessEnd] (
    [AccessLeaseId]     UNIQUEIDENTIFIER    NOT NULL,
    [RotationConfigId]  UNIQUEIDENTIFIER    NOT NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamPendingAccessEnd] PRIMARY KEY CLUSTERED ([AccessLeaseId] ASC),
    -- No FK to AccessLease: both parents cascade from Organization, and SQL Server rejects two cascade paths.
    CONSTRAINT [FK_PamPendingAccessEnd_PamRotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_PamPendingAccessEnd_RotationConfigId]
    ON [dbo].[PamPendingAccessEnd] ([RotationConfigId] ASC);
GO
