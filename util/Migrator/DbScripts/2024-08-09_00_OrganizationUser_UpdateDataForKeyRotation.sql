CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateDataForKeyRotation]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationUserJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Parse the JSON string and insert into a temporary table
    DECLARE @OrganizationUserInput AS TABLE (
        [Id] UNIQUEIDENTIFIER,
        [ResetPasswordKey] VARCHAR(MAX)
    )

    INSERT INTO @OrganizationUserInput
    SELECT
        [Id],
        [ResetPasswordKey]
    FROM OPENJSON(@OrganizationUserJson)
    WITH (
        [Id] UNIQUEIDENTIFIER '$.Id',
        [ResetPasswordKey] VARCHAR(MAX) '$.ResetPasswordKey'
    )

    -- Perform the update
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [ResetPasswordKey] = OUI.[ResetPasswordKey]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.Id = OUI.Id
    WHERE
        OU.[UserId] = @UserId

END
GO
