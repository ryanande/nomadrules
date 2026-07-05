# Playwright base image ships matching browser binaries preinstalled — reinstalling
# them in a slimmer runtime image would just re-download the same browsers, so this
# stays single-stage rather than a multi-stage build+runtime split. The image tag's
# version MUST exactly match the `playwright` npm package version in package.json
# (pinned there, not a caret range) — a mismatch means the installed npm package
# looks for browser binaries this image doesn't have (Playwright ties browser
# binary revisions to the exact npm package version, not just the major).
FROM mcr.microsoft.com/playwright:v1.61.1-jammy

WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npm run build

# The base image runs as root by default; drop to a non-root user for the actual
# crawl (matches the pod securityContext's runAsNonRoot in crawler-cronjob.yaml).
# Playwright's preinstalled browsers under /ms-playwright are world-readable, so
# no extra chown is needed there — only /app (this user's own build output).
RUN groupadd -r crawler && useradd -r -g crawler -d /app crawler \
    && chown -R crawler:crawler /app
USER crawler

# CronJob runs one crawl pass and exits (see infra/helm/templates/crawler-cronjob.yaml).
CMD ["node", "dist/index.js", "--run-now"]
