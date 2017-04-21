CREATE PROCEDURE [dbo].[SubvaultUserUserDetails_ReadBySubvaultId]
    @SubvaultId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubvaultUserUserDetailsView]
    WHERE
        [AccessAllSubvaults] = 1 
        OR [SubvaultId] = @SubvaultId
END