CREATE PROCEDURE [dbo].[ProviderUser_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderUserIds] @Ids

    DECLARE @UserAndProviderIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndProviderIds
        (Id1, Id2)
    SELECT
        UserId,
        ProviderId
    FROM
        [dbo].[ProviderUser] PU
    INNER JOIN
        @Ids PUIds ON PUIds.Id = PU.Id
    WHERE
        UserId IS NOT NULL AND
        ProviderId IS NOT NULL

    DECLARE @BatchSize INT = 100

    -- Delete ProviderUsers
    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION ProviderUser_DeleteMany_PUs

        DELETE TOP(@BatchSize) PU
        FROM
            [dbo].[ProviderUser] PU
        INNER JOIN
            @Ids I ON I.Id = PU.Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION ProviderUser_DeleteMany_PUs
    END
END
GO
