IF OBJECT_ID('[dbo].[SsoUser]') IS NULL
BEGIN
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

    CREATE UNIQUE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdExternalId]
        ON [dbo].[SsoUser]([OrganizationId] ASC, [ExternalId] ASC)
        INCLUDE ([UserId]);

    CREATE UNIQUE NONCLUSTERED INDEX [IX_SsoUser_OrganizationIdUserId]
        ON [dbo].[SsoUser]([OrganizationId] ASC, [UserId] ASC);
END
GO

IF OBJECT_ID('[dbo].[SsoUser_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_ReadById]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoUserView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[SsoUser_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_Create]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_Create]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoUser]
    (
        [UserId],
        [OrganizationId],
        [ExternalId],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @OrganizationId,
        @ExternalId,
        @CreationDate
    )

    SET @Id = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('[dbo].[SsoUser_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_Update]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_Update]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SsoUser]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
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
        (
            (@OrganizationId IS NULL AND SU.[OrganizationId] IS NULL)
            OR (@OrganizationId IS NOT NULL AND SU.[OrganizationId] = @OrganizationId)
        )
        AND SU.[ExternalId] = @ExternalId
END
GO
