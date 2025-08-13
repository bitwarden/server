﻿CREATE VIEW [dbo].[EmergencyAccessDetailsView]
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