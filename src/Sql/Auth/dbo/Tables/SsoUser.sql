CREATE TABLE [dbo].[SsoUser] (
    [Id]                BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NULL,
    [ExternalId]        NVARCHAR(50)     NOT NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_SsoUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SsoUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SsoUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdExternalId]
    ON [dbo].[SsoUser]([OrganizationId] ASC, [ExternalId] ASC)
    INCLUDE ([UserId]);

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdUserId]
    ON [dbo].[SsoUser]([OrganizationId] ASC, [UserId] ASC);


