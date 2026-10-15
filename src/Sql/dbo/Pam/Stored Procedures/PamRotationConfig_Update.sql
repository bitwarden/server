CREATE PROCEDURE [dbo].[PamRotationConfig_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
    @WrappedKey NVARCHAR(MAX),
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

    UPDATE
        [dbo].[PamRotationConfig]
    SET
        [OrganizationId] = @OrganizationId,
        [CipherId] = @CipherId,
        [TargetSystemId] = @TargetSystemId,
        [SealedData] = @SealedData,
        [WrappedKey] = @WrappedKey,
        [ScheduleCron] = @ScheduleCron,
        [RotateOnAccessEnd] = @RotateOnAccessEnd,
        [NextRotationDate] = @NextRotationDate,
        [Enabled] = @Enabled,
        [LastRotationDate] = @LastRotationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
