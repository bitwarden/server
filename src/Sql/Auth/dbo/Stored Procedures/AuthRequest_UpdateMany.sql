CREATE PROCEDURE AuthRequest_UpdateMany
    @jsonData NVARCHAR(MAX)
AS
BEGIN
    UPDATE AR
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
        OPENJSON(@jsonData)
        WITH (
            Id UNIQUEIDENTIFIER '$.Id',
            UserId UNIQUEIDENTIFIER '$.UserId',
            Type SMALLINT '$.Type',
            RequestDeviceIdentifier NVARCHAR(50) '$.RequestDeviceIdentifier',
            RequestDeviceType SMALLINT '$.RequestDeviceType',
            RequestIpAddress VARCHAR(50) '$.RequestIpAddress',
            ResponseDeviceId UNIQUEIDENTIFIER '$.ResponseDeviceId',
            AccessCode VARCHAR(25) '$.AccessCode',
            PublicKey VARCHAR(MAX) '$.PublicKey',
            [Key] VARCHAR(MAX) '$.Key',
            MasterPasswordHash VARCHAR(MAX) '$.MasterPasswordHash',
            Approved BIT '$.Approved',
            CreationDate DATETIME2 '$.CreationDate',
            ResponseDate DATETIME2 '$.ResponseDate',
            AuthenticationDate DATETIME2 '$.AuthenticationDate',
            OrganizationId UNIQUEIDENTIFIER '$.OrganizationId'
        ) ARI ON AR.Id = ARI.Id;
END
