SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;

SET NUMERIC_ROUNDABORT OFF;


GO
PRINT N'Creating [dbo].[GuidIdArray]...';


GO
CREATE TYPE [dbo].[GuidIdArray] AS TABLE (
    [Id] UNIQUEIDENTIFIER NOT NULL);


GO
PRINT N'Creating [dbo].[SelectionReadOnlyArray]...';


GO
CREATE TYPE [dbo].[SelectionReadOnlyArray] AS TABLE (
    [Id]       UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly] BIT              NOT NULL);


GO
PRINT N'Creating [dbo].[Cipher]...';


GO
CREATE TABLE [dbo].[Cipher] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           NVARCHAR (MAX)   NOT NULL,
    [Favorites]      NVARCHAR (MAX)   NULL,
    [Folders]        NVARCHAR (MAX)   NULL,
    [Attachments]    NVARCHAR (MAX)   NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[Cipher].[IX_Cipher_OrganizationId_Type]...';


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_OrganizationId_Type]
    ON [dbo].[Cipher]([OrganizationId] ASC, [Type] ASC) WHERE ([OrganizationId] IS NOT NULL);


GO
PRINT N'Creating [dbo].[Cipher].[IX_Cipher_UserId_Type]...';


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_Type]
    ON [dbo].[Cipher]([UserId] ASC, [Type] ASC);


GO
PRINT N'Creating [dbo].[Collection]...';


GO
CREATE TABLE [dbo].[Collection] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           VARCHAR (MAX)    NOT NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Collection] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[CollectionCipher]...';


GO
CREATE TABLE [dbo].[CollectionCipher] (
    [CollectionId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId]     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_CollectionCipher] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [CipherId] ASC)
);


GO
PRINT N'Creating [dbo].[CollectionCipher].[IX_CollectionCipher_CipherId]...';


GO
CREATE NONCLUSTERED INDEX [IX_CollectionCipher_CipherId]
    ON [dbo].[CollectionCipher]([CipherId] ASC);


GO
PRINT N'Creating [dbo].[CollectionGroup]...';


GO
CREATE TABLE [dbo].[CollectionGroup] (
    [CollectionId] UNIQUEIDENTIFIER NOT NULL,
    [GroupId]      UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]     BIT              NOT NULL,
    CONSTRAINT [PK_CollectionGroup] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [GroupId] ASC)
);


GO
PRINT N'Creating [dbo].[CollectionUser]...';


GO
CREATE TABLE [dbo].[CollectionUser] (
    [CollectionId]       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    [ReadOnly]           BIT              NOT NULL,
    CONSTRAINT [PK_CollectionUser] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [OrganizationUserId] ASC)
);


GO
PRINT N'Creating [dbo].[Device]...';


GO
CREATE TABLE [dbo].[Device] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         NVARCHAR (50)    NOT NULL,
    [Type]         SMALLINT         NOT NULL,
    [Identifier]   NVARCHAR (50)    NOT NULL,
    [PushToken]    NVARCHAR (255)   NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Device] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[Device].[UX_Device_UserId_Identifier]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Device_UserId_Identifier]
    ON [dbo].[Device]([UserId] ASC, [Identifier] ASC);


GO
PRINT N'Creating [dbo].[Folder]...';


GO
CREATE TABLE [dbo].[Folder] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         VARCHAR (MAX)    NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Folder] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[Grant]...';


GO
CREATE TABLE [dbo].[Grant] (
    [Key]            NVARCHAR (200) NOT NULL,
    [Type]           NVARCHAR (50)  NULL,
    [SubjectId]      NVARCHAR (50)  NULL,
    [ClientId]       NVARCHAR (50)  NOT NULL,
    [CreationDate]   DATETIME2 (7)  NOT NULL,
    [ExpirationDate] DATETIME2 (7)  NULL,
    [Data]           NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_Grant] PRIMARY KEY CLUSTERED ([Key] ASC)
);


GO
PRINT N'Creating [dbo].[Grant].[IX_Grant_SubjectId_ClientId_Type]...';


GO
CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_ClientId_Type]
    ON [dbo].[Grant]([SubjectId] ASC, [ClientId] ASC, [Type] ASC);


GO
PRINT N'Creating [dbo].[Group]...';


GO
CREATE TABLE [dbo].[Group] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR (50)    NOT NULL,
    [AccessAll]      BIT              NOT NULL,
    [ExternalId]     NVARCHAR (300)   NULL,
    [CreationDate]   DATETIME         NOT NULL,
    [RevisionDate]   DATETIME         NOT NULL,
    CONSTRAINT [PK_Group] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[GroupUser]...';


GO
CREATE TABLE [dbo].[GroupUser] (
    [GroupId]            UNIQUEIDENTIFIER NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_GroupUser] PRIMARY KEY CLUSTERED ([GroupId] ASC, [OrganizationUserId] ASC)
);


GO
PRINT N'Creating [dbo].[Installation]...';


GO
CREATE TABLE [dbo].[Installation] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [Email]        NVARCHAR (50)    NOT NULL,
    [Key]          VARCHAR (150)    NOT NULL,
    [Enabled]      BIT              NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Installation] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[Organization]...';


GO
CREATE TABLE [dbo].[Organization] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Name]                  NVARCHAR (50)    NOT NULL,
    [BusinessName]          NVARCHAR (50)    NULL,
    [BillingEmail]          NVARCHAR (50)    NOT NULL,
    [Plan]                  NVARCHAR (50)    NOT NULL,
    [PlanType]              TINYINT          NOT NULL,
    [Seats]                 SMALLINT         NULL,
    [MaxCollections]        SMALLINT         NULL,
    [UseGroups]             BIT              NOT NULL,
    [UseDirectory]          BIT              NOT NULL,
    [UseTotp]               BIT              NOT NULL,
    [SelfHost]              BIT              NOT NULL,
    [Storage]               BIGINT           NULL,
    [MaxStorageGb]          SMALLINT         NULL,
    [Gateway]               TINYINT          NULL,
    [GatewayCustomerId]     VARCHAR (50)     NULL,
    [GatewaySubscriptionId] VARCHAR (50)     NULL,
    [Enabled]               BIT              NOT NULL,
    [LicenseKey]            VARCHAR (100)    NULL,
    [ExpirationDate]        DATETIME2 (7)    NULL,
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Organization] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[OrganizationUser]...';


