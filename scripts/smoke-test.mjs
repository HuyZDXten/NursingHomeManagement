// Kiểm thử end-to-end (auth, phân quyền theo cư dân, chăm sóc, thuốc, hóa đơn, PayOS + các module Sprint 3+).
// Mỗi lần chạy cần dữ liệu sạch vì test thay đổi trạng thái.
//
// Cách 1 — dùng DB/API dev thường ngày (sẽ XÓA dữ liệu dev):
//   .\Database\setup-local-db.ps1 -Recreate
//   node scripts/payos-fake/server.mjs                          (khởi động lại để xóa link cũ trong bộ nhớ)
//   cd BE; $env:RateLimit__CredentialsPerMinute=200; dotnet run --project NursingHome.API --launch-profile http
//   node scripts/smoke-test.mjs
//
// Cách 2 — CÔ LẬP, không đụng DB/API đang chạy (DB NursingHomeDB_Test, API cổng 5081, PayOS giả cổng 5198):
//   .\scripts\run-smoke-isolated.ps1
//
// Thêm use case mới: thêm vài dòng check(...) vào cuối file.
const API = process.env.API_BASE ?? 'http://localhost:5080';
const FAKE = process.env.PAYOS_FAKE_BASE ?? 'http://localhost:5199';
let failures = 0;
const check = (name, cond, extra = '') => { console.log(`${cond ? 'PASS' : 'FAIL'}  ${name} ${extra}`); if (!cond) failures++; };

