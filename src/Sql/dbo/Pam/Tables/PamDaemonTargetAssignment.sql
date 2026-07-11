-- Which daemon is responsible for rotating credentials on which target system (invariant OneAssignmentPerDaemonTarget).
-- DaemonId and TargetSystemId are deliberately ON DELETE NO ACTION: both already reach Organization via their own
-- cascading FK, and also letting this table cascade from either of them would create multiple cascade paths back to
-- Organization -- a combination SQL Server refuses at CREATE TABLE time. OrganizationId therefore carries the only
-- cascade path, so deleting an organization still removes its assignments; a PamDaemon/PamTargetSystem row that still
-- has an assignment must be detached first (or its own delete will hit this table's NO ACTION FK).
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

-- OneAssignmentPerDaemonTarget.
CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
    ON [dbo].[PamDaemonTargetAssignment] ([DaemonId] ASC, [TargetSystemId] ASC);
GO

-- Supports the reverse lookup (which daemons cover a given target) and the claim sproc's join from target ->
-- assignment -> daemon.
CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_TargetSystemId]
    ON [dbo].[PamDaemonTargetAssignment] ([TargetSystemId] ASC);
GO
