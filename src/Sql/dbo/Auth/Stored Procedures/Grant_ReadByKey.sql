CREATE PROCEDURE [dbo].[Grant_ReadByKey]
    @Key NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GrantView]
    WHERE
        [Key] = @Key
END
