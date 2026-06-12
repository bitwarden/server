CREATE PROCEDURE [dbo].[OrganizationEventCleanup_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationEventCleanup]
    (
        [Id],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CreationDate
    )
END
