CREATE VIEW organization_user_organization_details_view
AS
SELECT
    ou.user_id,
    ou.organization_id,
    o.name,
    o.enabled,
    o.use_groups,
    o.use_directory,
    o.use_events,
    o.use_totp,
    o.use_2fa,
    o.use_api,
    o.self_host,
    o.users_get_premium,
    o.seats,
    o.max_collections,
    o.max_storage_gb,
    ou.key,
    ou.status,
    ou.type
FROM
    organization_user ou
INNER JOIN
    organization o ON o.id = ou.organization_id;