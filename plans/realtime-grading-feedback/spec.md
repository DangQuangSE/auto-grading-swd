# Spec: Real-time AI Grading Feedback (SignalR)

**Date:** 2026-07-16
**Status:** Ready

---

## Problem Statement
Khi sinh viên submit file bài tập, quá trình trích xuất tài liệu và chấm điểm bằng AI (chạy bất đồng bộ) diễn ra ngầm. Hiện tại không có bất kỳ phản hồi nào về tiến độ (loading/processing) cho người dùng, gây hoang mang, UX kém và dễ dẫn đến việc sinh viên bấm nộp lại nhiều lần.

---

## User Stories

- **[P1]** As a Sinh Viên, I want to thấy được thanh tiến trình các bước xử lý (Ghi nhận -> Trích xuất -> Đang chấm -> Hoàn tất) so that tôi biết bài nộp của mình đang được AI xử lý.
  Accepted when: UI tự động chuyển sang trang Result và cập nhật trạng thái realtime không cần reload trang.

- **[P1]** As a Sinh Viên, I want to nhấn nút "Thử chấm lại" khi hệ thống báo lỗi xử lý so that tôi không phải mất công chọn file và upload lại từ đầu.
  Accepted when: Nút "Thử chấm lại" gọi API trigger lại quá trình trích xuất/chấm điểm, thanh tiến trình tự động quay lại trạng thái xử lý.

- **[P2]** As a Developer, I want to tích hợp SignalR Hub vào `notification-api` so that trạng thái từ RabbitMQ có thể push xuống frontend thông qua Gateway.
  Accepted when: Gateway route thành công các request WebSocket đến `notification-api` và nhận event.

---

## Functional Requirements

1. FR-01: Mở rộng `notification-api` với SignalR Hub (`/hubs/notifications`). Cấu hình API Gateway forward request tới endpoint này.
2. FR-02: `submission-api` và `grading-api` publish các Integration Events (`SubmissionStatusChangedEvent`) lên RabbitMQ mỗi khi hoàn thành 1 bước xử lý.
3. FR-03: `notification-api` subscribe sự kiện trên RabbitMQ, dùng SignalR Client ID (mapping với User ID) để gửi message về đúng browser của sinh viên.
4. FR-04: UI Frontend bổ sung 컴포넌트 thanh tiến trình có 4 trạng thái tương ứng. Lắng nghe event từ SignalR để update UI.
5. FR-05: Xây dựng API `POST /api/submissions/{id}/retry` (tại `submission-api`) để trigger luồng xử lý bằng RabbitMQ với file đã lưu sẵn trong MinIO (sử dụng khi trạng thái là Failed).

---

## Non-Functional Requirements

- Performance: Độ trễ từ khi event phát ra ở RabbitMQ đến khi UI update < 2 giây.
- Availability: SignalR client ở Frontend (React/Vue) tự động reconnect nếu đứt mạng (withAutomaticReconnect).

---

## Success Criteria

- [ ] Chạy thực tế sinh viên nộp bài: nhìn thấy thanh tiến trình nhảy đủ 4 bước (tùy vào thời gian xử lý thực) mà không cần F5.
- [ ] Giả lập lỗi tại `grading-api`, UI báo lỗi và hiện nút "Thử chấm lại".
- [ ] Click "Thử chấm lại", quy trình chấm chạy lại từ đầu thành công.
- [ ] Logs không báo lỗi Connection Refused trên RabbitMQ/SignalR.

---

## Out of Scope

- Không lưu trữ toàn bộ lịch sử (logs chi tiết) của từng bước vào Database, DB chỉ lưu trạng thái hiện tại (enum Status) của Submission.

---

## Assumptions

- Gateway hỗ trợ chuẩn WebSocket.
- JWT token có thể được truyền qua QueryString (`?access_token=...`) để xác thực kết nối SignalR (do WebSockets ở Browser không gửi được Authorization Header).

---

## [NEEDS CLARIFICATION]
*(Đã clear, không có blocker)*