GO
CREATE TABLE [dbo].[OrganizationUser] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [Email]          NVARCHAR (50)    NULL,
    [Key]            VARCHAR (MAX)    NULL,
    [Status]         TINYINT          NOT NULL,
    [Type]           TINYINT          NOT NULL,
    [AccessAll]      BIT              NOT NULL,
    [ExternalId]     NVARCHAR (300)   NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrganizationUser] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[U2f]...';


GO
CREATE TABLE [dbo].[U2f] (
    [Id]           INT              IDENTITY (1, 1) NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [KeyHandle]    VARCHAR (200)    NULL,
    [Challenge]    VARCHAR (200)    NOT NULL,
    [AppId]        VARCHAR (50)     NOT NULL,
    [Version]      VARCHAR (20)     NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_U2f] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[User]...';


GO
CREATE TABLE [dbo].[User] (
    [Id]                              UNIQUEIDENTIFIER NOT NULL,
    [Name]                            NVARCHAR (50)    NULL,
    [Email]                           NVARCHAR (50)    NOT NULL,
    [EmailVerified]                   BIT              NOT NULL,
    [MasterPassword]                  NVARCHAR (300)   NOT NULL,
    [MasterPasswordHint]              NVARCHAR (50)    NULL,
    [Culture]                         NVARCHAR (10)    NOT NULL,
    [SecurityStamp]                   NVARCHAR (50)    NOT NULL,
    [TwoFactorProviders]              NVARCHAR (MAX)   NULL,
    [TwoFactorRecoveryCode]           NVARCHAR (32)    NULL,
    [EquivalentDomains]               NVARCHAR (MAX)   NULL,
    [ExcludedGlobalEquivalentDomains] NVARCHAR (MAX)   NULL,
    [AccountRevisionDate]             DATETIME2 (7)    NOT NULL,
    [Key]                             VARCHAR (MAX)    NULL,
    [PublicKey]                       VARCHAR (MAX)    NULL,
    [PrivateKey]                      VARCHAR (MAX)    NULL,
    [Premium]                         BIT              NOT NULL,
    [PremiumExpirationDate]           DATETIME2 (7)    NULL,
    [Storage]                         BIGINT           NULL,
    [MaxStorageGb]                    SMALLINT         NULL,
    [Gateway]                         TINYINT          NULL,
    [GatewayCustomerId]               VARCHAR (50)     NULL,
    [GatewaySubscriptionId]           VARCHAR (50)     NULL,
    [LicenseKey]                      VARCHAR (100)    NULL,
    [CreationDate]                    DATETIME2 (7)    NOT NULL,
    [RevisionDate]                    DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Creating [dbo].[User].[IX_User_Email]...';


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email]
    ON [dbo].[User]([Email] ASC);


GO
PRINT N'Creating [dbo].[FK_Cipher_Organization]...';


GO
ALTER TABLE [dbo].[Cipher] WITH NOCHECK
    ADD CONSTRAINT [FK_Cipher_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]);


GO
PRINT N'Creating [dbo].[FK_Cipher_User]...';


GO
ALTER TABLE [dbo].[Cipher] WITH NOCHECK
    ADD CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Creating [dbo].[FK_Collection_Organization]...';


GO
ALTER TABLE [dbo].[Collection] WITH NOCHECK
    ADD CONSTRAINT [FK_Collection_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_CollectionCipher_Cipher]...';


GO
ALTER TABLE [dbo].[CollectionCipher] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionCipher_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_CollectionCipher_Collection]...';


GO
ALTER TABLE [dbo].[CollectionCipher] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionCipher_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_CollectionGroup_Collection]...';


GO
ALTER TABLE [dbo].[CollectionGroup] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionGroup_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]);


GO
PRINT N'Creating [dbo].[FK_CollectionGroup_Group]...';


GO
ALTER TABLE [dbo].[CollectionGroup] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionGroup_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_CollectionUser_Collection]...';


GO
ALTER TABLE [dbo].[CollectionUser] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionUser_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_CollectionUser_OrganizationUser]...';


GO
ALTER TABLE [dbo].[CollectionUser] WITH NOCHECK
    ADD CONSTRAINT [FK_CollectionUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id]);


GO
PRINT N'Creating [dbo].[FK_Device_User]...';


GO
ALTER TABLE [dbo].[Device] WITH NOCHECK
    ADD CONSTRAINT [FK_Device_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Creating [dbo].[FK_Folder_User]...';


GO
ALTER TABLE [dbo].[Folder] WITH NOCHECK
    ADD CONSTRAINT [FK_Folder_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Creating [dbo].[FK_Group_Organization]...';


GO
ALTER TABLE [dbo].[Group] WITH NOCHECK
    ADD CONSTRAINT [FK_Group_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_GroupUser_Group]...';


GO
ALTER TABLE [dbo].[GroupUser] WITH NOCHECK
    ADD CONSTRAINT [FK_GroupUser_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_GroupUser_OrganizationUser]...';


GO
ALTER TABLE [dbo].[GroupUser] WITH NOCHECK
    ADD CONSTRAINT [FK_GroupUser_OrganizationUser] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id]);


GO
PRINT N'Creating [dbo].[FK_OrganizationUser_Organization]...';


