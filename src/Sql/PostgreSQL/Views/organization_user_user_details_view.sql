CREATE VIEW organization_user_user_details_view
AS
SELECT
    ou.id,
    ou.user_id,
    ou.organization_id,
    u.name,
    coalesce(u.email, ou.email) email,
    u.two_factor_providers,
    u.premium,
    ou.status,
    ou.type,
    ou.access_all,
    ou.external_id
FROM
    organization_user ou
LEFT JOIN
    "user" u ON U.id = ou.user_id;