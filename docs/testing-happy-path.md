# Happy-Path Test Flows (Demo Prep)

Mục tiêu: xác nhận toàn bộ chuỗi nghiệp vụ chính chạy được end-to-end trước khi demo —
đăng ký/đăng nhập → tạo subject/assignment/rubric → sinh viên nộp bài → extraction job →
AI grading job → publish điểm → notification. Đây là **happy case only** (input hợp lệ,
không test lỗi/permission/edge case). Test lỗi sẽ là tài liệu riêng sau.

Tất cả endpoint đi qua Gateway (`http://localhost:5500`), trừ khi ghi chú khác.

---

## 1. Chuẩn bị môi trường

```bash
docker compose up -d --build
docker compose ps   # tất cả service phải "healthy"
```

Cổng đã map ra host:

| Service | Port | Swagger | Hangfire |
| --- | --- | --- | --- |
| Gateway | 5500 | `/swagger` | - |
| Identity | 5001 | `/swagger` | - |
| Catalog | 5002 | `/swagger` | `/hangfire` |
| Submission | 5003 | `/swagger` | `/hangfire` |
| Grading | 5004 | `/swagger` | `/hangfire` |
| Notification | 5005 | `/swagger` | - |
| user-web | 5173 | - | - |
| admin-web | 5174 | - | - |

Mở sẵn 2 tab Hangfire (`localhost:5003/hangfire`, `localhost:5004/hangfire`) trong lúc demo
để chỉ trực quan job `ExtractionJob` / `AiGradingJob` chạy async — rất ấn tượng khi demo.

### Tài khoản test có sẵn (seed sẵn, không cần đăng ký)

Được seed tự động khi `Seed:TestAccounts=true` (đã bật trong `docker-compose.yml`), mật khẩu
chung là `Test@12345`:

| Email | Role |
| --- | --- |
| `testlecturer1@fpt.edu.vn` | Lecturer |
| `teststudent1@fpt.edu.vn` | Student |
| `testadmin1@fpt.edu.vn` | Admin |

(còn `teststudent2..10`, `testlecturer2..4` nếu cần nhiều actor cùng lúc)

### ⚠️ Gotcha cần nhớ

- **Double prefix qua Gateway**: route nội bộ của Submission và Notification service tự đặt
  tên group trùng với prefix Gateway (`/submissions`, `/notifications`), nên khi gọi *qua
  Gateway* phải gọi `/submissions/submissions/...` và `/notifications/notifications`
  (Catalog/Grading không bị vấn đề này: group là `/subjects`, `/assignments`, `/rubrics`,
  `/grades` — khác tên prefix `/catalog`, `/grading`). Ví dụ đúng:
  `POST http://localhost:5500/submissions/submissions/upload`.
- **Google login yêu cầu email `.edu`**; `/auth/register` + `/auth/login` (email/password)
  thì không giới hạn domain — dùng cách này cho test.
- **Subject mới tạo mặc định `RegistrationStatus = Closed`** — không ảnh hưởng việc nộp bài
  qua API (upload không kiểm tra enrollment), nhưng **có** ảnh hưởng tới việc lecturer xem bài
  (xem gotcha tiếp theo).
- **Lecturer chỉ thấy submission của sinh viên trong lớp mình dạy** — `GET
  /submissions/submissions` (list), `GET /submissions/submissions/{id}`, và `POST
  /submissions/submissions/{id}/retry` scope theo enrollment: lecturer phải có ít nhất 1
  `Class` (subject-scoped, `lecturerId` = chính họ) cho subject của assignment đó, và sinh
  viên phải **enroll vào lớp đó** — nếu không, lecturer nhận về danh sách rỗng / `403
  Forbidden`, kể cả khi sinh viên đã nộp bài thành công. Một lecturer có thể dạy nhiều class
  của cùng 1 subject — được gộp chung (union), không cần chọn đúng 1 mã lớp cụ thể. `admin`
  không bị giới hạn này (luôn thấy tất cả). Xem mục 2.11 để test riêng flow này.
- AI grading gọi LLM thật (OpenRouter/OpenCode) — có thể mất 10–60s, đừng poll quá gấp.

---

## 2. Happy Case — API/Postman (nhanh, dùng để verify backend)

