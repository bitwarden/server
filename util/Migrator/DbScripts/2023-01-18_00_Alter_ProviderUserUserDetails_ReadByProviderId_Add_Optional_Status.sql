SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[ProviderUserUserDetails_ReadByProviderId]
@ProviderId UNIQUEIDENTIFIER,
@Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [ProviderId] = @ProviderId
        AND [Status] = COALESCE(@Status, [Status])
END
GO
