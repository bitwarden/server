CREATE PROCEDURE [dbo].[User_ReadByPremium]
    @Premium BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Premium] = @Premium
END