IF COLUMNPROPERTY(OBJECT_ID('[dbo].[OrganizationSponsorship]', 'U'), 'SponsoringOrganizationId', 'AllowsNull') = 0
BEGIN
    DROP INDEX [IX_OrganizationSponsorship_SponsoringOrganizationId]
    ON [dbo].[OrganizationSponsorship]

    ALTER TABLE [dbo].[OrganizationSponsorship]
    ALTER COLUMN [SponsoringOrganizationId] UNIQUEIDENTIFIER NULL;

    CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationId] ASC)
    WHERE [SponsoringOrganizationId] IS NOT NULL;
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationDeleted') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoringOrganizationId] = NULL
    WHERE
        [SponsoringOrganizationId] = @OrganizationId

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = NULL
    WHERE
        [SponsoredOrganizationId] = @OrganizationId
END
GO

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

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO
