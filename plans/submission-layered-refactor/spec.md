# Spec: Tách logic khỏi Endpoint — Layered Architecture cho Submission service

**Date:** 2026-07-23
**Status:** Draft

---

## Problem Statement

`AutoGrading.Submission.Api` hiện gói toàn bộ business logic, EF Core query, và mapping DTO trực tiếp trong `Endpoints/SubmissionsEndpoints.cs` (209 dòng). Điều này khiến logic khó test độc lập, khó tái sử dụng, và khó đọc khi endpoint phình to. Cần tổ chức lại theo Layered Architecture (1 project, chia folder theo layer) để tách rõ trách nhiệm, dùng Submission làm pilot trước khi áp dụng cho Grading/Catalog và các service còn lại.

---

## User Stories

- **[P1]** As a developer, I want business logic (create/list/get submission, orchestrate extraction) tách khỏi `SubmissionsEndpoints.cs` và chuyển vào `Service/`, so that endpoint chỉ còn bind request → gọi service → map response.
  Accepted when: `SubmissionsEndpoints.cs` không còn reference trực tiếp tới `SubmissionDbContext` hay entity EF Core nào.

- **[P1]** As a developer, I want data access (EF Core, RabbitMQ, HTTP client tới Catalog, parser OpenXml) đứng sau interface trong `Interfaces/`, so that `Service/` có thể được unit test bằng mock, không cần DB/HTTP thật.
  Accepted when: `ISubmissionRepository`, `IArtifactParser`, `ICatalogServiceClient` tồn tại trong `Interfaces/`; `Repository/`, `Parsing/`, `Clients/` implement đúng các interface này; `Service/` chỉ phụ thuộc vào interface, không phụ thuộc class cụ thể.

- **[P1]** As a developer, I want cấu trúc thư mục thống nhất (`Constant / Endpoints / Domain / Dto / Interfaces / Repository / Service` + `Clients / Parsing / Jobs / Migrations` giữ nguyên vị trí), so that khi áp dụng lại cho Grading/Catalog sau này có 1 template rõ ràng để theo.
  Accepted when: cấu trúc thư mục của Submission khớp đúng danh sách trên, không có file nghiệp vụ nào còn nằm ngoài các thư mục đã định nghĩa.

- **[P2]** As a developer, I want DTO tách khỏi record/class khai báo trực tiếp trong Endpoint file, so that request/response contract rõ ràng và tái dùng được.
  Accepted when: mọi request/response type dùng bởi `SubmissionsEndpoints.cs` nằm trong `Dto/`.

- **[P3]** _(out of scope — áp dụng pattern này cho Grading/Catalog/Gateway/các service còn lại — làm ở phase sau, sau khi rút kinh nghiệm từ pilot Submission)_

---

## Functional Requirements

1. FR-01: Di chuyển toàn bộ business logic hiện có trong `SubmissionsEndpoints.cs` (create, list, get, trigger extraction...) sang class(es) trong `Service/` (vd `SubmissionService`, `ExtractionOrchestrator`), có interface tương ứng.
2. FR-02: Định nghĩa `ISubmissionRepository` trong `Interfaces/`; đổi `Data/SubmissionDbContext.cs` → `Repository/SubmissionDbContext.cs`, thêm `Repository/SubmissionRepository.cs` implement interface, chứa toàn bộ EF Core query hiện đang nằm rải rác trong Endpoint.
3. FR-03: Định nghĩa `IArtifactParser` (move từ `Parsing/IArtifactParser.cs` sang `Interfaces/`); `Parsing/DocxReportParser.cs` và `Parsing/DrawioDiagramParser.cs` tiếp tục implement interface này tại vị trí cũ.
4. FR-04: Định nghĩa `ICatalogServiceClient` trong `Interfaces/`; `Clients/CatalogApiClient.cs` implement interface này tại vị trí cũ.
5. FR-05: Tạo `Dto/` chứa toàn bộ request/response type hiện khai báo inline trong `SubmissionsEndpoints.cs`.
6. FR-06: Tạo `Constant/` chứa error code / magic string nghiệp vụ hiện đang hardcode trong Endpoint.
7. FR-07: `Jobs/ExtractionJob.cs` và `Jobs/SubmissionUploadedHandler.cs` (RabbitMQ consumer) gọi vào `Service/` qua interface, không còn tự thực hiện logic nghiệp vụ hoặc gọi thẳng EF Core.
8. FR-08: Đăng ký DI cho toàn bộ interface → implementation mới trong `Program.cs`.

---

## Non-Functional Requirements

- Maintainability: `SubmissionsEndpoints.cs` giảm còn thuần request binding + response mapping (không có ngưỡng dòng cứng, nhưng không còn logic nghiệp vụ hay EF Core query nào).
- Testability: `Service/` phải mock được toàn bộ dependency ngoài (DB, HTTP, parser) qua interface trong `Interfaces/`.
- Compatibility: Không đổi API contract (route, request/response shape) của `SubmissionsEndpoints.cs` — đây là refactor cấu trúc nội bộ, không phải thay đổi hành vi.

---

## Success Criteria

- [ ] `SubmissionsEndpoints.cs` không còn `using` trực tiếp tới `SubmissionDbContext` hoặc bất kỳ EF Core entity/query nào.
- [ ] Toàn bộ dependency ngoài của `Service/` (repository, parser, catalog client) đi qua interface trong `Interfaces/`, xác nhận bằng việc `Service/` có thể được unit test với mock (không cần DB/HTTP thật khi chạy test).
- [ ] Cấu trúc thư mục của `AutoGrading.Submission.Api` khớp đúng: `Constant/ Endpoints/ Domain/ Dto/ Interfaces/ Repository/ Service/` (+ `Clients/ Parsing/ Jobs/ Migrations/` giữ nguyên).
- [ ] API contract (endpoint route, request/response DTO shape) không đổi so với trước refactor — verify bằng so sánh OpenAPI spec trước/sau.

---

## Out of Scope

- Áp dụng pattern này cho Grading, Catalog, Gateway, hoặc bất kỳ service nào khác ngoài Submission (sẽ là phase riêng sau khi pilot xong).
- Physical multi-project split (Domain.csproj/Application.csproj/Infrastructure.csproj riêng) — đã cân nhắc và loại bỏ, không nằm trong phạm vi spec này.
- MediatR/CQRS hoặc bất kỳ thay đổi pattern Application layer nào ngoài service class thuần.
- Thay đổi API contract, hành vi nghiệp vụ, hoặc schema database.

---

## Assumptions

- `Service/` dùng service class thuần (method trực tiếp, vd `SubmissionService.CreateAsync(...)`), không dùng MediatR/CQRS.
- Interface bắt buộc cho mọi dependency đi ra ngoài process hiện tại (DB, HTTP client, RabbitMQ, file parser); không bắt buộc cho logic thuần nội bộ không cần mock.
- Ranh giới layer được enforce bằng code review/convention, không có compiler/analyzer nào chặn vi phạm — chấp nhận rủi ro "tái bẩn" nếu review lỏng ở các service sau.
- Đây là refactor cấu trúc thuần (behavior-preserving), không thay đổi API contract hay business rule hiện có.

---

## [NEEDS CLARIFICATION]

- [ ] Có viết characterization test (test hành vi hiện tại) trước khi di chuyển logic để đảm bảo không regression, hay refactor trực tiếp và verify bằng kiểm thử thủ công? Cần quyết định ở `/ck:plan` vì ảnh hưởng trực tiếp tới trình tự các phase.
