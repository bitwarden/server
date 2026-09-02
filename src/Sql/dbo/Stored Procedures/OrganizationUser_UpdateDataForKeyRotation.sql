CREATE PROCEDURE [dbo].[OrganizationUser_UpdateDataForKeyRotation]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationUserJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Parse the JSON string and insert into a temporary table
    DECLARE @OrganizationUserInput AS TABLE (
        [Id] UNIQUEIDENTIFIER,
        [ResetPasswordKey] VARCHAR(MAX),
        [V2UpgradeToken] VARCHAR(MAX)
    )

    INSERT INTO @OrganizationUserInput
    SELECT
        [Id],
        [ResetPasswordKey],
        [V2UpgradeToken]
    FROM OPENJSON(@OrganizationUserJson)
    WITH (
        [Id] UNIQUEIDENTIFIER '$.Id',
        [ResetPasswordKey] VARCHAR(MAX) '$.ResetPasswordKey',
        [V2UpgradeToken] VARCHAR(MAX) '$.V2UpgradeToken'
    )

    -- Perform the update
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [ResetPasswordKey] = OUI.[ResetPasswordKey],
        [V2UpgradeToken] = OUI.[V2UpgradeToken]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.Id = OUI.Id
    WHERE
        OU.[UserId] = @UserId

END
