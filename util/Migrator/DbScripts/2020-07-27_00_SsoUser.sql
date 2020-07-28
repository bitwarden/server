IF OBJECT_ID('[dbo].[SsoUser]') IS NULL
BEGIN
    CREATE TABLE [dbo].[SsoUser] (
        [UserId]            UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
        [ExternalId]        NVARCHAR(50) NOT NULL,
        CONSTRAINT [PK_SsoUser] PRIMARY KEY CLUSTERED ([UserId] ASC, [OrganizationId] ASC),
        CONSTRAINT [FK_SsoUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SsoUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdExternalId]
        ON [dbo].[SsoUser]([OrganizationId] ASC, [ExternalId] ASC)
        INCLUDE ([UserId]);
END
GO

IF OBJECT_ID('[dbo].[SsoUser_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_Create]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_Create]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoUser]
    (
        [UserId],
        [OrganizationId],
        [ExternalId]
    )
    VALUES
    (
        @UserId,
        @OrganizationId,
        @ExternalId
    )
END
GO

IF OBJECT_ID('[dbo].[SsoUser_Delete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_Delete]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_Delete]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] = @UserId
        AND [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[User_ReadBySsoUserOrganizationIdExternalId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadBySsoUserOrganizationIdExternalId]
END
GO

CREATE PROCEDURE [dbo].[User_ReadBySsoUserOrganizationIdExternalId]
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        U.*
    FROM
        [dbo].[UserView] U
    INNER JOIN
        [dbo].[SsoUser] SU ON SU.[UserId] = U.[Id]
    WHERE
        SU.[OrganizationId] = @OrganizationId
        AND SU.[ExternalId] = @ExternalId
END
GO
