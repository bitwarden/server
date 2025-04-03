-- Configure FK to cascade on delete
IF EXISTS(SELECT *
FROM information_schema.table_constraints
WHERE table_name='OrganizationIntegrationConfiguration'
    AND constraint_name='FK_OrganizationIntegrationConfiguration_OrganizationIntegration')
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration] DROP FK_OrganizationIntegrationConfiguration_OrganizationIntegration;
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration] ADD CONSTRAINT [FK_OrganizationIntegrationConfiguration_OrganizationIntegration] FOREIGN KEY ([OrganizationIntegrationId]) REFERENCES [dbo].[OrganizationIntegration] ([Id]) ON DELETE CASCADE;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegration]
        (
        [Id],
        [OrganizationId],
        [Type],
        [Configuration],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Type,
            @Configuration,
            @CreationDate,
            @RevisionDate
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegrationConfiguration]
        (
        [Id],
        [OrganizationIntegrationId],
        [EventType],
        [Configuration],
        [Template],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationIntegrationId,
            @EventType,
            @Configuration,
            @Template,
            @CreationDate,
            @RevisionDate
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Configuration] = @Configuration,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [OrganizationIntegrationId] = @OrganizationIntegrationId,
        [EventType] = @EventType,
        [Configuration] = @Configuration,
        [Template] = @Template,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
GO
