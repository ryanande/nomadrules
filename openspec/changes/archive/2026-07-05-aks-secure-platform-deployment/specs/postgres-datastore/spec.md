## ADDED Requirements

### Requirement: Shared datastore is a managed Postgres server
The system SHALL use Azure Database for PostgreSQL Flexible Server as the shared datastore for all services, provisioned via Terraform.

#### Scenario: Terraform provisions the datastore
- **WHEN** `terraform apply` runs against the infra root
- **THEN** an `azurerm_postgresql_flexible_server` resource exists and is reachable only from within the AKS cluster's VNet

### Requirement: No public network access to the datastore
The Postgres server SHALL NOT be reachable from the public internet; access is limited to private VNet integration.

#### Scenario: Connection attempt from outside the VNet
- **WHEN** a connection to the Postgres server is attempted from outside the AKS cluster's VNet
- **THEN** the connection is refused at the network layer

### Requirement: Connection credentials sourced from Key Vault
The Postgres connection string SHALL be stored as a Key Vault secret and resolved by each service at runtime via its Workload Identity — never as a plaintext environment variable literal in a Helm value or manifest.

#### Scenario: API resolves its connection string
- **WHEN** the `api` service pod starts
- **THEN** it reads the Postgres connection string from a Key Vault-mounted secret via its own Workload Identity, not from a plaintext `values.yaml` entry

### Requirement: Local development parity
Local development (`docker-compose`) SHALL run Postgres as a container so schema and query behavior match production.

#### Scenario: Developer runs the stack locally
- **WHEN** a developer runs `docker-compose up`
- **THEN** a Postgres container starts and the API and migration runner connect to it using the Postgres dialect
