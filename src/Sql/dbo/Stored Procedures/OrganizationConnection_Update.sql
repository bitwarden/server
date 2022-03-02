CREATE PROCEDURE [dbo].[OrganizationConnection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Enabled BIT,
    @Config NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationConnection]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Enabled] = @Enabled,
        [Config] = @Config
    WHERE
        [Id] = @Id
END
