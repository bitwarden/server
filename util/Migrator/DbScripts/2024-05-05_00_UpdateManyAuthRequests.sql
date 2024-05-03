IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE
        [Name] = 'AuthRequestType' AND
        is_user_defined = 1
)
BEGIN
    CREATE TYPE [dbo].[AuthRequestType] AS TABLE(
        [Id]                        UNIQUEIDENTIFIER,
        [UserId]                    UNIQUEIDENTIFIER,
        [Type]                      SMALLINT,
        [RequestDeviceIdentifier]   NVARCHAR(50),
        [RequestDeviceType]         SMALLINT,
        [RequestIpAddress]          VARCHAR(50),
        [ResponseDeviceId]          UNIQUEIDENTIFIER,
        [AccessCode]                VARCHAR(25),
        [PublicKey]                 VARCHAR(MAX),
        [Key]                       VARCHAR(MAX),
        [MasterPasswordHash]        VARCHAR(MAX),
        [Approved]                  BIT,
        [CreationDate]              DATETIME2 (7),
        [ResponseDate]              DATETIME2 (7),
        [AuthenticationDate]        DATETIME2 (7),
        [OrganizationId]            UNIQUEIDENTIFIER
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_UpdateMany]
    @AuthRequestsInput [dbo].[AuthRequestType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        AR
    SET
        [Id] = ARI.[Id],
        [UserId] = ARI.[UserId],
        [Type] = ARI.[Type],
        [RequestDeviceIdentifier] = ARI.[RequestDeviceIdentifier],
        [RequestDeviceType] = ARI.[RequestDeviceType],
        [RequestIpAddress] = ARI.[RequestIpAddress],
        [ResponseDeviceId] = ARI.[ResponseDeviceId],
        [AccessCode] = ARI.[AccessCode],
        [PublicKey] = ARI.[PublicKey],
        [Key] = ARI.[Key],
        [MasterPasswordHash] = ARI.[MasterPasswordHash],
        [Approved] = ARI.[Approved],
        [CreationDate] = ARI.[CreationDate],
        [ResponseDate] = ARI.[ResponseDate],
        [AuthenticationDate] = ARI.[AuthenticationDate],
        [OrganizationId] = ARI.[OrganizationId]
    FROM
        [dbo].[AuthRequest] AR
    INNER JOIN
        @AuthRequestsInput ARI ON AR.Id = ARI.Id
END
