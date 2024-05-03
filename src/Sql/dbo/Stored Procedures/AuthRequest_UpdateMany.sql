CREATE PROCEDURE [dbo].[AuthRequest_UpdateMany]
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
