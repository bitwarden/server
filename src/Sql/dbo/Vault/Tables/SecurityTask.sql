CREATE TABLE [dbo].[SecurityTask]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NULL,
    [Type] TINYINT NOT NULL,
    [Status] TINYINT NOT NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    [RevisionDate] DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_SecurityTask] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SecurityTask_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecurityTask_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
);

GO
CREATE NONCLUSTERED INDEX [IX_SecurityTask_CipherId]
    ON [dbo].[SecurityTask]([CipherId] ASC) WHERE CipherId IS NOT NULL;

GO
-- Covers SecurityTask_ReadByUserIdStatus: that procedure reads tasks by organization and returns
-- every column, so without the INCLUDE list each row costs a lookup into the clustered index.
-- The previous WHERE OrganizationId IS NOT NULL filter was redundant on a NOT NULL column, and the
-- EF model has always declared this index unfiltered, so dropping it also aligns the two tracks.
CREATE NONCLUSTERED INDEX [IX_SecurityTask_OrganizationId]
    ON [dbo].[SecurityTask]([OrganizationId] ASC)
    INCLUDE ([Status], [CipherId], [Type], [CreationDate], [RevisionDate]);

GO