GO
ALTER TABLE [dbo].[OrganizationUser] WITH NOCHECK
    ADD CONSTRAINT [FK_OrganizationUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Creating [dbo].[FK_OrganizationUser_User]...';


GO
ALTER TABLE [dbo].[OrganizationUser] WITH NOCHECK
    ADD CONSTRAINT [FK_OrganizationUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Creating [dbo].[FK_U2f_User]...';


GO
ALTER TABLE [dbo].[U2f] WITH NOCHECK
    ADD CONSTRAINT [FK_U2f_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Creating [dbo].[CipherView]...';


GO
CREATE VIEW [dbo].[CipherView]
AS
SELECT
    *
FROM
    [dbo].[Cipher]
GO
PRINT N'Creating [dbo].[CollectionView]...';


GO
CREATE VIEW [dbo].[CollectionView]
AS
SELECT
    *
FROM
    [dbo].[Collection]
GO
PRINT N'Creating [dbo].[DeviceView]...';


GO
CREATE VIEW [dbo].[DeviceView]
AS
SELECT
    *
FROM
    [dbo].[Device]
GO
PRINT N'Creating [dbo].[FolderView]...';


GO
CREATE VIEW [dbo].[FolderView]
AS
SELECT
    *
FROM
    [dbo].[Folder]
GO
PRINT N'Creating [dbo].[GrantView]...';


GO
CREATE VIEW [dbo].[GrantView]
AS
SELECT
    *
FROM
    [dbo].[Grant]
GO
PRINT N'Creating [dbo].[GroupView]...';


GO
CREATE VIEW [dbo].[GroupView]
AS
SELECT
    *
FROM
    [dbo].[Group]
GO
PRINT N'Creating [dbo].[InstallationView]...';


GO
CREATE VIEW [dbo].[InstallationView]
AS
SELECT
    *
FROM
    [dbo].[Installation]
GO
PRINT N'Creating [dbo].[OrganizationUserOrganizationDetailsView]...';


GO
CREATE VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    O.[Name],
    O.[Enabled],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseTotp],
    O.[Seats],
    O.[MaxCollections],
    O.[MaxStorageGb],
    OU.[Key],
    OU.[Status],
    OU.[Type]
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
GO
PRINT N'Creating [dbo].[OrganizationUserUserDetailsView]...';


GO
CREATE VIEW [dbo].[OrganizationUserUserDetailsView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[ExternalId]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
GO
PRINT N'Creating [dbo].[OrganizationUserView]...';


GO
CREATE VIEW [dbo].[OrganizationUserView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationUser]
GO
PRINT N'Creating [dbo].[OrganizationView]...';


GO
CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
GO
PRINT N'Creating [dbo].[U2fView]...';


GO
CREATE VIEW [dbo].[U2fView]
AS
SELECT
    *
FROM
    [dbo].[U2f]
GO
PRINT N'Creating [dbo].[UserView]...';


GO
CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
GO
PRINT N'Creating [dbo].[CipherDetails]...';


GO
CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.[Id],
    C.[UserId],
    C.[OrganizationId],
    C.[Type],
    C.[Data],
    C.[Attachments],
    C.[CreationDate],
    C.[RevisionDate],
    CASE WHEN
        C.[Favorites] IS NULL
        OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
    THEN 0
    ELSE 1
    END [Favorite],
    CASE WHEN
        C.[Folders] IS NULL
    THEN NULL
    ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
    END [FolderId]
FROM
    [dbo].[Cipher] C
GO
PRINT N'Creating [dbo].[UserCipherDetails]...';


GO
CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE 
        WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
        ELSE 0
    END [Edit],
    CASE 
        WHEN C.[UserId] IS NULL AND O.[UseTotp] = 1 THEN 1
        ELSE 0
    END [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) C
LEFT JOIN
    [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
LEFT JOIN
    [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
LEFT JOIN
    [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    C.[UserId] = @UserId
    OR (
        C.[UserId] IS NULL
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
    )
GO
PRINT N'Creating [dbo].[Cipher_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Cipher_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Cipher_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[Cipher_ReadCanEditByIdUserId]...';


GO
CREATE PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanEdit BIT

    ;WITH [CTE] AS (
        SELECT
            CASE 
                WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
                ELSE 0
            END [Edit]
        FROM
            [dbo].[CipherDetails](@UserId) C
        LEFT JOIN
            [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
        LEFT JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
        LEFT JOIN
            [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            C.Id = @Id
            AND (
                C.[UserId] = @UserId
                OR (
                    C.[UserId] IS NULL
                    AND OU.[Status] = 2 -- 2 = Confirmed
                    AND O.[Enabled] = 1
                    AND (
                        OU.[AccessAll] = 1
                        OR CU.[CollectionId] IS NOT NULL
                        OR G.[AccessAll] = 1
                        OR CG.[CollectionId] IS NOT NULL
                    )
                )
            )
    )
    SELECT
        @CanEdit = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [CTE]
    WHERE
        [Edit] = 1

    SELECT @CanEdit
END
GO
PRINT N'Creating [dbo].[CipherDetails_ReadByIdUserId]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Id] = @Id
    ORDER BY
        [Edit] DESC
END
GO
PRINT N'Creating [dbo].[CipherDetails_ReadByTypeUserId]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadByTypeUserId]
    @Type TINYINT,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Type] = @Type
END
GO
PRINT N'Creating [dbo].[CipherDetails_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
END
GO
PRINT N'Creating [dbo].[CipherDetails_ReadByUserIdHasCollection]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserIdHasCollection]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
            ELSE 0
        END [Edit]
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO
PRINT N'Creating [dbo].[Collection_Create]...';


GO
CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[Collection_CreateWithGroups]...';


GO
CREATE PROCEDURE [dbo].[Collection_CreateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        @Id,
        [Id],
        [ReadOnly]
    FROM
        @Groups
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])
END
GO
PRINT N'Creating [dbo].[Collection_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Collection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id

    DELETE
    FROM
        [dbo].[Collection]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Collection_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Collection_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Collection_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[Collection_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[Collection_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
    FROM
        [dbo].[CollectionView] C
    INNER JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND O.[Enabled] = 1
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO
PRINT N'Creating [dbo].[Collection_ReadCountByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[Collection_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[Collection]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[Collection_ReadWithGroupsById]...';


GO
CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadById] @Id

    SELECT
        [GroupId] [Id],
        [ReadOnly]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id
END
GO
PRINT N'Creating [dbo].[Collection_Update]...';


GO
CREATE PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Collection_UpdateWithGroups]...';


GO
CREATE PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Group]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @Groups AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[GroupId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            @Id,
            [Source].[Id],
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CollectionId] = @Id THEN
        DELETE
    ;

    -- TODO: Update user revision date times that this affects
END
GO
PRINT N'Creating [dbo].[CollectionCipher_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[CollectionCipher] SC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = SC.[CollectionId]
    WHERE
        S.[OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[CollectionCipher_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        OU.[Status] = 2 -- Confirmed
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO
PRINT N'Creating [dbo].[CollectionCipher_ReadByUserIdCipherId]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_ReadByUserIdCipherId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = S.[OrganizationId] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        CC.[CipherId] = @CipherId
        AND OU.[Status] = 2 -- Confirmed
        AND (
            OU.[AccessAll] = 1
            OR CU.[CollectionId] IS NOT NULL
            OR G.[AccessAll] = 1
            OR CG.[CollectionId] IS NOT NULL
        )
END
GO
PRINT N'Creating [dbo].[CollectionUserDetails_ReadByCollectionId]...';


GO
CREATE PROCEDURE [dbo].[CollectionUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS [OrganizationUserId],
        CASE
            WHEN OU.[AccessAll] = 1 OR G.[AccessAll] = 1 THEN 1
            ELSE 0
        END [AccessAll],
        U.[Name],
        ISNULL(U.[Email], OU.[Email]) Email,
        OU.[Status],
        OU.[Type],
        CASE
            WHEN OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 0
            ELSE 1
        END [ReadOnly]
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    WHERE
        CU.[CollectionId] IS NOT NULL
        OR CG.[CollectionId] IS NOT NULL
        OR (
            OU.[OrganizationId] = @OrganizationId
            AND (
                OU.[AccessAll] = 1
                OR G.[AccessAll] = 1
            )
        )
END
GO
PRINT N'Creating [dbo].[Device_ClearPushTokenById]...';


GO
CREATE PROCEDURE [dbo].[Device_ClearPushTokenById]
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [PushToken] = NULL
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Device_Create]...';


GO
CREATE PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[Device_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Device_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Device_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Device_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Device_ReadByIdentifier]...';


GO
CREATE PROCEDURE [dbo].[Device_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [Identifier] = @Identifier
END
GO
PRINT N'Creating [dbo].[Device_ReadByIdentifierUserId]...';


GO
CREATE PROCEDURE [dbo].[Device_ReadByIdentifierUserId]
    @UserId UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [UserId] = @UserId
        AND [Identifier] = @Identifier
END
GO
PRINT N'Creating [dbo].[Device_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[Device_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[Device_Update]...';


GO
CREATE PROCEDURE [dbo].[Device_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Type] = @Type,
        [Identifier] = @Identifier,
        [PushToken] = @PushToken,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Folder_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Folder_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Folder_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[Folder_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[FolderView]
    WHERE
        [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[Grant_DeleteByKey]...';


GO
CREATE PROCEDURE [dbo].[Grant_DeleteByKey]
    @Key NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [Key] = @Key
END
GO
PRINT N'Creating [dbo].[Grant_DeleteBySubjectIdClientId]...';


GO
CREATE PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientId]
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [SubjectId] = @SubjectId
        AND [ClientId] = @ClientId
END
GO
PRINT N'Creating [dbo].[Grant_DeleteBySubjectIdClientIdType]...';


GO
CREATE PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientIdType]
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50),
    @Type NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [SubjectId] = @SubjectId
        AND [ClientId] = @ClientId
        AND [Type] = @Type
END
GO
PRINT N'Creating [dbo].[Grant_ReadByKey]...';


GO
CREATE PROCEDURE [dbo].[Grant_ReadByKey]
    @Key NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GrantView]
    WHERE
        [Key] = @Key
END
GO
PRINT N'Creating [dbo].[Grant_ReadBySubjectId]...';


GO
CREATE PROCEDURE [dbo].[Grant_ReadBySubjectId]
    @SubjectId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GrantView]
    WHERE
        [SubjectId] = @SubjectId
END
GO
PRINT N'Creating [dbo].[Grant_Save]...';


GO
CREATE PROCEDURE [dbo].[Grant_Save]
    @Key  NVARCHAR(200),
    @Type  NVARCHAR(50),
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50),
    @CreationDate DATETIME2,
    @ExpirationDate DATETIME2,
    @Data NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    MERGE
        [dbo].[Grant] AS [Target]
    USING
    ( 
        VALUES
        (
            @Key,
            @Type,
            @SubjectId,
            @ClientId,
            @CreationDate,
            @ExpirationDate,
            @Data
        )
    ) AS [Source]
    (
        [Key],
        [Type],
        [SubjectId],
        [ClientId],
        [CreationDate],
        [ExpirationDate],
        [Data]
    ) 
    ON
        [Target].[Key] = [Source].[Key]
    WHEN MATCHED THEN
        UPDATE
        SET
            [Type] = [Source].[Type],
            [SubjectId] = [Source].[SubjectId],
            [ClientId] = [Source].[ClientId],
            [CreationDate] = [Source].[CreationDate],
            [ExpirationDate] = [Source].[ExpirationDate],
            [Data] = [Source].[Data]
    WHEN NOT MATCHED THEN
        INSERT
        (
            [Key],
            [Type],
            [SubjectId],
            [ClientId],
            [CreationDate],
            [ExpirationDate],
            [Data]
        )
        VALUES
        (
            [Source].[Key],
            [Source].[Type],
            [Source].[SubjectId],
            [Source].[ClientId],
            [Source].[CreationDate],
            [Source].[ExpirationDate],
            [Source].[Data]
        )
    ;
END
GO
PRINT N'Creating [dbo].[Group_Create]...';


GO
CREATE PROCEDURE [dbo].[Group_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Group]
    (
        [Id],
        [OrganizationId],
        [Name],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[Group_CreateWithCollections]...';


GO
CREATE PROCEDURE [dbo].[Group_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Create] @Id, @OrganizationId, @Name, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        [Id],
        @Id,
        [ReadOnly]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])
END
GO
PRINT N'Creating [dbo].[Group_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Group_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Group]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Group_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Group_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GroupView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Group_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[Group_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GroupView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[Group_ReadWithCollectionsById]...';


GO
CREATE PROCEDURE [dbo].[Group_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_ReadById] @Id

    SELECT
        [CollectionId] [Id],
        [ReadOnly]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [GroupId] = @Id
END
GO
PRINT N'Creating [dbo].[Group_Update]...';


GO
CREATE PROCEDURE [dbo].[Group_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Group]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [AccessAll] = @AccessAll,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Group_UpdateWithCollections]...';


GO
CREATE PROCEDURE [dbo].[Group_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    -- TODO: Update user revision date times that this affects
END
GO
PRINT N'Creating [dbo].[GroupUser_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[GroupUser_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        GU.*
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    WHERE
        G.[OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[GroupUser_ReadGroupIdsByOrganizationUserId]...';


GO
CREATE PROCEDURE [dbo].[GroupUser_ReadGroupIdsByOrganizationUserId]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [GroupId]
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @OrganizationUserId
END
GO
PRINT N'Creating [dbo].[GroupUser_UpdateUsers]...';


GO
CREATE PROCEDURE [dbo].[GroupUser_UpdateUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Group]
        WHERE
            [Id] = @GroupId
    )

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING 
        @OrganizationUserIds AS [Source]
    ON
        [Target].[GroupId] = @GroupId
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        INSERT VALUES
        (
            @GroupId,
            [Source].[Id]
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @GroupId
    AND [Target].[OrganizationUserId] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        DELETE
    ;

    -- TODO: Bump account revision date for all @OrganizationUserIds
END
GO
PRINT N'Creating [dbo].[GroupUserDetails_ReadByGroupId]...';


GO
CREATE PROCEDURE [dbo].[GroupUserDetails_ReadByGroupId]
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS [OrganizationUserId],
        OU.[AccessAll],
        U.[Name],
        ISNULL(U.[Email], OU.[Email]) Email,
        OU.[Status],
        OU.[Type]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        GU.[GroupId] = @GroupId
END
GO
PRINT N'Creating [dbo].[Installation_Create]...';


GO
CREATE PROCEDURE [dbo].[Installation_Create]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Installation]
    (
        [Id],
        [Email],
        [Key],
        [Enabled],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @Email,
        @Key,
        @Enabled,
        @CreationDate
    )
END
GO
PRINT N'Creating [dbo].[Installation_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Installation_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Installation]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Installation_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Installation_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[InstallationView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Installation_Update]...';


GO
CREATE PROCEDURE [dbo].[Installation_Update]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Installation]
    SET
        [Email] = @Email,
        [Key] = @Key,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Organization_Create]...';


GO
CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseTotp BIT,
    @SelfHost BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
        [Id],
        [Name],
        [BusinessName],
        [BillingEmail],
        [Plan],
        [PlanType],
        [Seats],
        [MaxCollections],
        [UseGroups],
        [UseDirectory],
        [UseTotp],
        [SelfHost],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [Enabled],
        [LicenseKey],
        [ExpirationDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Name,
        @BusinessName,
        @BillingEmail,
        @Plan,
        @PlanType,
        @Seats,
        @MaxCollections,
        @UseGroups,
        @UseDirectory,
        @UseTotp,
        @SelfHost,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @Enabled,
        @LicenseKey,
        @ExpirationDate,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[Organization_Read]...';


GO
CREATE PROCEDURE [dbo].[Organization_Read]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
END
GO
PRINT N'Creating [dbo].[Organization_ReadById]...';


GO
CREATE PROCEDURE [dbo].[Organization_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Organization_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[Organization_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.*
    FROM
        [dbo].[OrganizationView] O
    INNER JOIN
        [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[Organization_Update]...';


GO
CREATE PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseTotp BIT,
    @SelfHost BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)

AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Organization]
    SET
        [Name] = @Name,
        [BusinessName] = @BusinessName,
        [BillingEmail] = @BillingEmail,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UseGroups] = @UseGroups,
        [UseDirectory] = @UseDirectory,
        [UseTotp] = @UseTotp,
        [SelfHost] = @SelfHost,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [Enabled] = @Enabled,
        [LicenseKey] = @LicenseKey,
        [ExpirationDate] = @ExpirationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Organization_UpdateStorage]...';


GO
CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[OrganizationId] = @Id
        AND C.[Attachments] IS NOT NULL

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[OrganizationUser_Create]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationUser]
    (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[OrganizationUser_CreateWithCollections]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly]
    )
    SELECT
        [Id],
        @Id,
        [ReadOnly]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])
