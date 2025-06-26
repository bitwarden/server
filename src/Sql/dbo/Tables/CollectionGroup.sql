﻿CREATE TABLE [dbo].[CollectionGroup] (
    [CollectionId]  UNIQUEIDENTIFIER NOT NULL,
    [GroupId]       UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]      BIT              NOT NULL,
    [HidePasswords] BIT              NOT NULL,
    [Manage]        BIT              NOT NULL CONSTRAINT D_CollectionGroup_Manage DEFAULT (0),
    CONSTRAINT [PK_CollectionGroup] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [GroupId] ASC),
    CONSTRAINT [FK_CollectionGroup_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]),
    CONSTRAINT [FK_CollectionGroup_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE
);

GO
CREATE NONCLUSTERED INDEX IX_CollectionGroup_GroupId
    ON [dbo].[CollectionGroup] (GroupId)
    INCLUDE (ReadOnly, HidePasswords, Manage)

GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CollectionGroup')
        AND name = 'IX_CollectionGroup_GroupId_ReadOnly'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_CollectionGroup_GroupId_ReadOnly
      ON dbo.CollectionGroup (GroupId, ReadOnly)
      INCLUDE (CollectionId);
END
