CREATE OR ALTER PROCEDURE [dbo].[Send_ReadFilesByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [OrganizationId] IS NULL
        AND [UserId] = @UserId
        AND [Type] = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_ReadFilesByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Type] = 1
END
GO
