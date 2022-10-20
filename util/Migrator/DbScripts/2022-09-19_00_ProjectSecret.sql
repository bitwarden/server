CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM 
        [dbo].[CollectionUser] CU
    INNER JOIN 
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE 
        [OU].[OrganizationId] = @Id

    DELETE
    FROM 
        [dbo].[OrganizationUser]
    WHERE 
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Project]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[Secret]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

-- Update project and secret table to NOT on delete cascade anymore
IF EXISTS (SELECT  name
                FROM    sys.foreign_keys
                WHERE   name = 'FK_Project_Organization') 
BEGIN
    ALTER TABLE [Project] DROP CONSTRAINT FK_Project_Organization;
END 

ALTER TABLE [Project] ADD CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]);

IF EXISTS (SELECT  name
                FROM    sys.foreign_keys
                WHERE   name = 'FK_Secret_OrganizationId') 
BEGIN
    ALTER TABLE [Secret] DROP CONSTRAINT FK_Secret_OrganizationId;
END

ALTER TABLE [Secret] ADD CONSTRAINT [FK_Secret_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]);

IF OBJECT_ID('[dbo].[ProjectSecret]') IS NULL
BEGIN
  CREATE TABLE [ProjectSecret] (
      [ProjectsId] UNIQUEIDENTIFIER NOT NULL,
      [SecretsId]  UNIQUEIDENTIFIER NOT NULL,
       CONSTRAINT [PK_ProjectSecret] PRIMARY KEY ([ProjectsId], [SecretsId]),
      CONSTRAINT [FK_ProjectSecret_Project_ProjectsId] FOREIGN KEY ([ProjectsId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
      CONSTRAINT [FK_ProjectSecret_Secret_SecretsId] FOREIGN KEY ([SecretsId]) REFERENCES [Secret] ([Id]) ON DELETE CASCADE
  );

  CREATE INDEX [IX_ProjectSecret_SecretsId] ON [ProjectSecret] ([SecretsId]);

END

GO

