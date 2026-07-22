FROM node:20-alpine AS build
WORKDIR /app

COPY fe/user-web/package.json fe/user-web/package-lock.json* ./
RUN npm install

COPY fe/user-web/. .

ARG VITE_API_BASE_URL=http://localhost:5500
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ARG VITE_GOOGLE_CLIENT_ID=""
ENV VITE_GOOGLE_CLIENT_ID=$VITE_GOOGLE_CLIENT_ID
ARG VITE_ADMIN_WEB_URL=http://localhost:5174
ENV VITE_ADMIN_WEB_URL=$VITE_ADMIN_WEB_URL
ARG VITE_COURSE_ENROLLMENT_ENABLED=false
ENV VITE_COURSE_ENROLLMENT_ENABLED=$VITE_COURSE_ENROLLMENT_ENABLED
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY fe/user-web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
