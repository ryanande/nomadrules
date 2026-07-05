FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Unprivileged variant: runs as non-root by default and listens on 8080 (nginx
# can't bind low ports as non-root) — matches the pod securityContext's
# runAsNonRoot in infra/helm/templates/portal-deployment.yaml.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
