# Bitwarden Schema: Relationships

> Rule numbers in the Notes column refer to the Bitwarden grounding rules in SKILL.md.

Bitwarden's access model lives in these joins, not in any single table — membership, group mediation, and collection grants are all relationships. Trace every join through this table and read the Notes column: several are counterintuitive (`CollectionUser` and `GroupUser` key on `OrganizationUser.Id`, not `User.Id`), and a wrong join silently under- or over-counts instead of erroring. Cardinality and ownership (`UserId` XOR `OrganizationId`) separate a plausible query from a correct one.

| Relationship                      | Join                                                         | Type    | Notes                                   |
| --------------------------------- | ------------------------------------------------------------ | ------- | --------------------------------------- |
| Organization → OrganizationUser   | `OU.OrganizationId = O.Id`                                   | 1:Many  |                                         |
| User → OrganizationUser           | `OU.UserId = U.Id`                                           | 1:Many  | `UserId` NULL until invitation accepted |
| OrganizationUser → CollectionUser | `CU.OrganizationUserId = OU.Id`                              | 1:Many  | **Not** `User.Id` — see rule 5.         |
| OrganizationUser → GroupUser      | `GU.OrganizationUserId = OU.Id`                              | 1:Many  | **Not** `User.Id`.                      |
| Group → CollectionGroup           | `CG.GroupId = G.Id`                                          | 1:Many  |                                         |
| Group → GroupUser                 | `GU.GroupId = G.Id`                                          | 1:Many  |                                         |
| Collection → CollectionCipher     | `CC.CollectionId = C.Id`                                     | 1:Many  |                                         |
| Cipher → CollectionCipher         | `CC.CipherId = CI.Id`                                        | 1:Many  |                                         |
| Organization → Collection         | `C.OrganizationId = O.Id`                                    | 1:Many  |                                         |
| User → Cipher (personal)          | `CI.UserId = U.Id`                                           | 1:Many  | Pair with `OrganizationId IS NULL`      |
| Organization → Cipher             | `CI.OrganizationId = O.Id`                                   | 1:Many  | Pair with `UserId IS NULL`              |
| Organization → Group              | `G.OrganizationId = O.Id`                                    | 1:Many  |                                         |
| Send → User / Org / Cipher        | `S.UserId / S.OrganizationId / S.CipherId`                   | Tri-FK  | User/Org XOR; CipherId optional         |
| AuthRequest → User                | `AR.UserId = U.Id`                                           | 1:Many  |                                         |
| OrganizationSponsorship → Org     | `OS.SponsoringOrganizationId` / `OS.SponsoredOrganizationId` | Dual FK |                                         |

**Junction tables**: `CollectionCipher`, `CollectionUser`, `CollectionGroup`, `GroupUser`, `ProjectSecret`, `AccessPolicy`, `NotificationStatus`.
