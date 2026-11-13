# S?-?? · Tên task (UC-xx, UC-yy)

| Người làm | Ưu tiên | Điểm | Tuần | Phụ thuộc | Nhánh |
| --- | --- | --- | --- | --- | --- |
| Viên/Huy | Must/Should/Could | 1/2/3/5/8 | 3.1 | — | `feature/s?-??-ten` |

## Mục tiêu

Một đoạn: người dùng nào làm được gì sau task này.

## Tiêu chí hoàn thành

- [ ] Hành vi 1 (đo được, thử được bằng tài khoản demo nào)
- [ ] Role không được phép bị chặn (403/404)
- [ ] Seed demo + check trong `scripts/smoke-test.mjs`
- [ ] Definition of Done chung ([../SPRINTS.md](../SPRINTS.md))

## 1. Database

DDL dán vào vùng của mình trong `Database/01_Schema.sql`. Ràng buộc nghiệp vụ quan trọng phải có CHECK/UNIQUE.

## 2. Backend

| Layer | File | Nội dung |
| --- | --- | --- |
| BusinessObjects | | |
| DAO / Repository | | |
| DTOs | | |
| Service | | Quy tắc nghiệp vụ, kiểm quyền |
| Controller | | |

## 3. API

| Method | Đường dẫn | Policy | Ghi chú |
| --- | --- | --- | --- |

## 4. Frontend

Trang, component, route/menu trong `src/modules/<module>.tsx`.

## 5. Dữ liệu demo

## 6. Kiểm thử

Các bước thử tay (tài khoản nào, kết quả mong đợi) + `check(...)` thêm vào smoke test.

## Tham khảo code có sẵn

## Bẫy cần tránh
