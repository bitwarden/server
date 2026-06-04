CREATE PROCEDURE [dbo].[LeaseRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @LeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @ResolvedDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[LeaseRequest]
    (
        [Id],
        [LeaseId],
        [OrganizationId],
        [CollectionId],
        [CipherId],
        [RequesterId],
        [NotBefore],
        [NotAfter],
        [Reason],
        [Status],
        [CreationDate],
        [ResolvedDate]
    )
    VALUES
    (
        @Id,
        @LeaseId,
        @OrganizationId,
        @CollectionId,
        @CipherId,
        @RequesterId,
        @NotBefore,
        @NotAfter,
        @Reason,
        @Status,
        @CreationDate,
        @ResolvedDate
    )
END
