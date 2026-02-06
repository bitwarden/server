CREATE PROCEDURE [dbo].[OrganizationApplication_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationApplication]
    (
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate],
        [ContentEncryptionKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Applications,
        @CreationDate,
        @RevisionDate,
        @ContentEncryptionKey
    );
