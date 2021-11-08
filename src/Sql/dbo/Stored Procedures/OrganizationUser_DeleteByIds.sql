CREATE PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @Ids

    DECLARE @UserAndOrganizationIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndOrganizationIds
        (Id1, Id2)
    SELECT
        UserId,
        OrganizationId
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @Ids OUIds ON OUIds.Id = OU.Id
    WHERE
        UserId IS NOT NULL AND
        OrganizationId IS NOT NULL

    BEGIN
        EXEC [dbo].[SsoUser_DeleteMany] @UserAndOrganizationIds
    END

    DECLARE @BatchSize INT = 100

    -- Delete CollectionUsers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION CollectionUser_DeleteMany_CUs

        DELETE TOP(@BatchSize) CU
        FROM
            [dbo].[CollectionUser] CU
        INNER JOIN
            @Ids I ON I.Id = CU.OrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION CollectionUser_DeleteMany_CUs
    END

    SET @BatchSize = 100;

    -- Delete GroupUsers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION GroupUser_DeleteMany_GroupUsers

        DELETE TOP(@BatchSize) GU
        FROM
            [dbo].[GroupUser] GU
        INNER JOIN
            @Ids I ON I.Id = GU.OrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION GoupUser_DeleteMany_GroupUsers
    END

    EXEC [dbo].[OrganizationSponsorship_OrganizationUsersDeleted] @Ids
    
    SET @BatchSize = 100;

    -- Delete OrganizationUsers
    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION OrganizationUser_DeleteMany_OUs

        DELETE TOP(@BatchSize) OU
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            @Ids I ON I.Id = OU.Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION OrganizationUser_DeleteMany_OUs
    END
END
GO