async function call(method, path, token, body) {
  const res = await fetch(API + path, {
    method,
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data; try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  return { status: res.status, data };
}
const login = async (email) => (await call('POST', '/api/auth/login', null, { email, password: 'Demo@123456' })).data;

// ---- Sprint 0: auth ----
const bad = await call('POST', '/api/auth/login', null, { email: 'admin@nursinghome.local', password: 'sai' });
check('login sai mật khẩu -> 401', bad.status === 401, bad.data?.message);
const admin = await login('admin@nursinghome.local');
check('admin login', !!admin?.accessToken, admin?.role);
const me = await call('GET', '/api/auth/me', admin.accessToken);
check('GET /me', me.status === 200 && me.data.role === 'Admin');
const refreshed = await call('POST', '/api/auth/refresh', null, { refreshToken: admin.refreshToken });
check('refresh token xoay vòng', refreshed.status === 200);
const reuse = await call('POST', '/api/auth/refresh', null, { refreshToken: admin.refreshToken });
check('dùng lại refresh token cũ -> 401', reuse.status === 401);
const adminToken = (await login('admin@nursinghome.local')).accessToken;
const accounts = await call('GET', '/odata/Accounts?$count=true&$orderby=id', adminToken);
check('OData Accounts có @odata.count', accounts.data?.['@odata.count'] === 9, `count=${accounts.data?.['@odata.count']}`);

// ---- Sprint 1: residents / rooms / care / meds / billing ----
const residents = await call('GET', "/odata/Residents?$filter=status eq 'Active'&$count=true&$top=2&$orderby=code", adminToken);
check('OData Residents lọc + phân trang', residents.status === 200 && residents.data.value.length === 2 && residents.data['@odata.count'] === 4,
  JSON.stringify(residents.data?.value?.map((r) => `${r.code}:${r.bedLabel}`)));

const caregiver = await login('caregiver@nursinghome.local');
const cgResidents = await call('GET', '/odata/Residents', caregiver.accessToken);
check('Caregiver chỉ thấy cư dân được giao', cgResidents.data.value.map((r) => r.code).sort().join() === 'NH-0001,NH-0002');
const forbidden = await call('GET', '/api/residents/3', caregiver.accessToken);
check('Caregiver xem cư dân không được giao -> 403', forbidden.status === 403, forbidden.data?.message);

const tasks = await call('GET', '/odata/CareTasks?mine=true', caregiver.accessToken);
const pending = tasks.data.value.find((t) => t.status === 'Pending');
check('Caregiver thấy việc của mình', tasks.status === 200 && !!pending, `${tasks.data.value.length} việc`);
const upd = await call('POST', `/api/care-tasks/${pending.id}/updates`, caregiver.accessToken,
  { status: 'Completed', result: 'Đã đo: HA 130/80', shareWithFamily: true, rowVersion: pending.rowVersion });
check('Cập nhật việc + rowversion', upd.status === 200 && upd.data.status === 'Completed');
const stale = await call('POST', `/api/care-tasks/${pending.id}/updates`, caregiver.accessToken,
  { status: 'InProgress', result: 'ghi đè', shareWithFamily: false, rowVersion: pending.rowVersion });
check('Ghi đè bằng rowversion cũ bị chặn', stale.status === 400 || stale.status === 409, `${stale.status} ${stale.data?.message}`);

const cgVital = await call('POST', '/api/vitals', caregiver.accessToken,
  { residentId: 1, systolic: 120, diastolic: 80, heartRate: 80, temperature: 36.8, spO2: 97, shareWithFamily: true });
check('Caregiver hết việc với NH-0001 thì hết quyền ghi sinh hiệu -> 403', cgVital.status === 403);
const nurse = await login('nurse@nursinghome.local');
const vital = await call('POST', '/api/vitals', nurse.accessToken,
  { residentId: 1, systolic: 170, diastolic: 95, heartRate: 80, temperature: 36.8, spO2: 97, shareWithFamily: true });
check('Nurse ghi sinh hiệu (vượt ngưỡng -> cảnh báo)', vital.status === 200, `${vital.status} ${vital.data?.recordedByName ?? vital.data?.message}`);
const nurseNotes = await call('GET', '/api/notifications', nurse.accessToken);
check('Nurse nhận cảnh báo sinh hiệu', nurseNotes.data.items.some((n) => n.type === 'Health' && n.title.includes('Nguyễn Văn An')));

const today = await call('GET', '/api/medications/today', caregiver.accessToken);
check('Bảng phát thuốc hôm nay', today.status === 200, `${today.data.length} liều`);
const badBed = await call('POST', '/api/bed-assignments', (await login('receptionist@nursinghome.local')).accessToken, { residentId: 1, bedId: 2 });
check('Phân vào giường đang có người -> 400', badBed.status === 400, badBed.data?.message);
const doctor = await login('doctor@nursinghome.local');
const sched = await call('POST', '/api/medication-schedules', doctor.accessToken,
  { residentId: 4, medicationName: 'Thuốc mô phỏng C', dose: '1 viên', route: 'Uống', startDate: '2026-11-01', scheduledTime: '07:00:00', approvalReference: 'CĐ-TEST' });
check('Doctor tạo lịch thuốc', sched.status === 200, sched.data?.enteredByName);
const cgSched = await call('POST', '/api/medication-schedules', caregiver.accessToken,
  { residentId: 4, medicationName: 'X', dose: '1', route: 'Uống', startDate: '2026-11-01', scheduledTime: '07:00:00', approvalReference: 'X' });
check('Caregiver không được kê lịch thuốc -> 403', cgSched.status === 403);

const accountant = await login('accountant@nursinghome.local');
const over = await call('POST', '/api/invoices/1/payments', accountant.accessToken, { amount: 99000000, method: 'Cash', reference: 'PT-TEST-OVER' });
check('Thu vượt công nợ -> 400', over.status === 400, over.data?.message);

// ---- Sprint 2: PayOS ----
const family2 = await login('family2@nursinghome.local');
const fam = await call('GET', '/api/family/residents', family2.accessToken);
check('Family thấy người thân', fam.data?.length === 1 && fam.data[0].code === 'NH-0002');
const famOther = await call('GET', '/api/family/residents/1', family2.accessToken);
check('Family xem cư dân không liên kết -> 404', famOther.status === 404);
const famInvoice = await call('GET', '/api/invoices/2', family2.accessToken);
check('Family xem hóa đơn của người thân', famInvoice.status === 200 && famInvoice.data.remaining === 8000000);
const checkout = await call('POST', '/api/invoices/2/payos/checkout', family2.accessToken);
check('Tạo mã VietQR PayOS', checkout.status === 200 && !!checkout.data.qrCode, `${checkout.data?.amount} ${checkout.data?.bankName} ${checkout.data?.accountNumber}`);
const again = await call('POST', '/api/invoices/2/payos/checkout', family2.accessToken);
check('Tải lại trang dùng lại đúng QR cũ', again.data.transactionId === checkout.data.transactionId && again.data.qrCode === checkout.data.qrCode);
const otherInvoice = await call('POST', '/api/invoices/1/payos/checkout', family2.accessToken);
check('Family trả hóa đơn không phải người thân -> 403', otherInvoice.status === 403);

const orderCode = checkout.data.transactionId * 1000 + 1;
await fetch(`${FAKE}/dev/pay/${orderCode}`, { method: 'POST', redirect: 'manual' });
const status = await call('GET', `/api/payments/transactions/${checkout.data.transactionId}`, family2.accessToken);
check('Webhook PayOS -> giao dịch Paid, hóa đơn Paid', status.data.status === 'Paid' && status.data.invoiceStatus === 'Paid', JSON.stringify(status.data));
const famNotes = await call('GET', '/api/notifications', family2.accessToken);
check('Family nhận thông báo đã thanh toán', famNotes.data.items.some((n) => n.type === 'Payment'));

const logs = await call('GET', '/api/payments/webhook-logs', accountant.accessToken);
check('Nhật ký webhook APPLIED', logs.data.some((l) => l.outcome === 'APPLIED' && l.orderCode === orderCode));

// Webhook giả mạo (sai chữ ký) -> REJECTED, không ghi tiền
const forged = await fetch(`${API}/api/payments/payos/webhook`, { method: 'POST', headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ code: '00', desc: 'success', data: { orderCode: 5001, amount: 1000, reference: 'FAKE' }, signature: 'deadbeef' }) });
const forgedBody = await forged.json();
check('Webhook sai chữ ký -> 200 + REJECTED', forged.status === 200 && forgedBody.data.outcome === 'REJECTED');

