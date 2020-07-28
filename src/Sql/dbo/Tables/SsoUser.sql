CREATE TABLE [dbo].[SsoUser] (
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [ExternalId]        NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_SsoUser] PRIMARY KEY CLUSTERED ([UserId] ASC, [OrganizationId] ASC),
    CONSTRAINT [FK_SsoUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SsoUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdExternalId]
    ON [dbo].[SsoUser]([OrganizationId] ASC, [ExternalId] ASC)
    INCLUDE ([UserId]);



