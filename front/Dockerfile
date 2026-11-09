FROM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine

# Render only the backend origin; preserve nginx variables such as $uri.
COPY nginx.conf /etc/nginx/api-proxy.conf.template
COPY --chmod=755 docker/15-api-proxy-env.sh /docker-entrypoint.d/15-api-proxy-env.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/15-api-proxy-env.sh

COPY --from=build /app/dist/uniflow-auto-parts/browser/ /usr/share/nginx/html/

EXPOSE 8080
