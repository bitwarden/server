## Threat Model

### Example Model or Relationship

#### Example Threat

- **Type**: type
- **Priority**: TBD/Low/Medium/High/Critical
- **Likelihood**: TBD/Low/Medium/High/Critical
- **Impact**: TBD/Low/Medium/High/Critical

description of the threat.

##### Example Threat Mitigations
describe the mitigations for the threat.

### Clients -> IconsController
Communication from clients to the icons component. This is an unauthenticated endpoint with minimal input validation.

#### SSL termination exposes vault contents to network administrators

- **Type**: Information Disclosure
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

A machine with SSL terminating proxies cannot rely on encrypted query parameters hiding vault contents from network administrators.

##### Mitigations
- <span style="color:red">Not Implemented</span>: Establish encrypted pipe communication with Icons service prior to requesting icon resolution

#### Cleartext transmission of vault contents to Server

- **Type**: Information Disclosure
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

Server-side after TLS by necessity to lookup a favicon. However, to maintain our promises as a no-log proxy, we need to be sure not to maintain ip records for icon service requests

##### Mitigations
- <span style="color:red">Unconfirmed</span>: Configure network edge and datadog to drop this identifying data.

#### No SLA offered on Icons service

- **Type**: Denial of Service
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

We do not offer SLA on up time of icons service. Clients may be unable to resolve icons, and we need to determine a graceful degradation strategy.

##### Mitigations
- <span style="color:green">Done</span>: Default icon fallback (globe)
- <span style="color:red">Not Implemented, Not Prioritized</span>: Local cache of retrieved icons

#### SSRF by proxied requests

- **Type**: Elevation of Privilege / Information Disclosure
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

The service is designed to proxy requests to arbitrary URLs. This can be used to access internal network resources.

If a site redirects to an internal network address, the internal network topography may be exposed to the client.

##### Mitigations
- <span style="color:green">Done</span>: Isolation of the icons component from the rest of the system intranet.
- <span style="color:green">Done</span>: Avoid fetching by domain name. All requests must be first resolved to an IP address and filtered against internal network ranges, defined as:
  - `::1`, `::`, `::ffff:`
  - IPv6 and starting with `fc`, `fd`, `fe`, or `ff`
  - IPv4 and starting with `0.`, `10.`, `100.`, `127.`, `169.254`, `172.16-31`, or `192.168`

  This is done in the `IconDetermination` component

### IconsController -> IconCache
Communication from the icons controller to a mem cache of previously retrieved icons, keyed by original domain requested.

#### Cache determination through timing measurements

- **Type**: Information Disclosure
- **Priority**: Low
- **Likelihood**: Low
- **Impact**: Low

By measuring the time it takes to retrieve an icon, an attacker may be able to determine if a domain has been previously requested by another user, revealing that some user on the service has that domain in their vault.

##### Mitigations
<span style="color:red">None identified</span>

#### Unescaped storage of user-input data in cache

- **Type**: Tampering
- **Priority**: Low
- **Likelihood**: Low
- **Impact**: Low

Unescaped user input data may be stored as keys in the cache. This input data is not executed, but if the storage method is changed in the future, this may lead to some injection attack.

##### Mitigations
<span style="color:red">None identified</span>

#### Cache bloat through intentionally large icons

- **Type**: Denial of Service
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

User request may intentionally resolve to very large icons, bloating the cache and increasing memory requirements.

<span style="color:red">Open question</span>: Should we also limit the size of icons fetched?

##### Mitigations
<span style="color:green">Done</span>: Limit size of icons stored in cache

#### Cache bloat through many unique domain requests

- **Type**: Denial of Service
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

User request may intentionally resolve many unique domains to resolve that may or may not exist, bloating the cache and increasing memory requirements.

##### Mitigations
<span style="color:red">Unconfirmed</span>: Rate limit requests to the icons service

#### Storage of potentially sensitive data as keys or values in cache

- **Type**: Information Disclosure
- **Priority**: TBD
- **Likelihood**: TBD
- **Impact**: TBD

Upload of urls is automatic to our icon service. If our filters for upload are incorrect, we may store sensitive data in our cache. For example, onion addresses.

##### Mitigations
<span style="color:green">Done</span>: Avoid filter known sensitive urls
<span style="color:red">Not implemented, Not prioritized</span>: Add client-side setting to disable icon request for a given url or pattern

#### Cache poisoning via dns poisoning

- **Type**: Tampering
- **Priority**: Low
- **Likelihood**: Low
- **Impact**: Low

DNS poisoning would lead to incorrect icons being cached for a given domain.

##### Mitigations
<span style="color:red">None Identified</span>
