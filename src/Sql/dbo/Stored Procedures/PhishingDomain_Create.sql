CREATE PROCEDURE [dbo].[PhishingDomain_Create]
    @Id UNIQUEIDENTIFIER,
    @Domain NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PhishingDomain]
    (
        [Id],
        [Domain],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Domain,
        @CreationDate,
        @RevisionDate
    )
END 