## Perspectives

### Security
Highlights models and relationships identified as a part of [threat modeling](https://www.threatmodelingmanifesto.org/).

Identified threats are expected to be itemized in the perspective description, tagged with an appropriate `Security: threat` tag, and include a `!docs` property that describes the threat and mitigations. [`-> (relationships)`](https://docs.structurizr.com/dsl/language#relationship) do not allow for a `!docs` property, so a `url` property is used instead, linking to the appropriate section of the published docs.

#### Example Model or Relationship

##### Example Threat

- **Type**: type
- **Priority**: TBD/Low/Medium/High/Critical
- **Likelihood**: TBD/Low/Medium/High/Critical
- **Impact**: TBD/Low/Medium/High/Critical

description of the threat.

###### Example Threat Mitigations
describe the mitigations for the threat and whether or not they are complete


### SRE

Highlights concerns and requirements for cloud deployments.