Dùng `curl` + `jq` (Git Bash). Có thể copy nguyên khối chạy tuần tự.

```bash
BASE=http://localhost:5500

# --- 2.1 Login (dùng tài khoản seed sẵn) ---
LECTURER_LOGIN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testlecturer1@fpt.edu.vn","password":"Test@12345"}')
LECTURER_TOKEN=$(echo $LECTURER_LOGIN | jq -r .token)
LECTURER_ID=$(echo $LECTURER_LOGIN | jq -r .userId)

STUDENT_LOGIN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"teststudent1@fpt.edu.vn","password":"Test@12345"}')
STUDENT_TOKEN=$(echo $STUDENT_LOGIN | jq -r .token)
STUDENT_ID=$(echo $STUDENT_LOGIN | jq -r .userId)

ADMIN_TOKEN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testadmin1@fpt.edu.vn","password":"Test@12345"}' | jq -r .token)

echo "lecturer id: $LECTURER_ID"
echo "student id: $STUDENT_ID"
```

**Kỳ vọng**: cả 2 request trả `200 OK`, `token` không rỗng, `role` đúng (`lecturer`/`student`).

```bash
# --- 2.2 Lecturer: tạo Subject ---
SUBJECT=$(curl -s -X POST $BASE/catalog/subjects \
  -H "Authorization: Bearer $LECTURER_TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"SWD392","name":"Software Architecture and Design"}')
SUBJECT_ID=$(echo $SUBJECT | jq -r .id)
```

**Kỳ vọng**: `201 Created`, `registrationStatus: "Closed"`, có `id`.

```bash
# --- 2.3 Lecturer: tạo Assignment ---
ASSIGNMENT=$(curl -s -X POST $BASE/catalog/assignments \
  -H "Authorization: Bearer $LECTURER_TOKEN" -H "Content-Type: application/json" \
  -d "{\"subjectId\":\"$SUBJECT_ID\",\"title\":\"Assignment 1 - Architecture Report\",\"description\":\"Nộp báo cáo + diagram\",\"maxAttempts\":3}")
ASSIGNMENT_ID=$(echo $ASSIGNMENT | jq -r .id)
```

**Kỳ vọng**: `201 Created`, `maxAttempts: 3`.

```bash
# --- 2.4 Lecturer: upload Rubric (.docx) ---
# Xem format chuẩn tại docs/rubric-docx-format.md — cần file .docx thật, không phải placeholder
RUBRIC=$(curl -s -X POST $BASE/catalog/rubrics/upload \
  -H "Authorization: Bearer $LECTURER_TOKEN" \
  -F "SubjectId=$SUBJECT_ID" \
  -F "AssignmentId=$ASSIGNMENT_ID" \
  -F "Name=Rubric v1" \
  -F "Scope=Lecturer" \
  -F "File=@/path/to/rubric.docx;type=application/vnd.openxmlformats-officedocument.wordprocessingml.document")
RUBRIC_ID=$(echo $RUBRIC | jq -r .id)
echo "status: $(echo $RUBRIC | jq -r .status)"   # "Parsing"
```

**Kỳ vọng**: `201 Created`, `status: "Parsing"`. Job `RubricParsingJob` chạy async (xem
`localhost:5002/hangfire`) → status chuyển `Draft`. Poll cho tới khi Draft:

```bash
until [ "$(curl -s $BASE/catalog/rubrics?assignmentId=$ASSIGNMENT_ID -H "Authorization: Bearer $LECTURER_TOKEN" | jq -r '.[0].status')" = "Draft" ]; do sleep 2; done
```

```bash
# --- 2.5 Lecturer: confirm Rubric ---
curl -s -X POST $BASE/catalog/rubrics/$RUBRIC_ID/confirm \
  -H "Authorization: Bearer $LECTURER_TOKEN" | jq -r .status   # "Confirmed"
```

**Kỳ vọng**: `200 OK`, `status: "Confirmed"` (bắt buộc — Grading job chỉ chạy tốt khi rubric
đã confirmed và có criteria).

