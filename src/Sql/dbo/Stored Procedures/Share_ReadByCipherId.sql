CREATE PROCEDURE [dbo].[Share_ReadByCipherId]
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ShareView]
    WHERE
        [CipherId] = @CipherId
END
