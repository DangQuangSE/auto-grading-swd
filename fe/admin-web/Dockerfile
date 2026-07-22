FROM node:20-alpine AS build
WORKDIR /app

COPY fe/admin-web/package.json fe/admin-web/package-lock.json* ./
RUN npm install

COPY fe/admin-web/. .

ARG VITE_API_BASE_URL=http://localhost:5500
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ARG VITE_GOOGLE_CLIENT_ID=""
ENV VITE_GOOGLE_CLIENT_ID=$VITE_GOOGLE_CLIENT_ID
ARG VITE_COURSE_ENROLLMENT_ENABLED=false
ENV VITE_COURSE_ENROLLMENT_ENABLED=$VITE_COURSE_ENROLLMENT_ENABLED
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY fe/admin-web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