```bash
# --- 2.6 Student: upload Submission (.docx + .drawio) ---
# Chú ý double-prefix /submissions/submissions/upload — xem mục Gotcha ở trên
SUBMISSION=$(curl -s -X POST $BASE/submissions/submissions/upload \
  -H "Authorization: Bearer $STUDENT_TOKEN" \
  -F "AssignmentId=$ASSIGNMENT_ID" \
  -F "ReportFile=@/path/to/report.docx;type=application/vnd.openxmlformats-officedocument.wordprocessingml.document" \
  -F "DiagramFile=@/path/to/diagram.drawio;type=application/octet-stream")
SUBMISSION_ID=$(echo $SUBMISSION | jq -r .id)
echo "state: $(echo $SUBMISSION | jq -r .state)"   # "Uploading" hoặc "Uploaded"
```

**Kỳ vọng**: `201 Created`, `attemptNumber: 1`.

```bash
# --- 2.7 Poll trạng thái Submission cho tới khi extraction xong ---
until [ "$(curl -s $BASE/submissions/submissions/$SUBMISSION_ID -H "Authorization: Bearer $STUDENT_TOKEN" | jq -r .state)" = "Extracted" ]; do
  sleep 2
done
echo "extracted OK"
```

**Kỳ vọng**: `state` đi qua `Uploaded → Extracting → Extracted` (xem `localhost:5003/hangfire`
job `ExtractionJob`), có `artifacts[]` không rỗng.

```bash
# --- 2.8 Poll AI Grading run (Grading service tự enqueue khi Extracted) ---
until [ "$(curl -s $BASE/grading/grades/$SUBMISSION_ID/runs -H "Authorization: Bearer $LECTURER_TOKEN" | jq -r '.[0].status')" = "Completed" ]; do
  sleep 5
done
RUN_ID=$(curl -s $BASE/grading/grades/$SUBMISSION_ID/runs -H "Authorization: Bearer $LECTURER_TOKEN" | jq -r '.[0].id')
curl -s $BASE/grading/grades/$SUBMISSION_ID/runs -H "Authorization: Bearer $LECTURER_TOKEN" | jq '.[0].scores'
```

**Kỳ vọng**: `status: "Completed"`, `scores[]` có 1 phần tử mỗi criterion trong rubric, mỗi
phần tử có `suggestedScore` hợp lệ (0 ≤ score ≤ maxScore của criterion đó). Job này gọi LLM
thật nên có thể mất 10–60s — theo dõi tại `localhost:5004/hangfire`.

```bash
# --- 2.9 Lecturer: publish điểm cuối cùng ---
curl -s -X POST $BASE/grading/grades/$SUBMISSION_ID/publish \
  -H "Authorization: Bearer $LECTURER_TOKEN" -H "Content-Type: application/json" \
  -d "{\"gradingRunId\":\"$RUN_ID\",\"finalScore\":8.5,\"notes\":\"Good job\"}" | jq .
```

**Kỳ vọng**: `201 Created`, trả về `FinalGrade` với `finalScore: 8.5`. Nếu gọi lần 2 phải trả
`409 Conflict` (không phải happy case, chỉ lưu ý).

```bash
# --- 2.10 Student: kiểm tra Notification vừa được publish ---
curl -s "$BASE/notifications/notifications?userId=$STUDENT_ID" \
  -H "Authorization: Bearer $STUDENT_TOKEN" | jq '.[0]'
```

**Kỳ vọng**: `200 OK`, phần tử đầu tiên (mới nhất) có nội dung liên quan "grade published",
`isRead: false`.

