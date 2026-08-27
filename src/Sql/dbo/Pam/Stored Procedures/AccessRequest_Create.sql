CREATE PROCEDURE [dbo].[AccessRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Action TINYINT,
    @CreationDate DATETIME2(7),
    @ActionDate DATETIME2(7) = NULL,
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id],
        [ExtensionOfLeaseId],
        [OrganizationId],
        [CollectionId],
        [CipherId],
        [RequesterId],
        [NotBefore],
        [NotAfter],
        [Reason],
        [Action],
        [CreationDate],
        [ActionDate],
        [RuleId]
    )
    VALUES
    (
        @Id,
        @ExtensionOfLeaseId,
        @OrganizationId,
        @CollectionId,
        @CipherId,
        @RequesterId,
        @NotBefore,
        @NotAfter,
        @Reason,
        @Action,
        @CreationDate,
        @ActionDate,
        @RuleId
    )
END
