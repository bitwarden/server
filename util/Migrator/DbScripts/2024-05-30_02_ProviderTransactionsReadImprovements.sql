IF OBJECT_ID('[dbo].[Transaction_ReadByProviderId]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Transaction_ReadByProviderId]
    END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [ProviderId] = @ProviderId
    ORDER BY
        [CreationDate] DESC
END
