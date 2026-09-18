-- Which daemon rotates which target (OneAssignmentPerDaemonTarget).
-- DaemonId/TargetSystemId are NO ACTION; multiple cascade paths to Organization aren't allowed.
-- OrganizationId carries the only cascade; detach an assignment before deleting its daemon/target.
CREATE TABLE [dbo].[PamDaemonTargetAssignment] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [DaemonId]          UNIQUEIDENTIFIER    NOT NULL,
    [TargetSystemId]    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamDaemonTargetAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamDaemonTargetAssignment_Daemon] FOREIGN KEY ([DaemonId]) REFERENCES [dbo].[PamDaemon] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PamDaemonTargetAssignment_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PamDaemonTargetAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
    ON [dbo].[PamDaemonTargetAssignment] ([DaemonId] ASC, [TargetSystemId] ASC);
GO

-- Supports the reverse lookup and the claim sproc's target -> assignment -> daemon join.
CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_TargetSystemId]
    ON [dbo].[PamDaemonTargetAssignment] ([TargetSystemId] ASC);
GO

-- Backs PamDaemonTargetAssignment_ReadByOrganizationId and the Organization cascade delete.
CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_OrganizationId]
    ON [dbo].[PamDaemonTargetAssignment] ([OrganizationId] ASC);
GO