END
GO
PRINT N'Creating [dbo].[OrganizationUser_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadById]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND (@Type IS NULL OR [Type] = @Type)
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadByOrganizationIdEmail]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Email] = @Email
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadByOrganizationIdUserId]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdUserId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[Organization] O ON O.Id = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Type] < 2 -- Owner or Admin
        AND O.[PlanType] = 0 -- Free
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadCountByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Type] = 0
END
GO
PRINT N'Creating [dbo].[OrganizationUser_ReadWithCollectionsById]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUser_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = [OU].[Id]
    WHERE
        [OrganizationUserId] = @Id
END
GO
PRINT N'Creating [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND (@Status IS NULL OR [Status] = @Status)
END
GO
PRINT N'Creating [dbo].[OrganizationUserUserDetails_ReadByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
PRINT N'Creating [dbo].[U2f_Create]...';


GO
CREATE PROCEDURE [dbo].[U2f_Create]
    @Id INT,
    @UserId UNIQUEIDENTIFIER,
    @KeyHandle VARCHAR(200),
    @Challenge VARCHAR(200),
    @AppId VARCHAR(50),
    @Version VARCHAR(20),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[U2f]
    (
        [UserId],
        [KeyHandle],
        [Challenge],
        [AppId],
        [Version],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @KeyHandle,
        @Challenge,
        @AppId,
        @Version,
        @CreationDate
    )
