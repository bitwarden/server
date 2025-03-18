CREATE PROCEDURE [dbo].[PhishingDomain_Create]
    @Id UNIQUEIDENTIFIER,
    @Domain NVARCHAR(255),
    @Checksum NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PhishingDomain]
    (
        [Id],
        [Domain],
        [Checksum]
    )
    VALUES
    (
        @Id,
        @Domain,
        @Checksum
    )
END 