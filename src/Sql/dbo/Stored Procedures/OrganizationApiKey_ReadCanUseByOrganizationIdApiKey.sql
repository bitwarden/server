CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadCanUseByOrganizationIdApiKey]
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30),
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanUse BIT

    SELECT
        @CanUse = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [ApiKey] = @ApiKey AND
        [Type] = @Type
END