END
GO
PRINT N'Creating [dbo].[U2f_DeleteByUserId]...';


GO
CREATE PROCEDURE [dbo].[U2f_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[U2f]
    WHERE
        [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[U2f_ReadByUserId]...';


GO
CREATE PROCEDURE [dbo].[U2f_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[U2fView]
    WHERE
        [UserId] = @UserId
END
GO
PRINT N'Creating [dbo].[User_BumpAccountRevisionDate]...';


GO
CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [User]
    SET
        [AccountRevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_BumpAccountRevisionDateByOrganizationId]...';


GO
CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- Confirmed
END
GO
PRINT N'Creating [dbo].[User_BumpAccountRevisionDateByOrganizationUserId]...';


GO
CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserId]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    WHERE
        OU.[Id] = @OrganizationUserId
        AND OU.[Status] = 2 -- Confirmed
END
GO
PRINT N'Creating [dbo].[User_Create]...';


GO
CREATE PROCEDURE [dbo].[User_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Email NVARCHAR(50),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @LicenseKey VARCHAR(100),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[User]
    (
        [Id],
        [Name],
        [Email],
        [EmailVerified],
        [MasterPassword],
        [MasterPasswordHint],
        [Culture],
        [SecurityStamp],
        [TwoFactorProviders],
        [TwoFactorRecoveryCode],
        [EquivalentDomains],
        [ExcludedGlobalEquivalentDomains],
        [AccountRevisionDate],
        [Key],
        [PublicKey],
        [PrivateKey],
        [Premium],
        [PremiumExpirationDate],
        [Storage],
        [MaxStorageGb],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [LicenseKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Name,
        @Email,
        @EmailVerified,
        @MasterPassword,
        @MasterPasswordHint,
        @Culture,
        @SecurityStamp,
        @TwoFactorProviders,
        @TwoFactorRecoveryCode,
        @EquivalentDomains,
        @ExcludedGlobalEquivalentDomains,
        @AccountRevisionDate,
        @Key,
        @PublicKey,
        @PrivateKey,
        @Premium,
        @PremiumExpirationDate,
        @Storage,
        @MaxStorageGb,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @LicenseKey,
        @CreationDate,
        @RevisionDate
    )
END
GO
PRINT N'Creating [dbo].[User_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id
        AND [Type] != 0 -- 0 = owner

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO
PRINT N'Creating [dbo].[User_ReadAccountRevisionDateById]...';


GO

CREATE PROCEDURE [dbo].[User_ReadAccountRevisionDateById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [AccountRevisionDate]
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_ReadByEmail]...';


GO
CREATE PROCEDURE [dbo].[User_ReadByEmail]
    @Email NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Email] = @Email
END
GO
PRINT N'Creating [dbo].[User_ReadById]...';


GO
CREATE PROCEDURE [dbo].[User_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_ReadPublicKeyById]...';


GO
CREATE PROCEDURE [dbo].[User_ReadPublicKeyById]
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [PublicKey]
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_Update]...';


GO
CREATE PROCEDURE [dbo].[User_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Email NVARCHAR(50),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50),
    @Culture NVARCHAR(10),
    @SecurityStamp NVARCHAR(50),
    @TwoFactorProviders NVARCHAR(MAX),
    @TwoFactorRecoveryCode NVARCHAR(32),
    @EquivalentDomains NVARCHAR(MAX),
    @ExcludedGlobalEquivalentDomains NVARCHAR(MAX),
    @AccountRevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX),
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @Premium BIT,
    @PremiumExpirationDate DATETIME2(7),
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @LicenseKey VARCHAR(100),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Name] = @Name,
        [Email] = @Email,
        [EmailVerified] = @EmailVerified,
        [MasterPassword] = @MasterPassword,
        [MasterPasswordHint] = @MasterPasswordHint,
        [Culture] = @Culture,
        [SecurityStamp] = @SecurityStamp,
        [TwoFactorProviders] = @TwoFactorProviders,
        [TwoFactorRecoveryCode] = @TwoFactorRecoveryCode,
        [EquivalentDomains] = @EquivalentDomains,
        [ExcludedGlobalEquivalentDomains] = @ExcludedGlobalEquivalentDomains,
        [AccountRevisionDate] = @AccountRevisionDate,
        [Key] = @Key,
        [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey,
        [Premium] = @Premium,
        [PremiumExpirationDate] = @PremiumExpirationDate,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [LicenseKey] = @LicenseKey,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_UpdateKeys]...';


