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

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[OrganizationPasswordManager]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationSecretsManager]
    WHERE
        [OrganizationId] = @Id

   DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

IF EXISTS (
 SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'Plan' AND
        DATA_TYPE = 'NVARCHAR' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN [Plan] NVARCHAR(50) NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'PlanType' AND
        DATA_TYPE = 'tinyint' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN PlanType tinyint NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'UseTotp' AND
        DATA_TYPE = 'bit' AND
        TABLE_NAME = 'Organization' AND
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN UseTotp bit NULL
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'UsersGetPremium' AND
        DATA_TYPE = 'bit' AND
        TABLE_NAME = 'Organization' AND 
        IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE [dbo].[Organization]
    ALTER COLUMN UsersGetPremium bit NULL
END