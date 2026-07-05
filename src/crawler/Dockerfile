# Playwright base image ships matching browser binaries preinstalled — reinstalling
# them in a slimmer runtime image would just re-download the same browsers, so this
# stays single-stage rather than a multi-stage build+runtime split.
FROM mcr.microsoft.com/playwright:v1.47.0-jammy

WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npm run build

# CronJob runs one crawl pass and exits (see infra/helm/templates/crawler-cronjob.yaml).
CMD ["node", "dist/index.js", "--run-now"]
