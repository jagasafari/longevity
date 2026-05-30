FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:alpine
WORKDIR /usr/share/nginx/html

COPY --from=build /src/dist ./
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
