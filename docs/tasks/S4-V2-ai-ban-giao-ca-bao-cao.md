# S4-V2 · AI tóm tắt bàn giao ca (AI-04) + hỏi đáp báo cáo (AI-12, tùy chọn)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên | Should (AI-04) / Could (AI-12) | 5 | 4 | S3-V4 (bàn giao), S3-V3 (báo cáo), S4-V1 PR 1 | `feature/s4-v2-ai-handover` |

## Mục tiêu

Nút **"Viết tóm tắt bằng AI"** trong màn bàn giao ca: AI đọc dữ liệu gợi ý của ca (việc chưa xong, thuốc từ chối, sinh hiệu bất thường, sự cố) và viết bản bàn giao ngắn gọn theo từng cư dân, việc quan trọng lên đầu; người viết sửa lại rồi mới lưu. Nếu còn thời gian: Admin/Kế toán hỏi bằng tiếng Việt ("Tháng 10 thu được bao nhiêu?") và AI trả lời bằng số liệu từ báo cáo có sẵn.

## Tiêu chí hoàn thành

- [x] `POST /api/handovers/ai-draft {shiftId, date}` trả đoạn văn bản (≤ 12 dòng, gạch đầu dòng theo cư dân, mục cần ưu tiên ghi "[ƯU TIÊN]").
- [x] Chỉ dùng dữ liệu của `HandoverService.GetDraftAsync` (S3-V4); không có dữ liệu thì trả "Ca không có vấn đề cần lưu ý".
- [x] Văn bản AI được điền vào ô tóm tắt để **người dùng sửa**; lưu với `AiGenerated = 1`.
- [x] Dữ liệu gửi cho AI dùng **mã cư dân + tên gọi**, không gửi SĐT, CCCD, địa chỉ.
- [x] (Could, AI-12) Khung hỏi đáp ở trang Báo cáo; AI gọi tool `revenue(year)`, `occupancy(year)`, `medication_adherence(from,to)`, `incident_summary(from,to)` (gọi `IReportService` với `CurrentUser` để giữ quyền) và trả lời có số liệu; **không** sinh SQL. *(trừ `incident_summary` — chờ bảng `Incidents` của S3-H1)*

### Kết quả triển khai (13/11/2026)

- Kiểm thử: smoke thêm 10 kiểm tra với provider Fake (tổng 213/213 PASS), unit thêm `HandoverAiTests` (87/87). **Chưa chạy với Gemini thật**; nút "Viết tóm tắt bằng AI" và khung "Hỏi đáp số liệu" **chưa xem trên trình duyệt**.
- Khác đặc tả:
  - Provider Fake (hoặc khi AI lỗi/hết thời gian) dùng **bản tóm tắt theo quy tắc**: gộp theo cư dân, thuốc/sinh hiệu/sự cố lên đầu với "[ƯU TIÊN]", tối đa 12 dòng. Khi AI lỗi, nháp có `aiGenerated = false` + ghi chú, FE lưu đúng cờ đó.
  - Thêm `AiChatRequest.Temperature` (tùy chọn) để gọi tóm tắt với 0.2. Với model `gemini-3*` không gửi temperature (để mặc định của model, như StreetBiz).
  - `IReportService` không nhận `CurrentUser`, nên mỗi tool tự kiểm role theo policy của báo cáo tương ứng (doanh thu: FinanceStaff; công suất: BillingStaff; tuân thủ thuốc: Prescribers). Kế toán hỏi tuân thủ thuốc → model nhận `{"error": "không có quyền"}`.
  - Endpoint hỏi đáp: `POST /api/ai/staff/report-chat` + `GET .../history` (FinanceStaff, rate limit `Chat` theo tài khoản). Provider Fake nhận các từ khóa "doanh thu", "công suất", "tuân thủ/uống thuốc".
  - `incident_summary` thêm khi S3-H1 (bảng `Incidents`) merge — cùng chỗ với UC-125 của S3-V3.

## 1. Database

Không bảng mới (dùng `ShiftHandovers.AiGenerated`, `HighlightsJson` của S3-V4; hội thoại AI-12 lưu `AiConversations` kênh `Staff`).

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| Service | `Services/Ai/HandoverAiService.cs` | Lấy draft data → dựng prompt (system: "Bạn là điều dưỡng trưởng viết bàn giao ca…", user: JSON gọn các mục) → `IAiChatProvider.CompleteAsync` **không tool**, `temperature 0.2` → trả text |
| Service (AI-12) | `Services/Ai/ReportAssistantService.cs` + `Services/Ai/Tools/ReportTools.cs` | 4 tool gọi `IReportService`; dùng `IAiChatEngine` kênh `Staff` |
| Controller | `HandoversController` (thêm action), `AiController` (thêm `staff/report-chat`) | |

## 3. API

| Method | Đường dẫn | Policy |
| --- | --- | --- |
| POST | `/api/handovers/ai-draft` | ClinicalStaff |
| POST | `/api/ai/staff/report-chat` `{message}` | FinanceStaff + Admin (Could) |

## 4. Frontend

- Tab "Bàn giao ca" (`pages/Operations/Shifts.tsx`): nút "Viết tóm tắt bằng AI" cạnh "Lấy gợi ý"; hiện `Alert` "Nội dung do AI soạn — kiểm tra lại trước khi lưu".
- (Could) `pages/Operations/Reports.tsx`: `ChatWidget` với `send = (text) => aiApi.reportChat(text)`.

## 5. Kiểm thử

1. Provider Fake trả văn bản cố định → nút điền được vào ô, lưu có `AiGenerated = 1`.
2. Gemini: ca sáng hôm nay (seed có việc chưa xong + thuốc bị từ chối) → tóm tắt nhắc đúng 2 mục, mục thuốc bị từ chối có "[ƯU TIÊN]".
3. Kiểm log/payload gửi đi không chứa SĐT.

## Bẫy cần tránh

- Không tự lưu văn bản AI; người dùng phải bấm Lưu sau khi đọc.
- AI-12: tool phải giữ phân quyền (Accountant không gọi được báo cáo thuốc).