GO
CREATE PROCEDURE [dbo].[User_UpdateKeys]
    @Id UNIQUEIDENTIFIER,
    @SecurityStamp NVARCHAR(50),
    @Key NVARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [SecurityStamp] = @SecurityStamp,
        [Key] = @Key,
        [PrivateKey] = @PrivateKey,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[User_UpdateStorage]...';


GO
CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[UserId] = @Id
        AND C.[Attachments] IS NOT NULL

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[Cipher_Create]...';


GO
CREATE PROCEDURE [dbo].[Cipher_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [Attachments],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @Attachments,
        @CreationDate,
        @RevisionDate
    )

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_Delete]...';


GO
CREATE PROCEDURE [dbo].[Cipher_Delete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [Attachments] BIT NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId],
        CASE WHEN [Attachments] IS NULL THEN 0 ELSE 1 END
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Cleanup orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrgId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Cleanup user
    DECLARE @UserCiphersWithStorageCount INT
    SELECT
        @UserCiphersWithStorageCount = COUNT(1)
    FROM
        #Temp
    WHERE
        [UserId] IS NOT NULL
        AND [Attachments] = 1

    IF @UserCiphersWithStorageCount > 0
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
    END
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO
PRINT N'Creating [dbo].[Cipher_DeleteAttachment]...';


