-- Remove ON DELETE for service accounts
IF EXISTS (SELECT name FROM sys.foreign_keys WHERE name = 'FK_ServiceAccount_OrganizationId')
BEGIN
    ALTER TABLE [ServiceAccount] DROP CONSTRAINT [FK_ServiceAccount_OrganizationId];
END

ALTER TABLE [ServiceAccount] ADD CONSTRAINT [FK_ServiceAccount_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]);
GO

IF OBJECT_ID('[dbo].[AccessPolicy]') IS NULL
BEGIN
    CREATE TABLE [AccessPolicy] (
        [Id]                      UNIQUEIDENTIFIER NOT NULL,
        [Discriminator]           NVARCHAR(50)     NOT NULL,
        [OrganizationUserId]      UNIQUEIDENTIFIER NULL,
        [GroupId]                 UNIQUEIDENTIFIER NULL,
        [ServiceAccountId]        UNIQUEIDENTIFIER NULL,
        [GrantedProjectId]        UNIQUEIDENTIFIER NULL,
        [GrantedServiceAccountId] UNIQUEIDENTIFIER NULL,
        [Read]                    BIT NOT NULL,
        [Write]                   BIT NOT NULL,
        [CreationDate]            DATETIME2 NOT NULL,
        [RevisionDate]            DATETIME2 NOT NULL,
        CONSTRAINT [PK_AccessPolicy] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AccessPolicy_Group_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Group] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AccessPolicy_OrganizationUser_OrganizationUserId] FOREIGN KEY ([OrganizationUserId]) REFERENCES [OrganizationUser] ([Id]),
        CONSTRAINT [FK_AccessPolicy_Project_GrantedProjectId] FOREIGN KEY ([GrantedProjectId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId] FOREIGN KEY ([GrantedServiceAccountId]) REFERENCES [ServiceAccount] ([Id]),
        CONSTRAINT [FK_AccessPolicy_ServiceAccount_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [ServiceAccount] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GroupId] ON [AccessPolicy] ([GroupId]);

    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_OrganizationUserId] ON [AccessPolicy] ([OrganizationUserId]);

    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedProjectId] ON [AccessPolicy] ([GrantedProjectId]);

    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_ServiceAccountId] ON [AccessPolicy] ([ServiceAccountId]);

    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedServiceAccountId] ON [AccessPolicy] ([GrantedServiceAccountId]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id

    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT
        @OrganizationId = [OrganizationId],
        @UserId = [UserId]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL AND @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[SsoUser_Delete] @UserId, @OrganizationId
    END

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[AccessPolicy]
    WHERE
        [OrganizationUserId] = @Id

    EXEC [dbo].[OrganizationSponsorship_OrganizationUserDeleted] @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id
END
