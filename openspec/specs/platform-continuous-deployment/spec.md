# platform-continuous-deployment Specification

## Purpose
CI that builds, scans, and deploys every NomadRules service to the single AKS cluster — merge to `main` results in a running change in the cluster without a manual `helm upgrade`, gated by human review and with no long-lived Azure credential in GitHub.

## Requirements
### Requirement: Every service has a CI build pipeline
Each of the six services (`api`, `crawler`, `summarizer`, `email-service`, `ingest`, `portal`, and the `db-migrations` runner) SHALL have a GitHub Actions workflow that builds and pushes a container image on changes to its source path.

#### Scenario: Change to a service with no prior pipeline
- **WHEN** a commit touching `src/portal/**` is pushed to `main`
- **THEN** a `portal` image-build workflow runs, builds the image, and pushes it to the container registry tagged with the commit SHA and `latest`

### Requirement: Container images are scanned before push
Every image-build workflow SHALL run a vulnerability scan against the built image before pushing, and SHALL fail the workflow on any CRITICAL or HIGH severity finding without an approved allow-list entry.

#### Scenario: Scan finds an unaddressed critical vulnerability
- **WHEN** the vulnerability scan finds a CRITICAL severity CVE not present in the allow-list
- **THEN** the workflow fails and the image is not pushed to the registry

#### Scenario: Scan finds only allow-listed findings
- **WHEN** the vulnerability scan finds only CVEs present in the reviewed allow-list
- **THEN** the workflow proceeds to push the image

### Requirement: Merge to main deploys to the AKS cluster
Each service's workflow SHALL include a deploy job that runs `helm upgrade` against the single AKS cluster after a successful image build on merge to `main`, gated by a required-reviewer GitHub Environment checkpoint.

#### Scenario: Build succeeds and reviewer approves
- **WHEN** the image build and scan succeed on a `main` merge, and a required reviewer approves the `deploy` GitHub Environment
- **THEN** `helm upgrade` runs against the AKS cluster, updating the service's running Deployment/CronJob/Job to the new image tag

#### Scenario: Build fails
- **WHEN** the image build or vulnerability scan fails
- **THEN** the deploy job does not run and the cluster's running workload is unchanged

### Requirement: Cluster deploys use no long-lived Azure credential
The deploy job SHALL authenticate to Azure (for `az aks get-credentials`) via GitHub OIDC federated credential, with no client secret stored in GitHub Actions secrets.

#### Scenario: Deploy job authenticates
- **WHEN** the deploy job runs
- **THEN** it obtains Azure credentials via OIDC federation scoped to this repository and the `main` branch, with no stored client secret referenced
