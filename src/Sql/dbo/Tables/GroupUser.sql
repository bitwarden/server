CREATE TABLE [dbo].[GroupUser] (
    [GroupId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]  UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_GroupUser] PRIMARY KEY CLUSTERED ([GroupId] ASC, [UserId] ASC),
    CONSTRAINT [FK_GroupUser_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GroupUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

