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
        [Id],
        [Enabled],
        [OrganizationId],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Enabled,
        @OrganizationId,
        @Data,
        @CreationDate,
        @RevisionDate
    )

    SET @Id = SCOPE_IDENTITY();
END