```bash
# --- 2.11 Lecturer: xem submissions của học sinh TRONG LỚP MÌNH DẠY ---
# Bước bắt buộc: mở registration, tạo Class (subject-scoped, lecturerId = chính lecturer),
# rồi cho student enroll — enrollment chỉ chấp nhận khi subject đang "Open"
curl -s -X PATCH $BASE/catalog/subjects/$SUBJECT_ID/registration \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"status":"Open"}' > /dev/null

CLASS=$(curl -s -X POST $BASE/catalog/classes/subject-scoped \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"SE1234\",\"lecturerId\":\"$LECTURER_ID\",\"subjectId\":\"$SUBJECT_ID\"}")
CLASS_ID=$(echo $CLASS | jq -r .id)

curl -s -X PUT "$BASE/catalog/enrollments/me/$SUBJECT_ID" \
  -H "Authorization: Bearer $STUDENT_TOKEN" -H "Content-Type: application/json" \
  -d "{\"classId\":\"$CLASS_ID\"}"

# Giờ lecturer thấy submission của teststudent1
curl -s "$BASE/submissions/submissions?assignmentId=$ASSIGNMENT_ID" \
  -H "Authorization: Bearer $LECTURER_TOKEN" | jq 'length'   # >= 1

# Lecturer KHÁC (không dạy lớp nào của subject này) phải thấy rỗng
LECTURER2_TOKEN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testlecturer2@fpt.edu.vn","password":"Test@12345"}' | jq -r .token)
curl -s "$BASE/submissions/submissions?assignmentId=$ASSIGNMENT_ID" \
  -H "Authorization: Bearer $LECTURER2_TOKEN" | jq 'length'   # phải là 0
```

`ADMIN_TOKEN`/`LECTURER_ID` lấy tương tự bước 2.1 (login `testadmin1@fpt.edu.vn`, và
`LECTURER_ID` là `userId` trả về khi lecturer login). **Kỳ vọng**: `testlecturer1` (dạy lớp
SE1234) thấy đúng 1 submission; `testlecturer2` (không dạy lớp nào ở subject này) thấy mảng
rỗng dù cùng gọi 1 endpoint với cùng `assignmentId` — xác nhận scope theo lớp hoạt động đúng.
`GET /submissions/submissions/{id}` và `POST /submissions/submissions/{id}/retry` cũng áp
dụng cùng quy tắc.

Nếu tất cả bước trên trả đúng status code + field như mô tả → **happy path backend PASS**.

---

## 3. Happy Case — UI Demo Script (dùng khi trình bày trực tiếp)

Lưu ý quan trọng: `user-web` (5173) **chỉ có màn hình cho student** (login, profile, submit
bài, xem kết quả). Toàn bộ nghiệp vụ Subjects/Assignments/Rubrics/Classes/Roster/Grades/
**Review (chấm & publish điểm)** nằm ở `admin-web` (5174) — lecturer đăng nhập chung app này
với admin, chỉ riêng menu **Classes** là ẩn/chặn nếu không phải role `admin`.

### 3.1 Admin: tạo Subject → Class (gắn lecturer) (`admin-web`, http://localhost:5174)

1. Đăng nhập bằng `testadmin1@fpt.edu.vn` / `Test@12345`.
2. Vào **Subjects** → tạo subject mới (code + tên) → mở **Registration** → set `Open`.
3. Vào **Classes** → tạo class mới, gán `subjectId` = subject vừa tạo và `lecturerId` =
   `testlecturer1`. **Bước này bắt buộc** — nếu bỏ qua, lecturer sẽ không thấy submission nào
   ở mục Review (3.4) dù sinh viên đã nộp bài thành công (xem gotcha ở mục 1).

**Kỳ vọng nhìn thấy**: subject xuất hiện trong danh sách; class hiển thị đúng tên lecturer +
subject.

### 3.2 Lecturer: tạo Assignment + Rubric (`admin-web`, http://localhost:5174)

1. Đăng nhập bằng `testlecturer1@fpt.edu.vn` / `Test@12345`.
2. Vào **Assignments** → chọn subject vừa tạo → tạo assignment (title, due date, max attempts).
3. Vào **Rubric** → **Upload Rubric** → chọn file `.docx` theo đúng format ở
   `docs/rubric-docx-format.md` → chờ status chuyển `Draft` (UI tự poll hoặc reload).
4. Xem **criteria** được parse ra đúng như trong file rubric → bấm **Confirm Rubric**.

**Kỳ vọng nhìn thấy**: assignment xuất hiện trong danh sách ngay lập tức; rubric status đổi
`Parsing → Draft → Confirmed`; criteria hiển thị đúng tên/điểm tối đa.

### 3.3 Student flow (`user-web`, http://localhost:5173)

1. Đăng nhập bằng `teststudent1@fpt.edu.vn` / `Test@12345`.
2. **Subjects** → tìm subject `Open` → chọn đúng class do `testlecturer1` dạy (tạo ở 3.1) →
   đăng ký. **Bước này cũng bắt buộc** cùng lý do ở 3.1.
