## ADDED Requirements

### Requirement: Per-service Workload Identity
Each service deployed to the AKS cluster SHALL run under its own Kubernetes ServiceAccount federated to a dedicated Azure User-Assigned Managed Identity, distinct from every other service's identity.

#### Scenario: Service pod requests a Key Vault secret
- **WHEN** a pod for a given service starts and the Secrets Store CSI Driver mounts its `SecretProviderClass`
- **THEN** the pod authenticates to Key Vault using its own service's federated Managed Identity, not a shared or node-level identity

#### Scenario: Two services never share a ServiceAccount
- **WHEN** Helm renders Deployment/CronJob/Job manifests for any two distinct services
- **THEN** each manifest references a different Kubernetes ServiceAccount name

### Requirement: Least-privilege Key Vault RBAC per identity
Each service's Managed Identity SHALL be granted Key Vault RBAC access (`Key Vault Secrets User` or narrower) scoped only to the secrets that service requires, never vault-wide standing access.

#### Scenario: Service requests a secret it is not scoped to
- **WHEN** a service's Managed Identity attempts to read a Key Vault secret outside its granted scope
- **THEN** Azure denies the request with an authorization error

#### Scenario: Summarizer identity cannot read billing secrets
- **WHEN** the `summarizer` service's Managed Identity is evaluated against the Stripe secret key
- **THEN** it has no RBAC role assignment granting access to that secret

### Requirement: No plaintext secrets in git or Kubernetes manifests
The system SHALL NOT store any runtime credential (API key, connection string, webhook secret) as plaintext in `infra/helm/values.yaml`, any Helm template, or a checked-in Kubernetes `Secret` manifest.

#### Scenario: Repository scan for plaintext secrets
- **WHEN** `infra/helm/` is scanned for credential-shaped values (API keys, connection strings)
- **THEN** no plaintext secret values are found; all secret references are Key Vault secret names or `SecretProviderClass` references

#### Scenario: Secret rotation requires no Helm change
- **WHEN** a secret value is rotated in Key Vault
- **THEN** no `values.yaml` or template change is required for the running workload to pick up the new value on next pod restart

### Requirement: AKS cluster has OIDC issuer and Workload Identity enabled
The AKS cluster SHALL have its OIDC issuer and Workload Identity features enabled as a declared, Terraform-managed cluster attribute.

#### Scenario: Terraform plan shows cluster identity features as managed state
- **WHEN** `terraform plan` runs against the AKS cluster resource
- **THEN** `oidc_issuer_enabled` and `workload_identity_enabled` appear as part of the managed resource's declared configuration, not as unmanaged/out-of-band cluster state

### Requirement: Key Vault and container registry are not publicly reachable
Key Vault and the container registry SHALL have public network access disabled and be reachable only via a private endpoint inside the platform VNet.

#### Scenario: Public network request to Key Vault
- **WHEN** a request to the Key Vault's data-plane endpoint is made from outside the platform VNet
- **THEN** the request is refused at the network layer, independent of any RBAC role assignment

#### Scenario: In-cluster resolution
- **WHEN** a pod inside the AKS cluster resolves the Key Vault or registry hostname
- **THEN** DNS resolves to a private IP address inside the platform VNet, not a public IP
