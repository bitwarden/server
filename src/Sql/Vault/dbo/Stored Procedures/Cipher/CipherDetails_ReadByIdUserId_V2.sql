CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
    WHERE
        [Id] = @Id
    ORDER BY
        [Edit] DESC
END
