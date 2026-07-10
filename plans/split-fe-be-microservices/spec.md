# Spec: Tách FE/BE và scaffold ASP.NET Core Microservices (thay Supabase)

**Date:** 2026-07-10
**Status:** Draft

---

## Problem Statement
Dự án auto-grading hiện là 1 source đơn (FE React + BE Supabase). Đồ án yêu cầu kiến
trúc ASP.NET Core Microservices (4+ services, API Gateway, RabbitMQ, JWT, Background
Jobs, SQL Server). Cần tách repo thành `fe/` và `be/` độc lập, thay hoàn toàn Supabase
bằng ASP.NET Core + SQL Server + MinIO, dựng xương sống chạy được ở mức skeleton.

---

## User Stories

- **[P1]** As a dev, I want repo tách rõ `fe/` và `be/` để 2 phần build/deploy độc lập.
  Accepted when: `fe/` chứa toàn bộ React app, `be/` chứa .NET solution; mỗi bên có
  README + cách chạy riêng; root docker-compose khởi động cả 2.

- **[P1]** As a dev, I want be/ có 5 microservices + API Gateway (YARP) scaffold sẵn.
  Accepted when: mỗi service là 1 ASP.NET Core project có health endpoint; gateway route
  tới cả 5 service; `docker compose up` chạy toàn bộ không lỗi.

- **[P1]** As a user, I want Identity Service phát hành JWT để đăng nhập.
  Accepted when: POST /auth/login trả JWT hợp lệ; gateway validate JWT; role
  (student/lecturer/admin) nằm trong claim.

- **[P1]** As a dev, I want User Web và Admin Web tách thành 2 app FE riêng.
  Accepted when: `fe/user-web` và `fe/admin-web` build độc lập; mỗi app gọi API qua
  gateway thay vì Supabase SDK.

- **[P1]** As a dev, I want message broker + background job scaffold sẵn.
  Accepted when: RabbitMQ chạy trong compose; có ít nhất 1 publisher + 1 subscriber
  demo cho `SubmissionUploaded`; Hangfire dashboard truy cập được với ≥1 job đăng ký.

- **[P2]** As a dev, I want mỗi service có EF Core + migration SQL Server khởi tạo schema.
  Accepted when: chạy migration tạo được bảng của từng service trên SQL Server.

- **[P2]** As a user, I want luồng upload → extract → grade → review hoạt động end-to-end
  trên nền mới (port logic từ Edge Functions).

- **[P3]** _(out of scope) Import dữ liệu thật từ Supabase Postgres sang SQL Server._
- **[P3]** _(out of scope) CI/CD pipeline, k8s deploy — chỉ dừng ở Docker Compose._

---

## Functional Requirements

1. FR-01: Repo có 2 thư mục gốc `fe/` và `be/`; code FE hiện tại chuyển toàn bộ vào `fe/`
   không mất tính năng build (`npm run build` pass trong `fe/`).
2. FR-02: `be/` là 1 .NET solution gồm 5 project service: Identity, Catalog, Submission,
   Grading, Notification + 1 project ApiGateway (YARP) + project(s) Shared/Contracts.
3. FR-03: API Gateway route: `/identity/*`, `/catalog/*`, `/submissions/*`, `/grading/*`,
   `/notifications/*` tới đúng service; validate JWT bearer.
4. FR-04: Identity Service: đăng ký/đăng nhập, phát hành JWT (claim role), 3 role
   student/lecturer/admin; các service khác authorize theo role trên endpoint.
5. FR-05: Mỗi service dùng EF Core + SQL Server, database/schema riêng theo bảng đã map.
6. FR-06: File .docx/.drawio lưu trên MinIO (S3 API); Submission Service upload/đọc file
   từ MinIO thay Supabase Storage.
7. FR-07: RabbitMQ tích hợp; định nghĩa ≥5 event: SubmissionUploaded, ArtifactsExtracted,
   AiGradingCompleted, GradePublished, RubricParsed, UserRegistered (publisher/subscriber
   theo bảng thiết kế).
8. FR-08: ≥2 Background Job (Hangfire): (a) extraction job, (b) AI-grading job; job
   thứ 3 tuỳ chọn: overdue-assignment reminder.
9. FR-09: FE tách `fe/user-web` (student/lecturer) và `fe/admin-web` (admin); mỗi app
   có tầng service gọi REST qua gateway (thay `supabaseClient`).
10. FR-10: `docker-compose.yml` ở root khởi động: gateway, 5 service, SQL Server, RabbitMQ,
    MinIO, 2 FE app — `docker compose up` chạy được skeleton end-to-end.
11. FR-11: Grading Service giữ tích hợp OpenRouter (env cấu hình model) như Edge Function cũ.

---

## Non-Functional Requirements

- Performance: skeleton — không đặt mục tiêu latency; chỉ yêu cầu health endpoint < 1s.
- Security: JWT HS256/RS256, secret qua env; không hardcode key; MinIO/DB credential qua
  env; gateway là điểm vào duy nhất từ ngoài.
- Availability: chạy local qua Docker Compose; mỗi service restart độc lập không sập cả hệ.
- Maintainability: contracts (event/DTO) tách project dùng chung; mỗi service 1 solution
  folder rõ ràng.

---

## Success Criteria

- [ ] `git` repo có `fe/` và `be/` ở root; `fe/` build pass, `be/` `dotnet build` pass.
- [ ] `docker compose up` khởi động ≥ 9 container (gateway, 5 service, SQL Server,
      RabbitMQ, MinIO) không crash; tất cả health endpoint trả 200.
- [ ] Login qua gateway trả JWT hợp lệ; gọi 1 endpoint được bảo vệ thành công với token.
- [ ] Publish `SubmissionUploaded` → subscriber log nhận được event.
- [ ] Hangfire dashboard hiển thị ≥ 2 job đã đăng ký.
- [ ] `fe/user-web` và `fe/admin-web` build độc lập, mở được trang login gọi API mới.

---

## Out of Scope

- Import/migrate dữ liệu thật từ Supabase sang SQL Server (bắt đầu DB trống).
- Hoàn thiện đầy đủ business logic parse/grade (chỉ cần skeleton + luồng demo; logic đầy
  đủ để sprint sau).
- CI/CD, Kubernetes, cloud deploy (dừng ở Docker Compose local).
- Xoá thư mục `supabase/` ngay (giữ tham chiếu để port logic; dọn ở cuối).

---

## Assumptions

- DB bắt đầu trống — chỉ giữ ngữ nghĩa schema, không cần dữ liệu cũ.
- Mỗi microservice sở hữu database riêng (database-per-service) trên cùng 1 SQL Server
  instance trong compose.
- React app hiện tại đủ tốt làm nền cho `fe/user-web`; `fe/admin-web` có thể tách từ các
  trang lecturer/admin hiện có hoặc scaffold mới tối giản.
- Parser docx/.drawio sẽ được port sang C# HOẶC bọc thành Node worker — quyết định ở
  /ck:plan (xem NEEDS CLARIFICATION).

---

## [NEEDS CLARIFICATION]

- [ ] Parser docx/.drawio (hiện TypeScript/Deno): port sang C# native hay giữ Node worker
      để Submission Service gọi? — ảnh hưởng lớn tới effort của Submission Service.
- [ ] `fe/admin-web`: scaffold mới tối giản hay tách các trang admin/lecturer từ app hiện
      có? (hiện FE gộp student+lecturer, chưa có màn admin riêng).
- [ ] Database-per-service (5 DB riêng) có phải yêu cầu bắt buộc của đồ án, hay 1 DB nhiều
      schema là chấp nhận được?
