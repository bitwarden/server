﻿CREATE PROCEDURE [dbo].[Send_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @Password NVARCHAR(300),
    @MaxAccessCount INT,
    @AccessCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7),
    @DeletionDate DATETIME2(7),
    @Disabled BIT,
    @HideEmail BIT,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @Emails NVARCHAR(1024) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Send]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Key] = @Key,
        [Password] = @Password,
        [MaxAccessCount] = @MaxAccessCount,
        [AccessCount] = @AccessCount,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [ExpirationDate] = @ExpirationDate,
        [DeletionDate] = @DeletionDate,
        [Disabled] = @Disabled,
        [HideEmail] = @HideEmail,
        [CipherId] = @CipherId,
        [Emails] = @Emails
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
