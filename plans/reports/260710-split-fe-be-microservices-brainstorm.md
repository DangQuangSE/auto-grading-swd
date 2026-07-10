# Brainstorm: Tách FE/BE và migrate Supabase → ASP.NET Core Microservices

**Date:** 2026-07-10

## Bối cảnh
Dự án hiện tại (`auto-grading-swd`) là 1 source đơn: FE React+Vite+TS, BE là Supabase
(Postgres + Auth + Storage + 2 Edge Functions). Domain: chấm điểm báo cáo đồ án IT
(.docx) và sơ đồ kiến trúc (.drawio) theo rubric giảng viên định nghĩa, có AI gợi ý
điểm (OpenRouter), giảng viên duyệt điểm cuối.

Mục tiêu: refactor để đáp ứng [requirment.md](../../requirment.md) — kiến trúc
ASP.NET Core Microservices (4+ services, API Gateway, RabbitMQ, JWT, Background Jobs,
SQL Server/EF Core), tách thư mục `fe/` và `be/` độc lập, User Web + Admin Web riêng.

## Ideas Explored
- **Lai Supabase + ASP.NET Core** (giữ Auth/Storage, chỉ viết microservices cho nghiệp
  vụ): tiết kiệm effort nhưng không đúng checklist đề bài (đề yêu cầu SQL Server + JWT
  tự phát hành). → **Loại** (user chọn bỏ hẳn Supabase).
- **Bỏ hẳn Supabase, viết mới ASP.NET Core + SQL Server**: đúng chuẩn đề bài nhất, phải
  viết lại auth/DB/storage/logic. → **Chọn**.
- **Số lượng microservices**: 4 (gộp Notification vào Grading) vs 5 (Notification tách
  riêng). → Chọn **5** để tách rõ concern và thừa mức tối thiểu.
- **File storage thay Supabase Storage**: disk volume vs MinIO vs cloud thật.
  → Chọn **MinIO** (S3-compatible, chạy trong Docker Compose).
- **FE**: giữ 1 app đổi tầng API vs tách User Web + Admin Web. → Chọn **tách 2 app**.
- **Phạm vi đợt này**: scaffold toàn bộ vs migrate trọn 1 service mẫu vs chỉ tách thư
  mục. → Chọn **scaffold toàn bộ cấu trúc fe/ + be/** (5 service + gateway +
  docker-compose) chạy được ở mức skeleton, logic điền sau.

## User's Direction
Bỏ hẳn Supabase → viết mới toàn bộ BE bằng ASP.NET Core + SQL Server. Chấp nhận đề xuất
5 microservices + API Gateway (YARP). FE tách thành 2 app riêng (User Web + Admin Web).
Storage dùng MinIO. Đợt này ưu tiên **dựng xương sống** (scaffold) toàn bộ để chạy được
end-to-end ở mức skeleton, chưa cần logic đầy đủ.

## Ranh giới Microservices đề xuất (map từ code cũ)
| Service | Gánh phần code cũ | Bảng own |
| --- | --- | --- |
| Identity | authService, AuthProvider, roles | users, roles |
| Catalog | rubricService, useSubjects/useRubrics, rubric parser | subjects, assignments, rubrics, rubric_criteria |
| Submission | extract-submission edge fn, submissionService, docx/drawio parsers | submissions, extracted_artifacts (+ MinIO) |
| Grading | grade-submission edge fn, reviewService, OpenRouter | ai_grading_runs, ai_criterion_scores, final_grades, grade_publications |
| Notification | audit_events, gửi email/thông báo | notifications, audit_events |

- **API Gateway** (YARP) validate JWT do Identity ký, route tới services.
- **Events (RabbitMQ, 5+)**: SubmissionUploaded → ArtifactsExtracted → AiGradingCompleted
  → GradePublished → RubricParsed → UserRegistered.
- **Background Jobs (Hangfire, 2+)**: extraction async, AI grading async, overdue-assignment
  reminder.

## Open Questions (cho /ck:plan xử lý)
- Migration data từ Supabase Postgres hiện có sang SQL Server — có cần import dữ liệu
  thật không, hay bắt đầu DB trống? (giả định: DB trống, chỉ giữ schema semantics)
- Cách chia database: mỗi service 1 database riêng vs 1 SQL Server instance nhiều schema.
- Docx/drawio parser hiện viết bằng TypeScript (Deno) — port sang C# hay giữ dạng
  worker Node riêng được Submission Service gọi? (ảnh hưởng lớn tới effort).
- FE hiện chưa có role-based route guard (chỉ RLS) — khi tách 2 app phải tự implement
  authorization phía client + gateway.
- Có cần giữ Vitest/test hiện có cho FE sau khi tách không.

## Risks
1. **Port parser docx/.drawio từ TS/Deno sang C#** là phần rủi ro & tốn công nhất — logic
   parse phức tạp, dễ vỡ tính năng cốt lõi. Cần quyết sớm (port vs wrap Node worker).
2. **Viết lại Auth/JWT + RLS→authorization**: RLS của Postgres bảo vệ dữ liệu ở tầng DB;
   khi bỏ đi phải tái hiện toàn bộ luật phân quyền ở tầng application, dễ sót lỗ hổng.
3. **Scope creep**: 5 services + gateway + 2 FE app + MinIO + RabbitMQ + Hangfire là khối
   lượng lớn cho đồ án — cần kỷ luật "skeleton trước, logic sau" để không sa lầy.
