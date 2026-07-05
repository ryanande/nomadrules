## ADDED Requirements

### Requirement: Team access is granted via Entra ID app roles
The system SHALL grant team/admin access to Azure resources exclusively through Entra ID (workforce tenant) app roles assigned to named individuals, and SHALL NOT rely on standing subscription-level Owner/Contributor grants for day-to-day access.

#### Scenario: Team member assigned the Operator role
- **WHEN** a team member is assigned the `Operator` app role in the workforce Entra ID tenant
- **THEN** they receive Key Vault Secrets User and AKS RBAC Reader access scoped to the target namespace, and no broader access

#### Scenario: No standing Owner/Contributor grant
- **WHEN** the RBAC role assignments are enumerated for the target resource group
- **THEN** no individual human account holds Owner or Contributor at subscription or resource-group scope outside the documented break-glass account

### Requirement: Three least-privilege role tiers
The system SHALL define exactly three app roles — `Admin`, `Operator`, `ReadOnly` — each mapped to specific Azure built-in RBAC roles at a defined scope.

#### Scenario: Admin role scope
- **WHEN** a principal holds the `Admin` app role
- **THEN** they hold Key Vault Secrets Officer, AKS RBAC Admin, and ACR Push, each scoped to the resource group

#### Scenario: ReadOnly role scope
- **WHEN** a principal holds the `ReadOnly` app role
- **THEN** they hold Reader at resource-group scope and no write/secret-read permissions

#### Scenario: Role assignment without a recognized app role
- **WHEN** a principal is not assigned any of the three app roles
- **THEN** they have no access to the resource group's resources via this system's RBAC assignments

### Requirement: Break-glass access is documented, not implicit
The system SHALL retain exactly one documented break-glass account (the Terraform bootstrap identity or a designated human) with subscription Owner outside Terraform management, for recovery if a role assignment misconfiguration locks out all other access.

#### Scenario: Break-glass account exists and is documented
- **WHEN** the RBAC design is reviewed
- **THEN** exactly one account is identified in documentation as holding Owner outside Terraform's managed role assignments

#### Scenario: Terraform apply does not remove the break-glass grant
- **WHEN** a `terraform plan` is generated
- **THEN** it SHALL NOT include a change that revokes the documented break-glass account's Owner assignment
