CREATE TABLE [dbo].[SubvaultGroup] (
    [SubvaultId] UNIQUEIDENTIFIER NOT NULL,
    [GroupId]    UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_SubvaultGroup] PRIMARY KEY CLUSTERED ([SubvaultId] ASC, [GroupId] ASC),
    CONSTRAINT [FK_SubvaultGroup_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubvaultGroup_Subvault] FOREIGN KEY ([SubvaultId]) REFERENCES [dbo].[Subvault] ([Id])
);

