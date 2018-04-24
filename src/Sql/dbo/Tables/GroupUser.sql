CREATE TABLE [dbo].[GroupUser] (
    [GroupId]            UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_GroupUser] PRIMARY KEY CLUSTERED ([GroupId] ASC, [OrganizationUserId] ASC),
    CONSTRAINT [FK_GroupUser_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GroupUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_GroupUser_OrganizationUserId]
    ON [dbo].[GroupUser]([OrganizationUserId] ASC);



