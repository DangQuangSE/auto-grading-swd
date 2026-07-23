# Brainstorm: Tách logic khỏi Endpoint theo Layered Architecture

**Date:** 2026-07-23

## Ideas Explored

- **Physical multi-project Clean Architecture** (Domain.csproj / Application.csproj / Infrastructure.csproj / Api.csproj per service): ranh giới ép bằng compiler qua ProjectReference — đúng thuật ngữ "Clean Architecture" (Uncle Bob), khớp với precedent sẵn có của repo (BuildingBlocks/AutoGrading.Contracts, AutoGrading.Common). Bị bác bỏ: chi phí setup cao (x4 project mỗi service x 5 service), quá nặng so với nhu cầu thực tế.
- **MediatR / CQRS cho Application layer** (Command/Query + Handler): dễ test, thêm pipeline behavior (validation, logging) nhưng thêm thư viện + nhiều file nhỏ, thuật ngữ xa lạ với style hiện có của team. Bị bác bỏ.
- **Layered Architecture trong 1 project, chia theo folder** (tham chiếu trực tiếp từ 1 module Java thực tế: `constant / controller / domain / dto / interfaces / repository / service`): giữ nguyên 1 csproj/service như hiện tại, chỉ tổ chức lại thư mục theo layer. Ranh giới dựa vào convention + code review, không có compile-time enforcement. **→ Hướng được chọn.**

## User's Direction

Người dùng ban đầu nói "monolith kết hợp clean architecture" và đồng ý thử physical project separation khi được đề xuất, nhưng sau khi thấy bản vẽ 4-project thì quay lại xác nhận: không cần bọc 3 layer project riêng, ý muốn ban đầu chỉ là cấu trúc folder giống ví dụ Java (1 project, chia theo `constant/controller/domain/dto/interfaces/repository/service`). Đã xác nhận rõ tên đúng của pattern này là **Layered Architecture (N-Layer)**, không phải Clean Architecture thật, để tránh hiểu lầm thuật ngữ về sau.

Phạm vi triển khai: pilot ở service **Submission** trước, rút kinh nghiệm rồi mới áp dụng cho Grading/Catalog/các service còn lại.

Cấu trúc thư mục thống nhất cho Submission:
```
AutoGrading.Submission.Api/
├── Constant/       (mới)
├── Endpoints/       (giữ tên hiện tại, tương đương "Controller")
├── Domain/          (entity/enum, giữ nguyên)
├── Dto/             (mới — request/response tách khỏi Endpoint)
├── Interfaces/       (mới — ISubmissionRepository, IArtifactParser, ICatalogServiceClient)
├── Repository/       (đổi tên từ Data/, thêm implementation)
├── Service/          (mới — business logic kéo ra từ SubmissionsEndpoints.cs)
├── Clients/, Parsing/, Jobs/, Migrations/  (giữ nguyên vị trí, implement interface mới)
```

Quy tắc convention: Endpoint chỉ được gọi `Service` qua interface, không được inject `DbContext`/`Repository` trực tiếp.

## Open Questions

- Có viết characterization test trước khi di chuyển logic (để đảm bảo hành vi không đổi) hay coi đây là refactor cấu trúc thuần, verify bằng manual/regression test thủ công? → cần quyết ở `/ck:plan`.
- Interface (`ISubmissionRepository`, `IArtifactParser`, `ICatalogServiceClient`) có bắt buộc cho toàn bộ dependency hay chỉ nơi thực sự cần mock để unit test Service? → mặc định: bắt buộc cho Repository và các Client gọi ra ngoài (DB, HTTP, RabbitMQ), không bắt buộc cho logic thuần nội bộ.
- Ranh giới layer không được compiler ép buộc — rủi ro "tái bẩn" nếu không review kỹ khi áp dụng cho 4 service còn lại. Cần cách nào để enforce ngoài code review không (vd Roslyn analyzer/architecture test)? → để ngỏ, không bắt buộc cho pilot.

## Risks

- **Không có compile-time boundary**: sau vài sprint, Endpoint có thể lại bị nhét thẳng `DbContext` nếu review lỏng — rủi ro tái diễn đúng vấn đề đang muốn sửa.
- **Refactor cấu trúc không có test bảo vệ** dễ gây regression âm thầm cho luồng Submission (đang có Hangfire job + RabbitMQ consumer + parser OpenXml — nhiều side-effect khó test tay).
- **Đồng bộ pattern giữa 5 service**: nếu Submission pilot xong nhưng đặt tên/độ chi tiết khác đi khi áp dụng cho Grading/Catalog, sẽ tạo ra 5 kiểu tổ chức không nhất quán.
