CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_Update]
    @Id                        UNIQUEIDENTIFIER,
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

    UPDATE
        [dbo].[OrganizationPartnershipReconciliationExport]
    SET
        [OrganizationPartnershipId] = @OrganizationPartnershipId,
        [Status] = @Status,
        [RecordCount] = @RecordCount,
        [BlobName] = @BlobName,
        [CreationDate] = @CreationDate,
        [CompletionDate] = @CompletionDate,
        [ExpirationDate] = @ExpirationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
