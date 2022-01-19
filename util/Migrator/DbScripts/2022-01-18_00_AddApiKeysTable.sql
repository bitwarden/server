-- Create Organization ApiKey table
IF OBJECT_ID('[dbo].[OrganizationApiKey]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationApiKey] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [ApiKey]            VARCHAR(30) NOT NULL,
    CONSTRAINT [PK_OrganizationApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationApiKey_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
END
GO

-- Create indexes
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationApiKey_OrganizationId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationApiKey_OrganizationId]
    ON [dbo].[OrganizationApiKey]([OrganizationId] ASC);
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationApiKey_ApiKey')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationApiKey_ApiKey]
    ON [dbo].[OrganizationApiKey]([ApiKey] ASC);
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationApiKeyView')
BEGIN
    DROP VIEW [dbo].[OrganizationApiKeyView];
END
GO

CREATE VIEW [dbo].[OrganizationApiKeyView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationApiKey]
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_ReadById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationApiKey]
    (
        [Id],
        [OrganizationId],
        [ApiKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ApiKey
    )
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationApiKey]
    SET
        [OrganizationId] = @OrganizationId,
        [ApiKey] = @ApiKey
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationApiKey]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_ReadCanUseByOrganizationIdApiKey]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_ReadCanUseByOrganizationIdApiKey]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadCanUseByOrganizationIdApiKey]
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanUse BIT

    SELECT
        @CanUse = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [ApiKey] = @ApiKey
END
GO

IF OBJECT_ID('[dbo].[OrganizationApiKey_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationApiKey_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

-- Update Organization delete sprocs to handle organization api key
IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM 
        [dbo].[CollectionUser] CU
    INNER JOIN 
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE 
        [OU].[OrganizationId] = @Id

    DELETE
    FROM 
        [dbo].[OrganizationUser]
    WHERE 
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO
