CREATE TABLE [dbo].[CollectionGroup] (
    [CollectionId] UNIQUEIDENTIFIER NOT NULL,
    [GroupId]    UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_CollectionGroup] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [GroupId] ASC),
    CONSTRAINT [FK_CollectionGroup_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CollectionGroup_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id])
);