GO
CREATE PROCEDURE [dbo].[Cipher_DeleteAttachment]
    @Id UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @UserId = [UserId],
        @OrganizationId = [OrganizationId]
    FROM 
        [dbo].[Cipher]
    WHERE [Id] = @Id

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = JSON_MODIFY([Attachments], @AttachmentIdPath, NULL)
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Attachments BIT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = CASE WHEN [Attachments] IS NOT NULL THEN 1 ELSE 0 END
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_Move]...';


GO
CREATE PROCEDURE [dbo].[Cipher_Move]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @FolderId AS UNIQUEIDENTIFIER,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    ;WITH [IdsToMoveCTE] AS (
        SELECT
            [Id]
        FROM
            [dbo].[UserCipherDetails](@UserId)
        WHERE
            [Edit] = 1
            AND [Id] IN (SELECT * FROM @Ids)
    )
    UPDATE
        [dbo].[Cipher]
    SET
        [Folders] = 
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END
    WHERE
        [Id] IN (SELECT * FROM [IdsToMoveCTE])

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    -- TODO: What if some that were updated were organization ciphers? Then bump by org ids.
END
GO
PRINT N'Creating [dbo].[Cipher_Update]...';


GO
CREATE PROCEDURE [dbo].[Cipher_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Favorites] = @Favorites,
        [Folders] = @Folders,
        [Attachments] = @Attachments,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_UpdateAttachment]...';


GO
CREATE PROCEDURE [dbo].[Cipher_UpdateAttachment]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50),
    @AttachmentData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = 
            CASE
            WHEN [Attachments] IS NULL THEN
                CONCAT('{', @AttachmentIdKey, ':', @AttachmentData, '}')
            ELSE
                JSON_MODIFY([Attachments], @AttachmentIdPath, JSON_QUERY(@AttachmentData, '$'))
            END
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_UpdatePartial]...';


GO
CREATE PROCEDURE [dbo].[Cipher_UpdatePartial]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [Folders] = 
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END,
        [Favorites] =
            CASE
            WHEN @Favorite = 1 AND [Favorites] IS NULL THEN
                CONCAT('{', @UserIdKey, ':true}')
            WHEN @Favorite = 1 THEN
                JSON_MODIFY([Favorites], @UserIdPath, CAST(1 AS BIT))
            ELSE
                JSON_MODIFY([Favorites], @UserIdPath, NULL)
            END
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[Cipher_UpdateWithCollections]...';


GO
CREATE PROCEDURE [dbo].[Cipher_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [Attachments] = @Attachments,
        [RevisionDate] = @RevisionDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            S.[Id]
        FROM
            [dbo].[Collection] S
        INNER JOIN
            [Organization] O ON O.[Id] = S.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = S.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId]
        WHERE
            O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1 
                OR CU.[ReadOnly] = 0 
                OR G.[AccessAll] = 1 
                OR CG.[ReadOnly] = 0
            )
    )
    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    SELECT
        [Id],
        @Id
    FROM
        @CollectionIds
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])

    IF @Attachments IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_UpdateStorage] @UserId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO
PRINT N'Creating [dbo].[CipherDetails_Create]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @OrganizationUseTotp BIT -- not used
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        CASE WHEN @Favorite = 1 THEN CONCAT('{', @UserIdKey, ':true}') ELSE NULL END,
        CASE WHEN @FolderId IS NOT NULL THEN CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}') ELSE NULL END,
        @CreationDate,
        @RevisionDate
    )

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[CipherDetails_Update]...';


GO
CREATE PROCEDURE [dbo].[CipherDetails_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @OrganizationUseTotp BIT -- not used
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Folders] = 
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END,
        [Favorites] =
            CASE
            WHEN @Favorite = 1 AND [Favorites] IS NULL THEN
                CONCAT('{', @UserIdKey, ':true}')
            WHEN @Favorite = 1 THEN
                JSON_MODIFY([Favorites], @UserIdPath, CAST(1 AS BIT))
            ELSE
                JSON_MODIFY([Favorites], @UserIdPath, NULL)
            END,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
