# upgrades/

Script nâng cấp MỘT LẦN cho database đang có dữ liệu cần giữ. Tên file: `yyyy-MM-dd_TenThayDoi.sql`, phải idempotent (`IF COL_LENGTH(...) IS NULL`, `IF OBJECT_ID(...) IS NULL`).
Luôn sửa đồng thời `01_Schema.sql` / `05_DemoSeed.sql` để database dựng mới cũng đúng. Hiện chưa có script nào.
