CREATE PROCEDURE [dbo].[Grant_DeleteByKey]
    @Key NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [Key] = @Key
END
