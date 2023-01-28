CREATE PROCEDURE [dbo].[OrganizationApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApiKey VARCHAR(30),
    @Type TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationApiKey]
    (
        [Id],
        [OrganizationId],
        [ApiKey],
        [Type],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ApiKey,
        @Type,
        @RevisionDate
    )
END
