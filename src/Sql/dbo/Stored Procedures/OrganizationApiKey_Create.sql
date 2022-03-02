CREATE PROCEDURE [dbo].[OrganizationApiKey_Create]
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30),
    @Type TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationApiKey]
    (
        [OrganizationId],
        [ApiKey],
        [Type],
        [RevisionDate]
    )
    VALUES
    (
        @OrganizationId,
        @ApiKey,
        @Type,
        @RevisionDate
    )
END
