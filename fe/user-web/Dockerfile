FROM node:20-alpine AS build
WORKDIR /app

COPY fe/user-web/package.json fe/user-web/package-lock.json* ./
RUN npm install

COPY fe/user-web/. .

ARG VITE_API_BASE_URL=http://localhost:5500
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY fe/user-web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
