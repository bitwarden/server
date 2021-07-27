CREATE PROCEDURE [dbo].[ProviderUserProviderDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserProviderDetailsView]
    WHERE
        [UserId] = @UserId
        AND [ProviderStatus] != 0 -- Not Pending
        AND (@Status IS NULL OR [Status] = @Status)
END
