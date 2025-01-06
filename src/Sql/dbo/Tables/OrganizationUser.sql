﻿CREATE TABLE [dbo].[OrganizationUser] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
    [UserId]                        UNIQUEIDENTIFIER    NULL,
    [Email]                         NVARCHAR (256)      NULL,
    [Key]                           VARCHAR (MAX)       NULL,
    [ResetPasswordKey]              VARCHAR (MAX)       NULL,
    [Status]                        SMALLINT            NOT NULL,
    [Type]                          TINYINT             NOT NULL,
    [ExternalId]                    NVARCHAR (300)      NULL,
    [CreationDate]                  DATETIME2 (7)       NOT NULL,
    [RevisionDate]                  DATETIME2 (7)       NOT NULL,
    [Permissions]                   NVARCHAR (MAX)      NULL,
    [AccessSecretsManager]          BIT                 NOT NULL CONSTRAINT [DF_OrganizationUser_SecretsManager] DEFAULT (0),
    CONSTRAINT [PK_OrganizationUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_OrganizationUser_UserIdOrganizationIdStatusV2]
    ON [dbo].[OrganizationUser]([UserId] ASC, [OrganizationId] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_OrganizationUser_OrganizationId]
    ON [dbo].[OrganizationUser]([OrganizationId] ASC);

