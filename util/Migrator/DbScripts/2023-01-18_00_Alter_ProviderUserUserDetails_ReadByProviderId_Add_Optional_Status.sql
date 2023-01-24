CREATE OR ALTER PROCEDURE [dbo].[ProviderUserUserDetails_ReadByProviderId]
@ProviderId UNIQUEIDENTIFIER,
@Status TINYINT = NULL  -- new: this is required to be backwards compatible
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [ProviderId] = @ProviderId
        AND [Status] = COALESCE(@Status, [Status])  -- new
END
GO
