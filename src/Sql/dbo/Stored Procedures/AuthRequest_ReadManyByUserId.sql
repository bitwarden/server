CREATE PROCEDURE [dbo].[AuthRequest_ReadManyByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AuthRequestView]
    WHERE
        [UserId] = @UserId
END
