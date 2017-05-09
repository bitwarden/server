CREATE PROCEDURE [dbo].[GroupUserUserDetails_ReadByGroupId]
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GroupUserUserDetailsView]
    WHERE
        [GroupId] = @GroupId
END
