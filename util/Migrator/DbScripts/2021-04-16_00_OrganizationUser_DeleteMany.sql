-- Create sproc to bump the revision date of a batch of users
IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]
END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.UserId
    INTO
        #UserIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUIds ON OUIds.Id = OU.Id
    WHERE
        OU.[Status] = 2 -- Confirmed

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        #UserIds ON U.[Id] = #UserIds.[UserId]
END
GO

-- Create TwoGuidIdArray Type
IF NOT EXISTS (
    SELECT
    *
FROM
    sys.types
WHERE 
        [Name] = 'TwoGuidIdArray' AND
    is_user_defined = 1
)
CREATE TYPE [dbo].[TwoGuidIdArray] AS TABLE (
    [Id1] UNIQUEIDENTIFIER NOT NULL,
    [Id2] UNIQUEIDENTIFIER NOT NULL);
GO

-- Create sproc to delete batch of users
-- Parameter Ids are UserId, OrganizationId
IF OBJECT_ID('[dbo].[SsoUser_DeleteMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoUser_DeleteMany]
END
GO

CREATE PROCEDURE [dbo].[SsoUser_DeleteMany]
    @UserAndOrganizationIds [dbo].[TwoGuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id
    INTO
        #SSOIds
    FROM
        [dbo].[SsoUser] SU
        INNER JOIN
        @UserAndOrganizationIds UOI ON UOI.Id1 = SU.UserId AND UOI.Id2 = SU.OrganizationId

    DECLARE @BatchSize INT = 100

    -- Delete SSO Users
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION SsoUser_DeleteMany_SsoUsers

        DELETE TOP(@BatchSize) SU
        FROM
            [dbo].[SsoUser] SU
            INNER JOIN
            #SSOIDs ON #SSOIds.Id = SU.Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION SsoUser_DeleteMany_SsoUsers
    END
END
GO

-- Create OrganizationUser Delete many by Id procedure
IF OBJECT_ID('[dbo].[OrganizationUser_DeleteByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
END
GO

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

    DELETE CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        @Ids I ON I.Id = CU.OrganizationUserId

    DELETE GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        @IDs I ON I.Id = GU.OrganizationUserId

    DELETE OU
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @IDs I ON I.Id = OU.Id
END
GO