const dash = await call('GET', '/api/dashboard', accountant.accessToken);
check('Dashboard', dash.status === 200, `collected=${dash.data.collected} debt=${dash.data.debt}`);

// OData chỉ dùng attribute routing: controller REST trùng tên entity set (InvoicesController, CareTasksController) không được bắt nhầm.
const odataInvoices = await call('GET', '/odata/Invoices?$count=true', accountant.accessToken);
check('GET /odata/Invoices trả danh sách (không lạc sang InvoicesController.Get(id))', odataInvoices.status === 200 && odataInvoices.data['@odata.count'] >= 1);

// ---- Sprint 3 · S3-V1: tiếp nhận & tham quan ----
// Form công khai giới hạn 5 lần/phút/IP (RateLimit:PublicFormsPerMinute): mục này gọi đúng 4 lần trước phần kiểm 429.
const plusDays = (n) => new Date(Date.now() + 7 * 3600e3 + n * 86400e3).toISOString().slice(0, 10); // ngày theo giờ VN
const recep = await login('receptionist@nursinghome.local');
const inq = await call('POST', '/api/public/inquiries', null, { fullName: 'Khách test', phone: '090 123 4567', careNeeds: 'Tư vấn gói cơ bản', interestedPackageId: 1 });
check('Khách tạo yêu cầu tư vấn không cần đăng nhập', inq.status === 200, inq.data?.message);
const newInquiries = await call('GET', "/odata/Inquiries?$filter=status eq 'New'&$count=true", recep.accessToken);
check('Lễ tân thấy yêu cầu mới (OData)', newInquiries.data['@odata.count'] >= 2, `count=${newInquiries.data['@odata.count']}`);
const mine = newInquiries.data.value.find((item) => item.fullName === 'Khách test');
check('SĐT được chuẩn hóa, gói quan tâm có tên', mine?.phone === '0901234567' && mine?.interestedPackageName === 'Gói Cơ bản');
check('Caregiver không xem được yêu cầu -> 403', (await call('GET', '/odata/Inquiries', caregiver.accessToken)).status === 403);
check('Family không sửa được yêu cầu -> 403', (await call('PUT', `/api/inquiries/${mine.id}`, family2.accessToken, { status: 'Contacted' })).status === 403);
const recepNotes = await call('GET', '/api/notifications', recep.accessToken);
check('Lễ tân nhận thông báo yêu cầu mới', recepNotes.data.items.some((n) => n.title === 'Yêu cầu tư vấn mới'));

