# Brainstorm: Real-time Feedback for Submission Grading

**Date:** 2026-07-16

## Ideas Explored
- **Giao diện Polling:** Frontend gọi API định kỳ mỗi vài giây để check trạng thái chấm bài. (Dễ triển khai, hơi tốn request).
- **Real-time Push (SignalR):** Dùng WebSockets để backend push trạng thái realtime xuống frontend ngay lập tức thông qua notification-api. (Trải nghiệm mượt mà, xịn xò nhất).
- **Fire-and-Forget (Chỉ email/notification):** Nhận bài xong cho user rời đi, lúc nào chấm xong thì báo notification in-app/email. (Đơn giản, kém tương tác trực tiếp).

## User's Direction
Người dùng chọn **Hướng 2 (Real-time Push qua SignalR)** vì mong muốn có trải nghiệm mượt mà nhất và ghi điểm kỹ thuật cao cho đồ án.
Yêu cầu hiển thị 4 trạng thái chi tiết: Đã ghi nhận file ➔ Đang trích xuất nội dung ➔ AI đang đánh giá ➔ Hoàn tất.
Yêu cầu có xử lý lỗi: Hiển thị nút "Thử chấm lại" nếu tiến trình bị lỗi giữa chừng (timeout, lỗi trích xuất) mà không phải nộp lại file.

## Open Questions
Tất cả các câu hỏi lớn đã được giải quyết. Quá trình lập trình (planning) sẽ cần xác định chính xác các Event được phát ra ở RabbitMQ từ `submission-api` và `grading-api` để đồng bộ với UI.

## Risks
- **SignalR Connection:** Cần đảm bảo frontend kết nối WebSocket thành công, tự động reconnect.
- **Gateway Config:** Phải đảm bảo gateway (Ocelot/YARP/Nginx) forward WebSocket (ws:// / wss://) đúng cách tới `notification-api`.
- **Logic Retry:** Cần bổ sung 1 endpoint API để trigger quá trình xử lý lại dựa trên Submission ID cũ mà không yêu cầu gửi file vật lý lại.
