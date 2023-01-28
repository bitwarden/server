CREATE PROCEDURE [dbo].[OrganizationConnection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Enabled BIT,
    @Config NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationConnection]
    (
        [Id],
        [OrganizationId],
        [Type],
        [Enabled],
        [Config]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Type,
        @Enabled,
        @Config
    )
END
