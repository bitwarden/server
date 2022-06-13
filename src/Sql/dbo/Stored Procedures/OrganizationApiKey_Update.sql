CREATE PROCEDURE [dbo].[OrganizationApiKey_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @ApiKey VARCHAR(30),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationApiKey]
    SET
        [ApiKey] = @ApiKey,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
