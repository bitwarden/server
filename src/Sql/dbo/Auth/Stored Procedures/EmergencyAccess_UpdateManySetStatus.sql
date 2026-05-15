CREATE PROCEDURE [dbo].[EmergencyAccess_UpdateManySetStatus]
    @Ids [dbo].[GuidIdArray] READONLY,
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        EA
    SET
        EA.[Status] = @Status,
        EA.[KeyEncrypted] = NULL,
        EA.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[EmergencyAccess] EA
    INNER JOIN
        @Ids I ON I.[Id] = EA.[Id]
END
GO
