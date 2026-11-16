# Сборка фронта и раздача статики через nginx. Собирается из папки frontend/: docker build -t dekanat-web frontend
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:1.29-alpine
# Официальный образ nginx подставляет переменные окружения только в шаблоны из /etc/nginx/templates/.
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
ENV API_PROXY_TARGET=http://api:8080
EXPOSE 80
