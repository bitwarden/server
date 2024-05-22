IF OBJECT_ID('[dbo].[Transaction_ReadByUserId]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Transaction_ReadByUserId]
    END
GO

CREATE PROCEDURE [dbo].[Transaction_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] = @UserId
    ORDER BY
        [CreationDate] DESC
END
