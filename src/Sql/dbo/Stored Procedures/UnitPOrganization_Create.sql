CREATE PROCEDURE [dbo].[UnitPOrganization_Create]
    @Id UNIQUEIDENTIFIER,
    @UnitPId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[UnitPOrganization]
    (
        [Id],
        [UnitPId],
        [OrganizationId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UnitPId,
        @OrganizationId,
        @CreationDate,
        @RevisionDate
    )
END
