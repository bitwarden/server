# SCIM attribute projection

User and Group collection and individual GET endpoints accept `attributes` or
`excludedAttributes` as defined in [RFC 7644 section 3.9](https://www.rfc-editor.org/rfc/rfc7644.html#section-3.9).

Examples (relative to the organization's SCIM base URL):

```http
GET /Users?excludedAttributes=groups,meta
GET /Users?attributes=userName,name.givenName,groups.value
GET /Groups?excludedAttributes=members
GET /Groups/{id}?attributes=displayName,members.value,members.$ref
```

Attribute names are case-insensitive. Parent selection includes all supported child
attributes; subattribute selection includes only those children. Unknown attributes
are ignored. Duplicate paths are accepted; a selected parent takes precedence over
its selected children. Core User and Group schema-qualified paths are supported.
Blank selections, empty comma-separated entries and malformed paths return a SCIM
400 error, as does supplying both query parameters.

Resource `id` and `schemas` are always preserved. Selection cannot expose attributes
absent from the existing response models (including passwords). Collection envelope
fields, such as `totalResults`, `startIndex`, `itemsPerPage` and `schemas`, are not
projected. Excluded attributes are absent from JSON, not replaced with empty arrays.

Excluding memberships skips their repository lookups. Selecting Group members without
`display` also skips the member-email lookup.
Projection does not push collection filtering or pagination into the database.

List membership lookups are batched for the returned resource page. Member and group
details are fetched once for the distinct references selected by membership pagination,
rather than separately for every resource. Member displays use one organization-scoped
read selecting only the organization-user ID and email, falling back to the linked
account email only when the organization-user email is null. Both SQL Server and
Entity Framework use page-scoped membership queries. SQL Server deployments must apply
the accompanying membership batch-read and organization-user email-read migrations.

Without either parameter, existing responses are unchanged: complete memberships
are returned by default, while collections default to 50 resources per page.
Explicit `groupStartIndex`/`groupCount` and `memberStartIndex`/`memberCount` continue
to page memberships when those attributes are requested.

Projection applies only to these GET endpoints, not write responses.
