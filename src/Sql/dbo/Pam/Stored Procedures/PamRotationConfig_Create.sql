CREATE PROCEDURE [dbo].[PamRotationConfig_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX) = NULL,
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationDate DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationDate DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamRotationConfig]
    (
        [Id],
        [OrganizationId],
        [CipherId],
        [TargetSystemId],
        [SealedData],
        [WrappedKey],
        [ScheduleCron],
        [RotateOnAccessEnd],
        [NextRotationDate],
        [Enabled],
        [LastRotationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CipherId,
        @TargetSystemId,
        @SealedData,
        @WrappedKey,
        @ScheduleCron,
        @RotateOnAccessEnd,
        @NextRotationDate,
        @Enabled,
        @LastRotationDate,
        @CreationDate,
        @RevisionDate
    )
END
