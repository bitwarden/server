CREATE PROCEDURE [dbo].[CipherDetails_ReadByTypeUserId]
    @Type TINYINT,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherDetailsView]
    WHERE
        [Type] = @Type
        AND [UserId] = @UserId
END