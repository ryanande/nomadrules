#!/usr/bin/env bash
# Prints a JSON array with one entry per service. Each image is tagged with the commit that
# last touched the service's source, so the tag is stable until that source changes and the
# result is the same no matter which push or run asks. "exists" says whether the tag is
# already in the registry. Needs full git history and a registry login.
set -euo pipefail

owner="${GITHUB_REPOSITORY_OWNER,,}"
# Changing the workflow or this script (which defines contexts and image names) rebuilds everything.
inputs=(".github/workflows/images.yml" ".github/scripts/resolve-images.sh")

# name | source path | docker context | helm values key | image name
services=(
  "api|src/api|src/api/NomadRules.Api|api|nomadrules-api"
  "crawler|src/crawler|src/crawler|crawler|nomadrules-crawler"
  "db-migrations|src/db-migrations|src/db-migrations/NomadRules.DbMigrations|migrations|nomadrules-dbmigrations"
  "email-delivery|src/email-service|src/email-service/NomadRules.EmailDelivery|emailDelivery|nomadrules-email-delivery"
  "ingest|src/ingest|src/ingest/NomadRules.Ingest|ingest|nomadrules-ingest"
  "portal|src/portal|src/portal|portal|nomadrules-portal"
  "summarizer|src/summarizer|src/summarizer/NomadRules.Summarizer|summarizer|nomadrules-summarizer"
)

for entry in "${services[@]}"; do
  IFS='|' read -r name path context key image <<<"$entry"
  sha="$(git log -1 --format=%H -- "$path" "${inputs[@]}")"
  ref="ghcr.io/${owner}/${image}"
  # Only a definite "not found" means missing; auth errors, outages and rate limits must fail the
  # script rather than silently skip a deploy.
  if out="$(docker manifest inspect "${ref}:${sha}" 2>&1)"; then
    exists=true
  elif grep -qiE 'no such manifest|manifest unknown|not found|404' <<<"$out"; then
    exists=false
  else
    echo "cannot check ${ref}:${sha}: $out" >&2
    exit 1
  fi
  jq -cn --arg name "$name" --arg context "$context" --arg key "$key" \
    --arg image "$ref" --arg sha "$sha" --argjson exists "$exists" \
    '{name: $name, context: $context, key: $key, image: $image, sha: $sha, exists: $exists}'
done | jq -cs .
