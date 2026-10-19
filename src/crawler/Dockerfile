# Playwright base image ships matching browser binaries preinstalled — reinstalling
# them in a slimmer runtime image would just re-download the same browsers, so this
# stays single-stage rather than a multi-stage build+runtime split. The image tag's
# version MUST exactly match the `playwright` npm package version in package.json
# (pinned there, not a caret range) — a mismatch means the installed npm package
# looks for browser binaries this image doesn't have (Playwright ties browser
# binary revisions to the exact npm package version, not just the major).
FROM mcr.microsoft.com/playwright:v1.61.1-jammy AS build

WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npm run build && npm prune --omit=dev

FROM mcr.microsoft.com/playwright:v1.61.1-jammy AS final

# Patch fixable OS CVEs, and drop npm/corepack: the runtime only needs `node`, and
# their bundled dependencies (tar, pacote, ...) trip the Trivy gate.
RUN apt-get update && apt-get upgrade -y \
    && rm -rf /var/lib/apt/lists/* /usr/lib/node_modules/npm /usr/lib/node_modules/corepack \
       /usr/bin/npm /usr/bin/npx /usr/bin/corepack

# Numeric UID so the pod securityContext's runAsNonRoot (crawler-cronjob.yaml) can
# verify it. Playwright's browsers under /ms-playwright are world-readable.
RUN groupadd -r -g 10001 crawler && useradd -r -u 10001 -g crawler -d /app crawler
RUN mkdir -p /app && chown 10001:10001 /app
WORKDIR /app
COPY --from=build --chown=10001:10001 /app/dist ./dist
COPY --from=build --chown=10001:10001 /app/node_modules ./node_modules
COPY --from=build --chown=10001:10001 /app/package.json ./
USER 10001

# CronJob runs one crawl pass and exits (see infra/helm/templates/crawler-cronjob.yaml).
CMD ["node", "dist/index.js", "--run-now"]