const tourDate = plusDays(30);
const freeSlots = await call('GET', `/api/public/tour-slots?from=${tourDate}&to=${tourDate}`);
check('Xem chỗ trống buổi tham quan (ẩn danh)', freeSlots.status === 200 && freeSlots.data.length === 2 && freeSlots.data.every((s) => s.remaining === s.capacity));
const tour1 = await call('POST', '/api/public/tour-bookings', null, { fullName: 'Khách tham quan 1', phone: '0912000111', email: 'khach1@example.com', visitDate: tourDate, timeSlot: 'Morning', guestCount: 3 });
check('Đặt lịch tham quan 3 khách', tour1.status === 200, tour1.data?.message);
const tour2 = await call('POST', '/api/public/tour-bookings', null, { fullName: 'Khách tham quan 2', phone: '0912000222', visitDate: tourDate, timeSlot: 'Morning', guestCount: 3 });
check('Buổi không đủ chỗ (sức chứa 5) -> 400', tour2.status === 400, tour2.data?.message);
const todayTour = await call('POST', '/api/public/tour-bookings', null, { fullName: 'Khách hôm nay', phone: '0912000333', visitDate: plusDays(0), timeSlot: 'Afternoon', guestCount: 1 });
check('Không nhận đặt lịch cho hôm nay -> 400', todayTour.status === 400);
const leftSlots = await call('GET', `/api/public/tour-slots?from=${tourDate}&to=${tourDate}`);
check('Còn 2 chỗ buổi sáng', leftSlots.data.find((s) => s.slot === 'Morning')?.remaining === 2);

const tourList = await call('GET', `/api/tour-bookings?from=${tourDate}&to=${tourDate}`, recep.accessToken);
const booked = tourList.data.find((t) => t.fullName === 'Khách tham quan 1');
check('Lễ tân thấy lịch tham quan', tourList.status === 200 && booked?.status === 'Requested');
const confirmed = await call('POST', `/api/tour-bookings/${booked.id}/confirm`, recep.accessToken);
check('Xác nhận lịch (chưa cấu hình SMTP vẫn xác nhận)', confirmed.status === 200 && confirmed.data.status === 'Confirmed' && confirmed.data.confirmedById === 2);
check('Chưa đến ngày -> không đánh dấu đã đến', (await call('POST', `/api/tour-bookings/${booked.id}/done`, recep.accessToken)).status === 400);
const seededDone = await call('POST', '/api/tour-bookings/1/no-show', recep.accessToken); // lịch seed ngày mai
check('Lịch ngày mai chưa đánh dấu vắng được -> 400', seededDone.status === 400);

const contacted = await call('PUT', `/api/inquiries/${mine.id}`, recep.accessToken, { status: 'Contacted', assignedToId: 2, notes: 'Đã gọi tư vấn' });
check('Yêu cầu New -> Contacted, giao lễ tân', contacted.status === 200 && contacted.data.status === 'Contacted' && contacted.data.assignedToName);
check('Không PUT thẳng sang Admitted -> 400', (await call('PUT', `/api/inquiries/${mine.id}`, recep.accessToken, { status: 'Admitted', assignedToId: 2 })).status === 400);
check('Giao cho caregiver -> 400', (await call('PUT', `/api/inquiries/${mine.id}`, recep.accessToken, { status: 'Contacted', assignedToId: 6 })).status === 400);
check('Chưa tham quan thì chưa nhập viện -> 400', (await call('POST', `/api/inquiries/${mine.id}/admit`, recep.accessToken, { residentId: 1 })).status === 400);
await call('PUT', `/api/inquiries/${mine.id}`, recep.accessToken, { status: 'Visited', assignedToId: 2, notes: 'Đã tham quan' });
const newResident = await call('POST', '/api/residents', recep.accessToken, { code: 'NH-T001', fullName: 'Cụ Khách Test', dateOfBirth: '1940-05-01', gender: 'Nam' });
const admitted = await call('POST', `/api/inquiries/${mine.id}/admit`, recep.accessToken, { residentId: newResident.data?.id });
check('Nhập viện từ yêu cầu -> Admitted + ResidentId', admitted.status === 200 && admitted.data.status === 'Admitted' && admitted.data.residentId === newResident.data.id,
  JSON.stringify(admitted.data));
check('Admitted là trạng thái cuối -> 400', (await call('PUT', `/api/inquiries/${mine.id}`, recep.accessToken, { status: 'Lost', assignedToId: 2 })).status === 400);

let limited = false;
for (let i = 0; i < 8 && !limited; i++) {
  limited = (await call('POST', '/api/public/inquiries', null, { fullName: `Spam ${i}`, phone: '0900000000' })).status === 429;
}
check('Form công khai quá 5 lần/phút -> 429', limited);

console.log(failures === 0 ? '\nTẤT CẢ PASS' : `\n${failures} FAIL`);
process.exit(failures === 0 ? 0 : 1);
