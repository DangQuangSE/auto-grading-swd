# Spec: Student đăng ký lớp theo từng môn

**Date:** 2026-07-22
**Status:** Ready

---

## Problem Statement

Student hiện chỉ có một `ClassId` chung nên không thể thuộc các lớp khác nhau theo từng môn. Hệ thống cần cho student tự chọn lớp từ danh mục môn và lớp do admin quản lý, nhưng chỉ đối với môn đang mở đăng ký.

---

## User Stories

- **[P1]** Là lecturer hoặc admin, tôi muốn tạo môn; và là admin, tôi muốn mở hoặc đóng đăng ký để kiểm soát môn nào student được phép đăng ký.
  Accepted when: môn đóng đăng ký không xuất hiện trong danh sách đăng ký của student; môn mở đăng ký xuất hiện mà không cần student nhập mã thủ công.

- **[P1]** Là admin, tôi muốn mỗi lớp được gắn với đúng một môn để hệ thống chỉ hiển thị các lớp hợp lệ cho môn đó.
  Accepted when: khi tạo hoặc cập nhật lớp, admin phải chọn một môn tồn tại và API từ chối `SubjectId` không hợp lệ.

- **[P1]** Là student, tôi muốn chọn một lớp cho từng môn đang mở đăng ký để hồ sơ phản ánh đúng lớp học phần của mình.
  Accepted when: student có thể lưu `SWD -> SE1830` và `SWR -> SE1829` đồng thời; mỗi môn chỉ có tối đa một lớp.

- **[P2]** Là admin, tôi muốn xem và điều chỉnh đăng ký của student để xử lý trường hợp chọn nhầm.
  Accepted when: admin có thể xem và đổi một đăng ký mà không ảnh hưởng đăng ký ở môn khác.

- **[P3]** _(Ngoài phạm vi MVP: giới hạn sĩ số, thời hạn đăng ký chi tiết, duyệt đăng ký và danh sách chờ.)_

---

## Functional Requirements

1. FR-01: Admin và lecturer được tạo môn; chỉ admin được tạo/quản lý lớp và thay đổi trạng thái mở đăng ký của môn.
2. FR-02: Mỗi môn có trạng thái đăng ký `open` hoặc `closed`, mặc định `closed` khi mới tạo.
3. FR-03: Mỗi lớp phải tham chiếu đúng một môn; mã/tên lớp vẫn do admin quản lý.
4. FR-04: Hệ thống lưu đăng ký bằng quan hệ `StudentId + SubjectId + ClassId`, với ràng buộc duy nhất trên `StudentId + SubjectId`.
5. FR-05: Student có API tự xem danh sách đăng ký hiện tại của mình.
6. FR-06: Student chỉ nhận được danh sách môn có trạng thái `open` trong luồng đăng ký.
7. FR-07: Sau khi chọn môn, student chỉ nhận và chọn được các lớp thuộc môn đó.
8. FR-08: Student có thể tạo hoặc đổi đăng ký của chính mình khi môn đang `open`.
9. FR-09: API phải từ chối đăng ký nếu môn đang đóng, lớp không thuộc môn, student sửa dữ liệu của người khác, hoặc một mã định danh không tồn tại.
10. FR-10: Giao diện student có mục `Profile` hoặc `Thông tin cá nhân`, hiển thị email/định danh hiện có và khu vực `Môn học và lớp`.
11. FR-11: Mã môn và lớp được chọn bằng dropdown từ API; không có ô nhập mã tự do.
12. FR-12: Khi đổi lựa chọn môn, dropdown lớp được làm mới và không giữ một lớp không thuộc môn mới.
13. FR-13: Migration chuyển đổi mô hình hiện tại mà không âm thầm gán một lớp cũ vào sai môn; dữ liệu chưa đủ ánh xạ phải được giữ để admin xử lý hoặc ghi nhận rõ trong migration.
14. FR-14: `StudentEnrollment` là nguồn dữ liệu chính cho lớp học phần; roster, bulk import và các luồng ghi lớp mới phải nhận đủ Subject và Class rồi ghi enrollment thay vì `User.ClassId`.
15. FR-15: Sau khi admin ánh xạ lớp cũ với môn và báo cáo đối soát không còn dữ liệu mơ hồ, hệ thống ngừng đọc/ghi rồi xóa `User.ClassId` bằng migration riêng.
16. FR-16: Nếu cần quản lý lớp hành chính/cohort trong tương lai, phải dùng trường hoặc entity riêng; không tái sử dụng lớp học phần hoặc `User.ClassId`.

---

## Non-Functional Requirements

- Performance: API danh sách môn/lớp/đăng ký có p95 dưới 500 ms với 10.000 đăng ký và phân trang tối đa 100 bản ghi mỗi request.
- Security: mọi endpoint ghi yêu cầu xác thực; thao tác danh mục yêu cầu role admin; student chỉ được ghi đăng ký của chính mình; tất cả quan hệ được kiểm tra phía server.
- Availability: thao tác lưu đăng ký phải atomic; lỗi validation không được tạo đăng ký dở dang hoặc trùng môn.

---

## Success Criteria

- [ ] Student lưu được ít nhất 2 cặp môn-lớp khác nhau trong cùng một tài khoản.
- [ ] 100% môn đóng đăng ký bị loại khỏi danh sách đăng ký của student và bị API từ chối nếu gọi trực tiếp.
- [ ] 100% lớp hiển thị trong dropdown thuộc môn đã chọn.
- [ ] Database từ chối đăng ký thứ hai cho cùng một cặp student-môn.
- [ ] Kiểm thử authorization xác nhận student không thể quản lý danh mục hoặc sửa đăng ký của student khác.
- [ ] Các kiểm thử backend/frontend hiện có vẫn vượt qua sau migration.
- [ ] Roster/import tạo hoặc cập nhật đúng enrollment theo Subject và Class mà không ghi `User.ClassId`.
- [ ] Migration xóa `User.ClassId` chỉ chạy khi preflight reconciliation báo 0 lớp/user chưa ánh xạ và 0 enrollment mâu thuẫn.
- [ ] Trước khi xóa cột, hệ thống ghi nhận 7 ngày liên tiếp và ít nhất 1.000 thao tác với 0 legacy write, 0 mismatch và 0 dead-letter/pending quá hạn.

---

## Out of Scope

- Giới hạn sĩ số, danh sách chờ và quy trình duyệt đăng ký.
- Lịch học, phòng học và xung đột thời khóa biểu.
- Student tự tạo hoặc nhập tự do mã môn/mã lớp.
- Đồng bộ dữ liệu lớp với hệ thống bên ngoài.
- Quản lý lớp hành chính/cohort; nếu phát sinh sẽ dùng mô hình riêng.

---

## Assumptions

- Mỗi student chỉ chọn một lớp trong một môn tại một thời điểm.
- Student được đổi lớp khi môn còn mở đăng ký; khi môn đóng, đăng ký hiện tại chỉ được xem.
- Một lớp thuộc đúng một môn; nhiều lớp có thể dùng cùng mã hiển thị nếu database sử dụng ID riêng, nhưng tổ hợp môn và mã lớp phải không trùng.
- Admin và lecturer có thể tạo môn; admin là nguồn quản lý duy nhất cho lớp và trạng thái mở đăng ký.
- Dữ liệu `User.ClassId` hiện tại là legacy lớp học phần, không phải lớp hành chính cần giữ lâu dài.
