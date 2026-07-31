-- Revert AccessRequest.RejectedDate: an audit-support field that only fed the retired synthesized projection. Refused
-- activations are now recorded by the written LeaseActivationRejected event, so drop the field, its writer proc, and
-- the @RejectedDate parameter on AccessRequest_Create. The recreated proc keeps @RuleId (added by
-- 2026-07-03_00_PamPinRuleOnAccessRequest, which runs before this). Dapper/MSSQL only.

DROP PROCEDURE IF EXISTS [dbo].[AccessRequest_MarkActivationRejected]
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @ResolvedDate DATETIME2(7) = NULL,
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
        [Status],
        [CreationDate],
        [ResolvedDate],
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
        @Status,
        @CreationDate,
        @ResolvedDate,
        @RuleId
    )
END
GO

IF COL_LENGTH('[dbo].[AccessRequest]', 'RejectedDate') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequest] DROP COLUMN [RejectedDate];
END
GO
