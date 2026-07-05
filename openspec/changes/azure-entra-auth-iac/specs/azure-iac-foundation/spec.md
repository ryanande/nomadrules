## ADDED Requirements

### Requirement: All Entra and RBAC resources are defined in Terraform
The system SHALL define every Entra app registration, app role, and Azure RBAC role assignment introduced by this change as Terraform resources under `infra/terraform/`, and SHALL NOT create or modify them through the Azure portal or CLI as standard practice.

#### Scenario: Entra app registration exists as a Terraform resource
- **WHEN** `infra/terraform/` is applied
- **THEN** the subscriber-facing CIAM app registration and the workforce admin app registration both exist as `azuread_application` (or equivalent) resources in the Terraform state

#### Scenario: Drift is detectable
- **WHEN** `terraform plan` is run against the current Azure state
- **THEN** any manual out-of-band change to a managed app registration, app role, or role assignment appears as a plan diff

### Requirement: CI plans on PR and applies on merge
The system SHALL run `terraform plan` on every pull request touching `infra/terraform/` and `terraform apply` on merge to `main`, authenticated via GitHub OIDC federated credential rather than a stored client secret.

#### Scenario: PR triggers a plan
- **WHEN** a pull request modifies files under `infra/terraform/`
- **THEN** CI runs `terraform plan` and posts or surfaces the plan output for review

#### Scenario: Merge triggers an apply
- **WHEN** a PR touching `infra/terraform/` merges to `main`
- **THEN** CI runs `terraform apply` using a short-lived token obtained via OIDC federation

#### Scenario: No long-lived cloud credential in CI
- **WHEN** the GitHub Actions workflow configuration is inspected
- **THEN** no Azure client secret or long-lived credential is present in repository or workflow secrets for the Terraform identity

### Requirement: Remote state with locking
The system SHALL store Terraform state in a remote Azure Storage backend with state locking enabled, and SHALL NOT rely on local state files for any environment applied via CI.

#### Scenario: Concurrent apply is prevented
- **WHEN** two `terraform apply` runs are triggered concurrently
- **THEN** the second run is blocked by the state lock until the first completes

#### Scenario: State is not committed to git
- **WHEN** the repository is inspected
- **THEN** no `.tfstate` file is tracked in git
