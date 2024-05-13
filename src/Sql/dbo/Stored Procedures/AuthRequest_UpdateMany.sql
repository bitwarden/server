CREATE OR ALTER PROCEDURE AuthRequest_UpdateMany
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
            Id INT '$.Id',
            UserId INT '$.UserId',
            Type NVARCHAR(50) '$.Type',
            RequestDeviceIdentifier NVARCHAR(100) '$.RequestDeviceIdentifier',
            RequestDeviceType NVARCHAR(50) '$.RequestDeviceType',
            RequestIpAddress NVARCHAR(50) '$.RequestIpAddress',
            ResponseDeviceId INT '$.ResponseDeviceId',
            AccessCode NVARCHAR(50) '$.AccessCode',
            PublicKey NVARCHAR(MAX) '$.PublicKey',
            Key NVARCHAR(MAX) '$.Key',
            MasterPasswordHash NVARCHAR(MAX) '$.MasterPasswordHash',
            Approved BIT '$.Approved',
            CreationDate DATETIME2 '$.CreationDate',
            ResponseDate DATETIME2 '$.ResponseDate',
            AuthenticationDate DATETIME2 '$.AuthenticationDate',
            OrganizationId INT '$.OrganizationId'
        ) AS ARI
    WHERE
        AR.Id = ARI.Id;
END
