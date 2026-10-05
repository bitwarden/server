-- Which access connector rotates which target (OneAssignmentPerConnectorTarget).
-- AccessConnectorId/TargetSystemId are NO ACTION; multiple cascade paths to Organization aren't allowed.
-- OrganizationId carries the only cascade; detach an assignment before deleting its access connector/target.
CREATE TABLE [dbo].[PamAccessConnectorTargetAssignment] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [AccessConnectorId] UNIQUEIDENTIFIER    NOT NULL,
    [TargetSystemId]    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamAccessConnectorTargetAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PamAccessConnectorTargetAssignment_AccessConnector] FOREIGN KEY ([AccessConnectorId]) REFERENCES [dbo].[PamAccessConnector] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PamAccessConnectorTargetAssignment_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PamAccessConnectorTargetAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId]
    ON [dbo].[PamAccessConnectorTargetAssignment] ([AccessConnectorId] ASC, [TargetSystemId] ASC);
GO

-- Supports the reverse lookup and the claim sproc's target -> assignment -> access connector join.
CREATE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_TargetSystemId]
    ON [dbo].[PamAccessConnectorTargetAssignment] ([TargetSystemId] ASC);
GO

-- Backs PamAccessConnectorTargetAssignment_ReadByOrganizationId and the Organization cascade delete.
CREATE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_OrganizationId]
    ON [dbo].[PamAccessConnectorTargetAssignment] ([OrganizationId] ASC);
GO
