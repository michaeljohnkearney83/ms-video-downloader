# Build the static app with Node, serve it with unprivileged nginx (port 8080, non-root).
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# Empty = the app calls its own origin and nginx proxies /v1 to the Manager.
ARG VITE_MANAGER_URL=
ENV VITE_MANAGER_URL=${VITE_MANAGER_URL}
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS final

# The image runs envsubst over /etc/nginx/templates/*.template at start-up.
ENV MANAGER_URL=http://manager-api:8000 \
    NGINX_RESOLVER=127.0.0.11
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
