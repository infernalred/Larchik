FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_API_BASE=/api
ARG VITE_IMPORT_MAX_FILE_SIZE_MB=10

ENV VITE_API_BASE=${VITE_API_BASE}
ENV VITE_IMPORT_MAX_FILE_SIZE_MB=${VITE_IMPORT_MAX_FILE_SIZE_MB}

COPY src/larchik_client/package*.json ./
COPY src/larchik_client/tsconfig*.json ./
COPY src/larchik_client/vite.config.* ./
COPY src/larchik_client/index.html ./
COPY src/larchik_client/src ./src
COPY src/larchik_client/public ./public

RUN npm ci
RUN npm run build

FROM caddy:2.8-alpine AS final
COPY --from=build /app/dist /usr/share/caddy
RUN printf ":80 {\n    root * /usr/share/caddy\n    try_files {path} /index.html\n    file_server\n}\n" > /etc/caddy/Caddyfile
EXPOSE 80
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
