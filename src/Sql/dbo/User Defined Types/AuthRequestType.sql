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