3. Vào assignment vừa tạo → **Upload Submission** → kéo-thả `report.docx` +
   `diagram.drawio` → Submit.
4. Theo dõi màn **Submission Status** — trạng thái tự cập nhật
   `Uploading → Uploaded → Extracting → Extracted → Grading → Graded` (poll hoặc realtime).
5. Sau khi có điểm publish, vào **My Grades / Notifications** xem điểm + noti "Grade
   Published".

**Kỳ vọng nhìn thấy**: file upload thành công không lỗi extension; trạng thái tự chuyển
không cần F5 thủ công (hoặc F5 là thấy ngay); artifacts (report text, diagram) hiển thị sau
khi Extracted; điểm cuối cùng khớp với số lecturer đã publish.

### 3.4 Lecturer — review & publish (`admin-web`, http://localhost:5174, menu **Review**)

1. Quay lại tài khoản `testlecturer1` → menu **Review** → chọn Subject → chọn Assignment.
2. Bảng danh sách submissions hiện ra — **chỉ gồm sinh viên thuộc lớp `testlecturer1` dạy**
   (đúng tính năng vừa thêm). Thử đăng nhập bằng `testlecturer2@fpt.edu.vn` (không dạy lớp
   nào ở subject này) và mở lại menu Review với cùng assignment → bảng phải **rỗng**, xác
   nhận scope hoạt động đúng ở tầng UI chứ không chỉ API.
3. Quay lại `testlecturer1`, bấm vào submission của `teststudent1` → xem **AI Grading Run**
   (per-criterion suggested score do AI chấm).
4. Sửa/giữ nguyên điểm, nhập `finalScore` + `notes` → **Publish**.

**Kỳ vọng**: `testlecturer1` thấy đúng 1 dòng; `testlecturer2` thấy bảng trống; publish
thành công → sinh viên nhận notification ngay (xem lại 3.3 bước 5).

### 3.5 Admin flow (`admin-web`, http://localhost:5174)

1. Đăng nhập bằng `testadmin1@fpt.edu.vn` / `Test@12345`.
2. Vào **Review** — xác nhận admin (khác lecturer) thấy **tất cả** submissions của assignment
   bất kể lớp/lecturer nào (admin không bị scope theo lớp).
3. Vào **Audit Events** — xác nhận thấy đủ các event vừa phát sinh ở trên:
   `UserRegistered`(nếu vừa đăng ký mới) → `SubmissionUploaded`-liên-quan →
   `AiGradingCompleted` → `GradePublished`.
4. (tuỳ chọn) **Roster bulk import** — upload file `.xlsx` mẫu để gán `studentCode`/lớp
   hàng loạt.

**Kỳ vọng**: audit log hiển thị đúng thứ tự thời gian, không thiếu event nào trong chuỗi.

---

## 4. Checklist tổng hợp (chạy trước demo 1 lần)

- [ ] `docker compose ps` — toàn bộ container `healthy`
- [ ] Login lecturer/student/admin bằng tài khoản seed — nhận JWT hợp lệ
- [ ] Tạo Subject → Class (gắn lecturer) → Assignment → Upload+Confirm Rubric — không lỗi
- [ ] Student enroll vào đúng class của lecturer đó, rồi mới upload Submission — state cuối
      cùng là `Extracted` (không stuck ở `Extracting`)
- [ ] Hangfire (`:5003/hangfire`, `:5004/hangfire`) không có job `Failed`
- [ ] AI Grading run có `status: Completed` với scores hợp lệ
- [ ] Lecturer dạy lớp đó mở Review → thấy submission; lecturer KHÁC mở Review cùng
      assignment → thấy bảng rỗng (scope theo lớp hoạt động đúng)
- [ ] Publish Grade thành công, student thấy `Notification` mới
- [ ] Admin Audit Events log đủ 4-5 event trong chuỗi trên

Nếu một bước fail, dừng lại debug trước khi demo — đừng để lộ lỗi async job giữa buổi trình
bày (đặc biệt `AiGradingJob`, phụ thuộc OpenRouter/OpenCode API còn sống).
