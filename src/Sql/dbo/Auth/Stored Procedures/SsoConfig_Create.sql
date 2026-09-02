CREATE PROCEDURE [dbo].[SsoConfig_Create]
    @Id BIGINT OUTPUT,
    @Enabled BIT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoConfig]
    (
        [Enabled],
        [OrganizationId],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Enabled,
        @OrganizationId,
        @Data,
        @CreationDate,
        @RevisionDate
    )

    SET @Id = SCOPE_IDENTITY();
END
