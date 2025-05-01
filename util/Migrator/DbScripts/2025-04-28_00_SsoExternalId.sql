IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'SsoUser'
      AND COLUMN_NAME = 'ExternalId'
      AND DATA_TYPE = 'nvarchar'
      AND CHARACTER_MAXIMUM_LENGTH < 300
)
BEGIN
    -- Update table ExternalId column size
    ALTER TABLE [dbo].[SsoUser]
    ALTER COLUMN [ExternalId] NVARCHAR(300) NOT NULL
END
GO

-- Update stored procedures to handle the new ExternalId column size
CREATE OR ALTER PROCEDURE [dbo].[SsoUser_Create]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoUser]
    (
        [UserId],
        [OrganizationId],
        [ExternalId],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @OrganizationId,
        @ExternalId,
        @CreationDate
    )

    SET @Id = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SsoUser_Update]
    @Id BIGINT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SsoUser]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END
GO