PRINT N'Creating [dbo].[CollectionCipher_Create]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_Create]
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    VALUES
    (
        @CollectionId,
        @CipherId
    )

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO
PRINT N'Creating [dbo].[CollectionCipher_Delete]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_Delete]
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionCipher]
    WHERE
        [CollectionId] = @CollectionId
        AND [CipherId] = @CipherId

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO
PRINT N'Creating [dbo].[CollectionCipher_UpdateCollections]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollections]
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] = @CipherId
    )

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            C.[Id]
        FROM
            [dbo].[Collection] C
        INNER JOIN
            [Organization] O ON O.[Id] = C.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            O.[Id] = @OrgId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1
                OR CU.[CollectionId] IS NOT NULL
                OR G.[AccessAll] = 1
                OR CG.[CollectionId] IS NOT NULL
            )
    )
    MERGE
        [dbo].[CollectionCipher] AS [Target]
    USING 
        @CollectionIds AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId
    AND [Target].[CollectionId] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        DELETE
    ;

    IF @OrgId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
    END
END
GO
PRINT N'Creating [dbo].[CollectionCipher_UpdateCollectionsAdmin]...';


GO
CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsAdmin]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionCipher] AS [Target]
    USING 
        @CollectionIds AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId THEN
        DELETE
    ;

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO
PRINT N'Creating [dbo].[CollectionUser_Delete]...';


GO
CREATE PROCEDURE [dbo].[CollectionUser_Delete]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [CollectionId] = @CollectionId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
GO
PRINT N'Creating [dbo].[Folder_Create]...';


GO
CREATE PROCEDURE [dbo].[Folder_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Folder]
    (
        [Id],
        [UserId],
        [Name],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
PRINT N'Creating [dbo].[Folder_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Folder_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM [dbo].[Folder] WHERE [Id] = @Id)
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$."', @UserId, '"')

    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
    WHERE
        (
            C.[UserId] = @UserId
            OR (
                C.[UserId] IS NULL
                AND (OU.[AccessAll] = 1 OR CU.[CollectionId] IS NOT NULL)
            )
        )
        AND C.[Folders] IS NOT NULL
        AND JSON_VALUE(C.[Folders], @UserIdPath) = @Id

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
PRINT N'Creating [dbo].[Folder_Update]...';


GO
CREATE PROCEDURE [dbo].[Folder_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Folder]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
PRINT N'Creating [dbo].[GroupUser_Delete]...';


GO
CREATE PROCEDURE [dbo].[GroupUser_Delete]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
GO
PRINT N'Creating [dbo].[GroupUser_UpdateGroups]...';


GO
CREATE PROCEDURE [dbo].[GroupUser_UpdateGroups]
    @OrganizationUserId UNIQUEIDENTIFIER,
    @GroupIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [Id] = @OrganizationUserId
    )

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING 
        @GroupIds AS [Source]
    ON
        [Target].[GroupId] = [Source].[Id]
        AND [Target].[OrganizationUserId] = @OrganizationUserId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @OrganizationUserId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[OrganizationUserId] = @OrganizationUserId
    AND [Target].[GroupId] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
GO
PRINT N'Creating [dbo].[Organization_DeleteById]...';


GO
CREATE PROCEDURE [dbo].[Organization_DeleteById]
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
            [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id
END
GO
PRINT N'Creating [dbo].[OrganizationUser_Update]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [OrganizationId] = @OrganizationId,
        [UserId] = @UserId,
        [Email] = @Email,
        [Key] = @Key,
        [Status] = @Status,
        [Type] = @Type,
        [AccessAll] = @AccessAll,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
PRINT N'Creating [dbo].[OrganizationUser_UpdateWithCollections]...';


GO
CREATE PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionUser] AS [Target]
    USING 
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[OrganizationUserId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[OrganizationUserId] = @Id THEN
        DELETE
    ;
END
GO
PRINT N'Checking existing data against newly created constraints';


GO
ALTER TABLE [dbo].[Cipher] WITH CHECK CHECK CONSTRAINT [FK_Cipher_Organization];

ALTER TABLE [dbo].[Cipher] WITH CHECK CHECK CONSTRAINT [FK_Cipher_User];

ALTER TABLE [dbo].[Collection] WITH CHECK CHECK CONSTRAINT [FK_Collection_Organization];

ALTER TABLE [dbo].[CollectionCipher] WITH CHECK CHECK CONSTRAINT [FK_CollectionCipher_Cipher];

ALTER TABLE [dbo].[CollectionCipher] WITH CHECK CHECK CONSTRAINT [FK_CollectionCipher_Collection];

ALTER TABLE [dbo].[CollectionGroup] WITH CHECK CHECK CONSTRAINT [FK_CollectionGroup_Collection];

ALTER TABLE [dbo].[CollectionGroup] WITH CHECK CHECK CONSTRAINT [FK_CollectionGroup_Group];

ALTER TABLE [dbo].[CollectionUser] WITH CHECK CHECK CONSTRAINT [FK_CollectionUser_Collection];

ALTER TABLE [dbo].[CollectionUser] WITH CHECK CHECK CONSTRAINT [FK_CollectionUser_OrganizationUser];

ALTER TABLE [dbo].[Device] WITH CHECK CHECK CONSTRAINT [FK_Device_User];

ALTER TABLE [dbo].[Folder] WITH CHECK CHECK CONSTRAINT [FK_Folder_User];

ALTER TABLE [dbo].[Group] WITH CHECK CHECK CONSTRAINT [FK_Group_Organization];

ALTER TABLE [dbo].[GroupUser] WITH CHECK CHECK CONSTRAINT [FK_GroupUser_Group];

ALTER TABLE [dbo].[GroupUser] WITH CHECK CHECK CONSTRAINT [FK_GroupUser_OrganizationUser];

ALTER TABLE [dbo].[OrganizationUser] WITH CHECK CHECK CONSTRAINT [FK_OrganizationUser_Organization];

ALTER TABLE [dbo].[OrganizationUser] WITH CHECK CHECK CONSTRAINT [FK_OrganizationUser_User];

ALTER TABLE [dbo].[U2f] WITH CHECK CHECK CONSTRAINT [FK_U2f_User];


GO
PRINT N'Update complete.';


GO
