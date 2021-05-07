CREATE PROCEDURE [dbo].[UnitPUser_ReadByUnitId]
    @UnitPId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UnitPUserView]
    WHERE
        [UnitPId] = @UnitPId
        AND (@Type IS NULL OR [Type] = @Type)
END
