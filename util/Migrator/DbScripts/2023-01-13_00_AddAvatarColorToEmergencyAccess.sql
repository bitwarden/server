SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW [dbo].[EmergencyAccessDetailsView]
AS
SELECT
    EA.*,
    GranteeU.[Name] GranteeName,
    ISNULL(GranteeU.[Email], EA.[Email]) GranteeEmail,
    GranteeU.[AvatarColor] GranteeAvatarColor,
    GrantorU.[Name] GrantorName,
    GrantorU.[Email] GrantorEmail,
    GrantorU.[AvatarColor] GrantorAvatarColor
FROM
    [dbo].[EmergencyAccess] EA
LEFT JOIN
    [dbo].[User] GranteeU ON GranteeU.[Id] = EA.[GranteeId]
LEFT JOIN
    [dbo].[User] GrantorU ON GrantorU.[Id] = EA.[GrantorId]
GO

CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccessDetails_ReadByGrantorId]
    @GrantorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GrantorId] = @GrantorId
END
GO