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
- **Subject mới tạo mặc định `RegistrationStatus = Closed`** — không ảnh hưởng flow nộp bài
  qua API (Submission service không kiểm tra enrollment), chỉ ảnh hưởng nếu bạn test màn
  "sinh viên tự đăng ký môn học" trên UI.
- AI grading gọi LLM thật (OpenRouter/OpenCode) — có thể mất 10–60s, đừng poll quá gấp.

---

## 2. Happy Case — API/Postman (nhanh, dùng để verify backend)

Dùng `curl` + `jq` (Git Bash). Có thể copy nguyên khối chạy tuần tự.

```bash
BASE=http://localhost:5500

# --- 2.1 Login (dùng tài khoản seed sẵn) ---
LECTURER_TOKEN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testlecturer1@fpt.edu.vn","password":"Test@12345"}' | jq -r .token)

STUDENT_LOGIN=$(curl -s -X POST $BASE/identity/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"teststudent1@fpt.edu.vn","password":"Test@12345"}')
STUDENT_TOKEN=$(echo $STUDENT_LOGIN | jq -r .token)
STUDENT_ID=$(echo $STUDENT_LOGIN | jq -r .userId)

echo "lecturer token: ${LECTURER_TOKEN:0:20}..."
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

Nếu tất cả bước trên trả đúng status code + field như mô tả → **happy path backend PASS**.

---

## 3. Happy Case — UI Demo Script (dùng khi trình bày trực tiếp)

### 3.1 Lecturer flow (`user-web`, http://localhost:5173)

1. Đăng nhập bằng `testlecturer1@fpt.edu.vn` / `Test@12345`.
2. Vào **Subjects** → tạo subject mới (code + tên) → mở **Registration** → set `Open`
   (để sinh viên có thể thấy/đăng ký ở bước 3.2, nếu demo có phần enrollment).
3. Vào subject vừa tạo → **Assignments** → tạo assignment (title, due date, max attempts).
4. Mở assignment → **Upload Rubric** → chọn file `.docx` theo đúng format ở
   `docs/rubric-docx-format.md` → chờ status chuyển `Draft` (UI tự poll hoặc reload).
5. Xem **criteria** được parse ra đúng như trong file rubric → bấm **Confirm Rubric**.

**Kỳ vọng nhìn thấy**: subject/assignment xuất hiện trong danh sách ngay lập tức; rubric
status đổi `Parsing → Draft → Confirmed`; criteria hiển thị đúng tên/điểm tối đa.

### 3.2 Student flow (`user-web`, http://localhost:5173)

1. Đăng nhập bằng `teststudent1@fpt.edu.vn` / `Test@12345`.
2. (Nếu bật enrollment) **Subjects** → tìm subject `Open` → chọn lớp → đăng ký.
3. Vào assignment vừa tạo → **Upload Submission** → kéo-thả `report.docx` +
   `diagram.drawio` → Submit.
4. Theo dõi màn **Submission Status** — trạng thái tự cập nhật
   `Uploading → Uploaded → Extracting → Extracted → Grading → Graded` (poll hoặc realtime).
5. Sau khi có điểm publish, vào **My Grades / Notifications** xem điểm + noti "Grade
   Published".

**Kỳ vọng nhìn thấy**: file upload thành công không lỗi extension; trạng thái tự chuyển
không cần F5 thủ công (hoặc F5 là thấy ngay); artifacts (report text, diagram) hiển thị sau
khi Extracted; điểm cuối cùng khớp với số lecturer đã publish.

### 3.3 Lecturer — review & publish (`user-web`)

1. Quay lại tài khoản lecturer → mở submission của sinh viên vừa nộp.
2. Xem **AI Grading Run** — per-criterion suggested score do AI chấm.
3. Sửa/giữ nguyên điểm, nhập `finalScore` + `notes` → **Publish Grade**.

**Kỳ vọng**: publish thành công → sinh viên nhận notification ngay (xem lại 3.2 bước 5).

### 3.4 Admin flow (`admin-web`, http://localhost:5174)

1. Đăng nhập bằng `testadmin1@fpt.edu.vn` / `Test@12345`.
2. Vào **Classes** → tạo class gắn với subject + lecturer (nếu demo enrollment).
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
- [ ] Tạo Subject → Assignment → Upload+Confirm Rubric — không lỗi
- [ ] Student upload Submission — state cuối cùng là `Extracted` (không stuck ở `Extracting`)
- [ ] Hangfire (`:5003/hangfire`, `:5004/hangfire`) không có job `Failed`
- [ ] AI Grading run có `status: Completed` với scores hợp lệ
- [ ] Publish Grade thành công, student thấy `Notification` mới
- [ ] Admin Audit Events log đủ 4-5 event trong chuỗi trên

Nếu một bước fail, dừng lại debug trước khi demo — đừng để lộ lỗi async job giữa buổi trình
bày (đặc biệt `AiGradingJob`, phụ thuộc OpenRouter/OpenCode API còn sống).
