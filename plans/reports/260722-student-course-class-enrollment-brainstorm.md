# Brainstorm: Student đăng ký lớp theo từng môn

**Date:** 2026-07-22

## Ideas Explored

- Lưu một lớp chung trong hồ sơ student: đơn giản nhưng không thể biểu diễn việc một student học SWD ở SE1830 và SWR ở SE1829.
- Cho student nhập tự do mã môn và mã lớp: ít giao diện quản trị hơn nhưng dễ sai mã và tạo dữ liệu không hợp lệ.
- Admin gán toàn bộ môn/lớp cho từng student: kiểm soát tốt nhưng làm tăng đáng kể thao tác quản trị.
- Student tự chọn lớp theo từng môn đang mở đăng ký: cân bằng giữa luồng đơn giản và tính toàn vẹn dữ liệu; dropdown dùng dữ liệu do admin tạo.

## User's Direction

Chọn phương án student tự đăng ký. Admin hoặc lecturer có thể tạo môn ở trạng thái đóng; student chỉ nhìn thấy các môn admin đã mở đăng ký và chỉ chọn được lớp thuộc môn tương ứng. Mã môn và mã lớp không được nhập tự do.

## Open Questions

- Không còn câu hỏi chặn MVP. Mặc định mỗi student chỉ có một lớp cho mỗi môn và có thể đổi lớp khi môn còn mở đăng ký.

## Risks

- Mô hình hiện tại lưu một `ClassId` trực tiếp trên user; migration phải giữ dữ liệu cũ an toàn.
- `Class` hiện chưa thuộc một `Subject`; dữ liệu lớp cũ cần cách ánh xạ hoặc trạng thái chưa gán môn.
- Cần tách rõ quyền: lecturer/admin được tạo môn, nhưng chỉ admin được mở đăng ký và quản lý lớp.
