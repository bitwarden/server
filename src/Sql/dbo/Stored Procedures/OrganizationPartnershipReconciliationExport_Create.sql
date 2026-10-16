CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_Create]
    @Id                        UNIQUEIDENTIFIER OUTPUT,
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @Status                    TINYINT,
    @RecordCount               INT,
    @BlobName                  NVARCHAR(256),
    @CreationDate              DATETIME2(7),
    @CompletionDate            DATETIME2(7),
    @ExpirationDate            DATETIME2(7),
    @RevisionDate              DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnershipReconciliationExport]
    (
        [Id],
        [OrganizationPartnershipId],
        [Status],
        [RecordCount],
        [BlobName],
        [CreationDate],
        [CompletionDate],
        [ExpirationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationPartnershipId,
        @Status,
        @RecordCount,
        @BlobName,
        @CreationDate,
        @CompletionDate,
        @ExpirationDate,
        @RevisionDate
    )
END
