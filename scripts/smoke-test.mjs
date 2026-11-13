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

// ---- Sprint 3 · S3-V2: ca trực & phân công phụ trách ----
const vnNow = new Date(Date.now() + 7 * 3600e3);
const thisMonday = plusDays(-((vnNow.getUTCDay() + 6) % 7));
const mondayPlus = (n) => new Date(Date.parse(thisMonday) + n * 86400e3).toISOString().slice(0, 10);
const nextMonday = mondayPlus(7);
const shiftList = await call('GET', '/api/shifts', recep.accessToken);
check('Danh mục 3 ca (mọi nhân viên đọc được)', shiftList.status === 200 && shiftList.data.length === 3);
const shiftOf = (code) => shiftList.data.find((s) => s.code === code).id;
check('Receptionist xếp ca -> 403', (await call('POST', '/api/shift-assignments', recep.accessToken, { accountId: 7, shiftId: shiftOf('MORNING'), workDate: nextMonday })).status === 403);
const morningCg2 = await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('MORNING'), workDate: nextMonday });
check('Admin xếp ca sáng thứ Hai tuần sau cho caregiver2', morningCg2.status === 200 && !morningCg2.data.warning, morningCg2.data?.message);
check('Xếp trùng người + ngày + ca -> 409', (await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('MORNING'), workDate: nextMonday })).status === 409);
const afternoonCg2 = await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('AFTERNOON'), workDate: nextMonday });
check('Ca liền kề vẫn tạo nhưng có cảnh báo', afternoonCg2.status === 200 && !!afternoonCg2.data.warning, afternoonCg2.data?.warning);
const nightCg2 = await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('NIGHT'), workDate: nextMonday });
check('Ca đêm 8 tiếng, kết thúc sáng hôm sau', nightCg2.status === 200
  && Date.parse(nightCg2.data.endsAt) - Date.parse(nightCg2.data.startsAt) === 8 * 3600e3
  && nightCg2.data.endsAt.slice(0, 10) === mondayPlus(7) && nightCg2.data.startsAt.slice(0, 10) === mondayPlus(7), `${nightCg2.data?.startsAt} → ${nightCg2.data?.endsAt}`);
check('Không xếp ca cho lễ tân -> 400', (await call('POST', '/api/shift-assignments', adminToken, { accountId: 2, shiftId: shiftOf('MORNING'), workDate: nextMonday })).status === 400);
check('Không xếp ca ngày đã qua -> 400', (await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('MORNING'), workDate: plusDays(-1) })).status === 400);
check('Admin bỏ ca đêm vừa xếp', (await call('DELETE', `/api/shift-assignments/${nightCg2.data.id}`, adminToken)).status === 204);

const caregiver2 = await login('caregiver2@nursinghome.local');
const cg2Mine = await call('GET', '/api/shift-assignments/mine?days=14', caregiver2.accessToken);
check('Caregiver2 xem "Lịch ca của tôi" 14 ngày', cg2Mine.status === 200 && cg2Mine.data.every((a) => a.accountId === 7)
  && cg2Mine.data.some((a) => a.workDate === nextMonday && a.shiftCode === 'MORNING'));
check('Caregiver2 nhận thông báo lịch ca', (await call('GET', '/api/notifications', caregiver2.accessToken)).data.items.some((n) => n.title === 'Lịch ca mới'));
const weekRange = `from=${thisMonday}&to=${mondayPlus(6)}`;
const cgWeek = await call('GET', `/api/shift-assignments?${weekRange}`, caregiver.accessToken);
check('Caregiver chỉ thấy ca của mình', cgWeek.status === 200 && cgWeek.data.length === 5 && cgWeek.data.every((a) => a.accountId === 6));
check('Nurse thấy lịch ca của mọi người', (await call('GET', `/api/shift-assignments?${weekRange}`, nurse.accessToken)).data.length === 15);
const copied = await call('POST', '/api/shift-assignments/copy-week', adminToken, { fromWeekStart: thisMonday, toWeekStart: nextMonday });
check('Sao chép tuần này sang tuần sau, bỏ qua ô đã có', copied.status === 200 && copied.data.copied === 14 && copied.data.skipped === 1, JSON.stringify(copied.data));
check('Sao chép từ ngày không phải thứ Hai -> 400', (await call('POST', '/api/shift-assignments/copy-week', adminToken, { fromWeekStart: mondayPlus(1), toWeekStart: nextMonday })).status === 400);

const careAssign = await call('POST', '/api/care-assignments', nurse.accessToken, { residentId: 3, accountId: 6 });
check('Nurse phân công caregiver phụ trách NH-0003 (mọi ca)', careAssign.status === 200 && careAssign.data.isActive && careAssign.data.shiftId === null, careAssign.data?.message);
check('NH-0003 đã có người phụ trách mọi ca -> 409', (await call('POST', '/api/care-assignments', nurse.accessToken, { residentId: 3, accountId: 7, shiftId: shiftOf('MORNING') })).status === 409);
check('Phụ trách phải là Nurse/Caregiver -> 400', (await call('POST', '/api/care-assignments', nurse.accessToken, { residentId: 1, accountId: 4 })).status === 400);
check('Caregiver không tự phân công -> 403', (await call('POST', '/api/care-assignments', caregiver.accessToken, { residentId: 1, accountId: 6 })).status === 403);
check('Receptionist xem phân công -> 403', (await call('GET', '/api/care-assignments', recep.accessToken)).status === 403);
const nh3Assignments = await call('GET', '/api/care-assignments?residentId=3', nurse.accessToken);
check('Danh sách phân công đang hiệu lực của NH-0003', nh3Assignments.data.length === 1 && nh3Assignments.data[0].accountName === 'Hoàng Văn Nam');
const cg2Care = await call('GET', '/api/care-assignments', caregiver2.accessToken);
check('Caregiver chỉ thấy phân công của chính mình', cg2Care.status === 200 && cg2Care.data.length === 1 && cg2Care.data.every((a) => a.accountId === 7));
// Quyền: caregiver chưa có việc với NH-0003 nhưng là người phụ trách chính -> xem được hồ sơ (ResidentAccessService).
check('Caregiver phụ trách NH-0003 xem được hồ sơ dù chưa có việc', (await call('GET', '/api/residents/3', caregiver.accessToken)).status === 200);
check('Danh sách cư dân của caregiver có thêm NH-0003',
  (await call('GET', '/odata/Residents', caregiver.accessToken)).data.value.map((r) => r.code).sort().join() === 'NH-0002,NH-0003');
check('Phụ trách vẫn không xem được hóa đơn -> 403', (await call('GET', '/api/residents/3/subscriptions', caregiver.accessToken)).status === 403);
const endedAssign = await call('POST', `/api/care-assignments/${careAssign.data.id}/end`, nurse.accessToken);
check('Kết thúc phân công phụ trách', endedAssign.status === 200 && !endedAssign.data.isActive && !!endedAssign.data.endDate);
check('Hết phân công -> caregiver lại không xem được NH-0003 (403)', (await call('GET', '/api/residents/3', caregiver.accessToken)).status === 403);
check('Kết thúc lần hai -> 400', (await call('POST', `/api/care-assignments/${careAssign.data.id}/end`, nurse.accessToken)).status === 400);

// ---- Sprint 3 · S3-V3: báo cáo + xuất Excel ----
const yearNow = Number(plusDays(0).slice(0, 4));
const monthIdx = Number(plusDays(0).slice(5, 7)) - 1;
const download = async (path, token) => {
  const res = await fetch(API + path, { headers: { authorization: `Bearer ${token}` } });
  const bytes = new Uint8Array(await res.arrayBuffer());
  return { status: res.status, type: res.headers.get('content-type') ?? '', name: res.headers.get('content-disposition') ?? '', zip: bytes[0] === 0x50 && bytes[1] === 0x4b,
    pdf: String.fromCharCode(...bytes.slice(0, 4)) === '%PDF' };
};
const revenue = await call('GET', `/api/reports/revenue?year=${yearNow}`, accountant.accessToken);
check('Báo cáo doanh thu đủ 12 tháng + bảng công nợ', revenue.status === 200 && revenue.data.months.length === 12 && revenue.data.debts.length >= 1,
  `thu=${revenue.data?.totalCollected} nợ=${revenue.data?.totalDebt}`);
check('Tháng này có khoản thu PayOS vừa thanh toán', revenue.data.months[monthIdx].collectedOnline >= 8000000);
check('Tổng công nợ khớp Dashboard', revenue.data.totalDebt === dash.data.debt, `${revenue.data.totalDebt} vs ${dash.data.debt}`);
const revenueXlsx = await download(`/api/reports/revenue/export?year=${yearNow}`, accountant.accessToken);
check('Xuất Excel doanh thu (.xlsx)', revenueXlsx.status === 200 && revenueXlsx.type.includes('spreadsheetml') && revenueXlsx.zip && revenueXlsx.name.includes('doanh-thu'));
const occupancy = await call('GET', `/api/reports/occupancy?year=${yearNow}`, recep.accessToken);
check('Công suất giường tháng này (lễ tân xem được)', occupancy.status === 200 && occupancy.data.months[monthIdx].percent > 0
  && occupancy.data.months[monthIdx].percent <= 100, `${occupancy.data?.months?.[monthIdx]?.percent}%`);
check('Xuất Excel công suất giường', (await download(`/api/reports/occupancy/export?year=${yearNow}`, recep.accessToken)).zip);
const adherence = await call('GET', '/api/reports/medication-adherence', doctor.accessToken);
const nh1Adherence = adherence.data?.rows?.find((r) => r.code === 'NH-0001');
check('Tuân thủ thuốc 30 ngày (bác sĩ)', adherence.status === 200 && nh1Adherence?.total > 0 && nh1Adherence.takenRate > 0 && nh1Adherence.takenRate <= 100,
  `NH-0001 ${nh1Adherence?.taken}/${nh1Adherence?.total} = ${nh1Adherence?.takenRate}%`);
check('Xuất Excel tuân thủ thuốc', (await download('/api/reports/medication-adherence/export', doctor.accessToken)).zip);
check('Khoảng ngày ngược -> 400', (await call('GET', `/api/reports/medication-adherence?from=${plusDays(0)}&to=${plusDays(-5)}`, doctor.accessToken)).status === 400);
check('Doctor xem doanh thu -> 403', (await call('GET', '/api/reports/revenue', doctor.accessToken)).status === 403);
check('Caregiver xem công suất -> 403', (await call('GET', '/api/reports/occupancy', caregiver.accessToken)).status === 403);
check('Family tải Excel doanh thu -> 403', (await download('/api/reports/revenue/export', family2.accessToken)).status === 403);

// ---- Sprint 3 · S3-V4: nghỉ phép/đổi ca & bàn giao ca ----
// Sau S3-V2, thứ Ba tuần sau: caregiver (6) ca sáng, caregiver2 (7) ca chiều.
const nextTuesday = mondayPlus(8);
const cgNext = (await call('GET', `/api/shift-assignments/mine?days=28`, caregiver.accessToken)).data;
const myTueMorning = cgNext.find((a) => a.workDate === nextTuesday && a.shiftCode === 'MORNING');
const swapOptions = await call('GET', `/api/leave-requests/swap-candidates?shiftAssignmentId=${myTueMorning?.id}`, caregiver.accessToken);
const theirTueAfternoon = swapOptions.data?.find?.((a) => a.workDate === nextTuesday && a.shiftCode === 'AFTERNOON' && a.accountId === 7);
check('Gợi ý ca đổi được: ca chiều thứ Ba của caregiver2, không có ô làm trùng', swapOptions.status === 200 && !!theirTueAfternoon
  && swapOptions.data.every((a) => a.accountId === 7 && !(a.shiftCode === 'MORNING' && a.workDate === mondayPlus(7))), `${swapOptions.data?.length} ca`);
check('Không xin đổi ca của người khác -> 403', (await call('GET', `/api/leave-requests/swap-candidates?shiftAssignmentId=${theirTueAfternoon?.id}`, caregiver.accessToken)).status === 403);
const swapReq = await call('POST', '/api/leave-requests', caregiver.accessToken,
  { type: 'Swap', shiftAssignmentId: myTueMorning?.id, swapShiftAssignmentId: theirTueAfternoon?.id, reason: 'Sáng thứ Ba đưa con đi khám.' });
check('Caregiver xin đổi ca sáng lấy ca chiều của caregiver2', swapReq.status === 200 && swapReq.data.status === 'Pending' && swapReq.data.swapWithName === 'Đỗ Thị Hoa', swapReq.data?.message);
check('Đơn đổi ca trùng ca đang chờ -> 409', (await call('POST', '/api/leave-requests', caregiver.accessToken,
  { type: 'Swap', shiftAssignmentId: myTueMorning?.id, swapShiftAssignmentId: theirTueAfternoon?.id, reason: 'Gửi lại lần nữa' })).status === 409);
const nurseTuesday = (await call('GET', `/api/shift-assignments?from=${nextTuesday}&to=${nextTuesday}`, nurse.accessToken)).data.find((a) => a.accountId === 5);
check('Đổi ca với người khác vai trò -> 400', (await call('POST', '/api/leave-requests', caregiver.accessToken,
  { type: 'Swap', shiftAssignmentId: myTueMorning?.id, swapShiftAssignmentId: nurseTuesday?.id, reason: 'Thử khác vai trò' })).status === 400);
check('Lễ tân xem đơn nghỉ phép -> 403', (await call('GET', '/api/leave-requests', recep.accessToken)).status === 403);
const cg2Requests = await call('GET', '/api/leave-requests', caregiver2.accessToken);
check('Caregiver2 thấy đơn nghỉ của mình + đơn xin đổi ca với mình', cg2Requests.status === 200
  && cg2Requests.data.some((r) => r.id === 1 && r.type === 'Leave') && cg2Requests.data.some((r) => r.id === swapReq.data.id));
check('Caregiver không thấy đơn nghỉ của caregiver2', (await call('GET', '/api/leave-requests', caregiver.accessToken)).data.every((r) => r.accountId === 6 || r.swapWithAccountId === 6));
check('Caregiver tự duyệt đơn -> 403', (await call('POST', `/api/leave-requests/${swapReq.data.id}/approve`, caregiver.accessToken, {})).status === 403);
check('Caregiver2 có thông báo xin đổi ca', (await call('GET', '/api/notifications', caregiver2.accessToken)).data.items.some((n) => n.title.includes('xin đổi ca với bạn')));
const swapOk = await call('POST', `/api/leave-requests/${swapReq.data.id}/approve`, adminToken, { note: 'Đồng ý.' });
check('Admin duyệt đổi ca', swapOk.status === 200 && swapOk.data.request.status === 'Approved', swapOk.data?.message);
const tueAfter = (await call('GET', `/api/shift-assignments?from=${nextTuesday}&to=${nextTuesday}`, nurse.accessToken)).data;
check('Lịch thứ Ba đã hoán đổi đúng người', tueAfter.some((a) => a.accountId === 6 && a.shiftCode === 'AFTERNOON')
  && tueAfter.some((a) => a.accountId === 7 && a.shiftCode === 'MORNING')
  && !tueAfter.some((a) => a.accountId === 6 && a.shiftCode === 'MORNING'), tueAfter.map((a) => `${a.accountId}:${a.shiftCode}`).join(' '));
check('Duyệt lại đơn đã duyệt -> 400', (await call('POST', `/api/leave-requests/${swapReq.data.id}/approve`, adminToken, {})).status === 400);
check('Caregiver nhận thông báo duyệt đổi ca', (await call('GET', '/api/notifications', caregiver.accessToken)).data.items.some((n) => n.title === 'Đơn đổi ca được duyệt'));

check('Xin nghỉ ngày đã qua -> 400', (await call('POST', '/api/leave-requests', caregiver.accessToken,
  { type: 'Leave', fromDate: plusDays(-1), toDate: plusDays(1), reason: 'Ốm' })).status === 400);
check('Xin nghỉ quá 31 ngày -> 400', (await call('POST', '/api/leave-requests', caregiver.accessToken,
  { type: 'Leave', fromDate: plusDays(1), toDate: plusDays(40), reason: 'Nghỉ dài' })).status === 400);
const ownLeave = await call('POST', '/api/leave-requests', caregiver.accessToken, { type: 'Leave', fromDate: plusDays(20), toDate: plusDays(21), reason: 'Việc riêng' });
check('Caregiver tự hủy đơn nghỉ đang chờ', ownLeave.status === 200
  && (await call('POST', `/api/leave-requests/${ownLeave.data.id}/cancel`, caregiver.accessToken)).data.status === 'Cancelled');
// Đơn nghỉ demo (Id 1) của caregiver2: +7..+8 ngày. Xếp thêm ca đêm +7 để chắc có ca bị bỏ dù hôm nay là thứ mấy.
await call('POST', '/api/shift-assignments', adminToken, { accountId: 7, shiftId: shiftOf('NIGHT'), workDate: plusDays(7) });
const leaveOk = await call('POST', '/api/leave-requests/1/approve', adminToken, {});
check('Duyệt nghỉ: bỏ ca trong khoảng nghỉ, trả danh sách ca trống', leaveOk.status === 200 && leaveOk.data.vacatedShifts.length >= 1
  && leaveOk.data.vacatedShifts.every((s) => s.workDate >= plusDays(7) && s.workDate <= plusDays(8)), JSON.stringify(leaveOk.data?.vacatedShifts?.map((s) => `${s.workDate} ${s.shiftCode}`)));
check('Lịch caregiver2 trong khoảng nghỉ đã trống',
  (await call('GET', `/api/shift-assignments?from=${plusDays(7)}&to=${plusDays(8)}`, nurse.accessToken)).data.every((a) => a.accountId !== 7));
check('Admin nhận thông báo "Cần xếp người thay"', (await call('GET', '/api/notifications', adminToken)).data.items.some((n) => n.title.startsWith('Cần xếp người thay')));

const yesterdayVn = plusDays(-1);
const nurseDraft = await call('GET', `/api/handovers/draft?shiftId=${shiftOf('MORNING')}&date=${plusDays(0)}`, nurse.accessToken);
check('Gợi ý bàn giao ca sáng (điều dưỡng): việc chưa xong của mọi cư dân', nurseDraft.status === 200
  && nurseDraft.data.items.some((i) => i.kind === 'Task' && i.text.includes('Theo dõi bữa trưa'))
  && !nurseDraft.data.items.some((i) => i.text.includes('Đo sinh hiệu buổi sáng')), `${nurseDraft.data?.items?.length} mục`);
const cgDraft = await call('GET', `/api/handovers/draft?shiftId=${shiftOf('MORNING')}&date=${plusDays(0)}`, caregiver.accessToken);
check('Gợi ý bàn giao của caregiver chỉ gồm cư dân mình được xem', cgDraft.status === 200
  && cgDraft.data.items.some((i) => i.text.includes('Hỗ trợ đi lại')) && cgDraft.data.items.every((i) => i.residentId === 2));
check('Lễ tân lấy gợi ý bàn giao -> 403', (await call('GET', `/api/handovers/draft?shiftId=1`, recep.accessToken)).status === 403);
const handover = await call('POST', '/api/handovers', adminToken, {
  shiftId: shiftOf('NIGHT'), workDate: yesterdayVn, summary: 'Ca đêm yên tĩnh, NH-0002 ngủ ngon, không sốt lại.',
  highlights: [{ kind: 'Note', residentId: 2, residentName: 'NH-0002', text: 'Theo dõi nhiệt độ sáng nay.' }],
});
check('Admin viết bàn giao ca đêm hôm qua', handover.status === 200 && handover.data.highlights.length === 1 && !handover.data.canAcknowledge, handover.data?.message);
check('Viết bàn giao trùng ca -> 409', (await call('POST', '/api/handovers', adminToken, { shiftId: shiftOf('NIGHT'), workDate: yesterdayVn, summary: 'Lần hai nè' })).status === 409);
check('Không có ca thì không viết bàn giao -> 403', (await call('POST', '/api/handovers', caregiver2.accessToken, { shiftId: shiftOf('NIGHT'), workDate: yesterdayVn, summary: 'Thử viết' })).status === 403);
check('Ca chưa bắt đầu thì chưa viết được -> 400', (await call('POST', '/api/handovers', adminToken, { shiftId: shiftOf('MORNING'), workDate: plusDays(1), summary: 'Viết trước' })).status === 400);
const nurseInbox = await call('GET', '/api/handovers/inbox', nurse.accessToken);
check('Điều dưỡng thấy bàn giao chờ xác nhận', nurseInbox.status === 200 && nurseInbox.data.some((h) => h.id === handover.data.id));
check('Danh sách bàn giao hôm nay có bàn giao demo chưa đọc', (await call('GET', `/api/handovers?date=${plusDays(0)}`, nurse.accessToken)).data.some((h) => h.id === 2 && !h.acknowledgedAt));
check('Admin tự xác nhận bàn giao của mình -> 400', (await call('POST', `/api/handovers/${handover.data.id}/acknowledge`, adminToken)).status === 400);
const acked = await call('POST', `/api/handovers/${handover.data.id}/acknowledge`, nurse.accessToken);
check('Điều dưỡng bấm "Đã đọc"', acked.status === 200 && acked.data.acknowledgedByName === 'ĐD. Trần Thị Mai' && !acked.data.canAcknowledge);
check('Xác nhận lần hai -> 400', (await call('POST', `/api/handovers/${handover.data.id}/acknowledge`, nurse.accessToken)).status === 400);
check('Người viết nhận thông báo bàn giao đã được đọc', (await call('GET', '/api/notifications', adminToken)).data.items.some((n) => n.title === 'Bàn giao ca đã được đọc'));
// Bàn giao demo Id 2: ca sáng hôm nay → ca kế tiếp là ca chiều; caregiver (6) chỉ có ca sáng.
check('Caregiver không làm ca kế tiếp thì không xác nhận được -> 403', (await call('POST', '/api/handovers/2/acknowledge', caregiver.accessToken)).status === 403);

// ---- Sprint 3 · S3-V5: dịch vụ phát sinh, hóa đơn hàng loạt, PDF, nhắc nợ ----
const thisMonth1 = `${plusDays(0).slice(0, 8)}01`;
const firstOfMonthPlus = (n) => { const d = new Date(`${thisMonth1}T00:00:00Z`); d.setUTCMonth(d.getUTCMonth() + n); return d.toISOString().slice(0, 10); };
const nextMonth1 = firstOfMonthPlus(1);
const extraList = await call('GET', '/api/extra-services?activeOnly=true', caregiver.accessToken);
check('Danh mục 4 dịch vụ phát sinh (nhân viên xem được)', extraList.status === 200 && extraList.data.length === 4);
const laundry = extraList.data.find((s) => s.name === 'Giặt ủi');
check('Lễ tân thêm dịch vụ -> 403', (await call('POST', '/api/extra-services', recep.accessToken, { name: 'Cắt móng', unit: 'lần', unitPrice: 40000, isActive: true })).status === 403);
check('Admin thêm dịch vụ', (await call('POST', '/api/extra-services', adminToken, { name: 'Cắt móng', unit: 'lần', unitPrice: 40000, isActive: true })).status === 200);
check('Trùng tên dịch vụ -> 409', (await call('POST', '/api/extra-services', adminToken, { name: 'Cắt móng', unit: 'lần', unitPrice: 1, isActive: true })).status === 409);
const usage = await call('POST', '/api/residents/2/service-usages', caregiver.accessToken, { extraServiceId: laundry.id, quantity: 2, note: 'Ga giường' });
check('Caregiver ghi Giặt ủi 2 kg cho NH-0002 (giá chốt 30.000)', usage.status === 200 && usage.data.unitPrice === 30000 && usage.data.amount === 60000 && !usage.data.invoiced, usage.data?.message);
check('Admin đổi giá danh mục', (await call('PUT', `/api/extra-services/${laundry.id}`, adminToken, { name: 'Giặt ủi', unit: 'kg', unitPrice: 35000, isActive: true })).data.unitPrice === 35000);
check('Caregiver ghi cho cư dân ngoài phạm vi -> 403', (await call('POST', '/api/residents/1/service-usages', caregiver.accessToken, { extraServiceId: laundry.id, quantity: 1 })).status === 403);
check('Số lượng 3 chữ số thập phân -> 400', (await call('POST', '/api/residents/2/service-usages', caregiver.accessToken, { extraServiceId: laundry.id, quantity: 1.255 })).status === 400);
check('Ngày sử dụng ở tương lai -> 400', (await call('POST', '/api/residents/2/service-usages', caregiver.accessToken, { extraServiceId: laundry.id, quantity: 1, usedOn: plusDays(1) })).status === 400);
check('Family ghi dịch vụ -> 403', (await call('POST', '/api/residents/2/service-usages', family2.accessToken, { extraServiceId: laundry.id, quantity: 1 })).status === 403);
const extraInvoice = await call('POST', '/api/invoices', accountant.accessToken, { residentId: 2, billingPeriod: thisMonth1, dueDate: plusDays(2) });
check('Tạo hóa đơn tháng này cho NH-0002 tự thêm dòng Giặt ủi với giá chốt', extraInvoice.status === 200
  && extraInvoice.data.items.some((i) => i.description.startsWith('Giặt ủi') && i.unitPrice === 30000 && i.quantity === 2), extraInvoice.data?.message);
const usageAfter = (await call('GET', '/api/residents/2/service-usages', caregiver.accessToken)).data.find((u) => u.id === usage.data.id);
check('Lần sử dụng đã vào hóa đơn', usageAfter?.invoiced === true && usageAfter.invoiceId === extraInvoice.data.id);
check('Xóa lần sử dụng đã vào hóa đơn -> 400', (await call('DELETE', `/api/service-usages/${usage.data.id}`, caregiver.accessToken)).status === 400);
const spare = await call('POST', '/api/residents/2/service-usages', nurse.accessToken, { extraServiceId: laundry.id, quantity: 1 });
check('Lần ghi sau dùng giá mới', spare.status === 200 && spare.data.unitPrice === 35000);
check('Caregiver xóa lần người khác ghi -> 403', (await call('DELETE', `/api/service-usages/${spare.data.id}`, caregiver.accessToken)).status === 403);
check('Nurse xóa lần mình ghi (chưa vào hóa đơn)', (await call('DELETE', `/api/service-usages/${spare.data.id}`, nurse.accessToken)).status === 204);

check('Family tải PDF hóa đơn nháp -> 404', (await download(`/api/invoices/${extraInvoice.data.id}/pdf`, family2.accessToken)).status === 404);
const draftPdf = await download(`/api/invoices/${extraInvoice.data.id}/pdf`, accountant.accessToken);
check('Kế toán tải PDF hóa đơn nháp', draftPdf.status === 200 && draftPdf.type.includes('application/pdf') && draftPdf.pdf);
check('Phát hành hóa đơn', (await call('POST', `/api/invoices/${extraInvoice.data.id}/issue`, accountant.accessToken)).data.status === 'Unpaid');
const familyPdf = await download(`/api/invoices/${extraInvoice.data.id}/pdf`, family2.accessToken);
check('family2 tải PDF hóa đơn của NH-0002', familyPdf.status === 200 && familyPdf.pdf && familyPdf.name.includes('.pdf'));
check('family2 tải PDF hóa đơn của NH-0001 -> 403', (await download('/api/invoices/1/pdf', family2.accessToken)).status === 403);
check('Caregiver tải PDF -> 403', (await download(`/api/invoices/${extraInvoice.data.id}/pdf`, caregiver.accessToken)).status === 403);

check('Lễ tân chạy nhắc nợ -> 403', (await call('POST', '/api/invoices/reminders/run', recep.accessToken)).status === 403);
const remindRun = await call('POST', '/api/invoices/reminders/run', accountant.accessToken);
check('Chạy nhắc nợ: có lượt nhắc mới', remindRun.status === 200 && remindRun.data.sent >= 1, JSON.stringify(remindRun.data));
const remindAgain = await call('POST', '/api/invoices/reminders/run', accountant.accessToken);
check('Chạy lại không nhắc trùng mốc', remindAgain.status === 200 && remindAgain.data.sent === 0, JSON.stringify(remindAgain.data));
const sentStages = await call('GET', `/api/invoices/${extraInvoice.data.id}/reminders`, accountant.accessToken);
check('Hóa đơn hạn +2 ngày chỉ có 1 mốc "trước hạn 3 ngày"', sentStages.data.length === 1 && sentStages.data[0].stage === 'Before3Days', JSON.stringify(sentStages.data));
check('family2 nhận thông báo nhắc thanh toán', (await call('GET', '/api/notifications', family2.accessToken)).data.items.some((n) => n.title.startsWith('Nhắc thanh toán')));

const batch = await call('POST', '/api/invoices/batch', accountant.accessToken, { billingPeriod: nextMonth1, dueDate: nextMonth1.replace(/01$/, '10') });
const batchCodes = batch.data?.created?.map((i) => i.residentCode) ?? [];
check('Hóa đơn hàng loạt tháng sau: tạo cho NH-0001..0004', batch.status === 200 && ['NH-0001', 'NH-0002', 'NH-0003', 'NH-0004'].every((c) => batchCodes.includes(c)), batchCodes.join());
check('Bỏ qua NH-0005 vì đã ngừng lưu trú', batch.data.skipped.some((s) => s.residentCode === 'NH-0005' && s.reason.includes('ngừng lưu trú')), JSON.stringify(batch.data.skipped));
const nh1Next = await call('GET', `/api/invoices/${batch.data.created.find((i) => i.residentCode === 'NH-0001').id}`, accountant.accessToken);
check('Dịch vụ phát sinh chưa tính tiền vào hóa đơn kỳ sau', nh1Next.data.items.some((i) => i.description.startsWith('Giặt ủi') && i.quantity === 3.5));
// Hồi quy lõi: số lượng thập phân khi thêm khoản phí (trước đây [Range] parse "0.01" theo culture vi-VN → 500).
const addItem = await call('POST', `/api/invoices/${nh1Next.data.id}/items`, accountant.accessToken, { description: 'Phụ thu điện máy lạnh', quantity: 1.5, unitPrice: 100000 });
check('Thêm khoản phí số lượng 1,5 vào hóa đơn nháp', addItem.status === 200 && addItem.data.items.some((i) => i.quantity === 1.5 && i.amount === 150000), addItem.data?.message);
const batchAgain = await call('POST', '/api/invoices/batch', accountant.accessToken, { billingPeriod: nextMonth1, dueDate: nextMonth1.replace(/01$/, '10') });
check('Chạy lại hàng loạt không tạo trùng', batchAgain.data.created.length === 0 && batchAgain.data.skipped.filter((s) => s.reason.startsWith('Đã có hóa đơn')).length >= 4);
check('Kỳ không phải ngày 1 -> 400', (await call('POST', '/api/invoices/batch', accountant.accessToken, { billingPeriod: `${plusDays(0).slice(0, 8)}15`, dueDate: plusDays(30) })).status === 400);
await call('POST', `/api/invoices/${batch.data.created.find((i) => i.residentCode === 'NH-0003').id}/cancel`, accountant.accessToken);
const nh3Usages = await call('GET', `/api/residents/3/service-usages?from=${thisMonth1}&to=${plusDays(0)}`, accountant.accessToken);
check('Hủy hóa đơn trả dịch vụ phát sinh về "chưa tính"', nh3Usages.status === 200 && nh3Usages.data.length >= 1 && nh3Usages.data.every((u) => !u.invoiced));

// ---- Sprint 3 · S3-V6: chat gia đình – nhân viên ----
const family = await login('family@nursinghome.local');
const famList = await call('GET', '/api/conversations', family.accessToken);
check('Gia đình thấy cuộc trò chuyện demo về NH-0001', famList.status === 200 && famList.data.some((c) => c.id === 1 && c.residentCode === 'NH-0001' && c.unread === 0));
const nurseList = await call('GET', '/api/conversations', nurse.accessToken);
check('Điều dưỡng thấy 1 tin chưa đọc của gia đình', nurseList.status === 200 && nurseList.data.find((c) => c.id === 1)?.unread === 1);
check('Kế toán dùng tin nhắn -> 403', (await call('GET', '/api/conversations', accountant.accessToken)).status === 403);
check('Bác sĩ dùng tin nhắn -> 403', (await call('GET', '/api/conversations', doctor.accessToken)).status === 403);
check('family2 đọc cuộc trò chuyện của family -> 404', (await call('GET', '/api/conversations/1/messages', family2.accessToken)).status === 404);
check('family2 mở chat về cư dân không liên kết -> 404', (await call('POST', '/api/conversations', family2.accessToken, { residentId: 1 })).status === 404);
check('Nhân viên không tự mở cuộc trò chuyện -> 403', (await call('POST', '/api/conversations', nurse.accessToken, { residentId: 1 })).status === 403);
const history = await call('GET', '/api/conversations/1/messages', nurse.accessToken);
check('Lịch sử 3 tin theo thứ tự thời gian, có vai trò người gửi', history.status === 200 && history.data.length === 3
  && history.data[0].fromFamily && history.data[1].senderRole === 'Nurse' && Date.parse(history.data[0].sentAt) < Date.parse(history.data[2].sentAt));
check('Mở cuộc trò chuyện -> đánh dấu đã đọc', (await call('POST', '/api/conversations/1/read', nurse.accessToken)).status === 200
  && (await call('GET', '/api/conversations', nurse.accessToken)).data.find((c) => c.id === 1)?.unread === 0);

const conv2 = await call('POST', '/api/conversations', family2.accessToken, { residentId: 2 });
check('family2 mở cuộc trò chuyện về NH-0002', conv2.status === 200 && conv2.data.residentCode === 'NH-0002');
check('Mở lại trả đúng cuộc trò chuyện cũ', (await call('POST', '/api/conversations', family2.accessToken, { residentId: 2 })).data.id === conv2.data.id);
check('Tin rỗng -> 400', (await call('POST', `/api/conversations/${conv2.data.id}/messages`, family2.accessToken, { body: '   ' })).status === 400);
check('Tin quá 2000 ký tự -> 400', (await call('POST', `/api/conversations/${conv2.data.id}/messages`, family2.accessToken, { body: 'a'.repeat(2001) })).status === 400);
const fam2Msg = await call('POST', `/api/conversations/${conv2.data.id}/messages`, family2.accessToken, { body: 'Mẹ tôi hôm nay đã hết sốt chưa ạ?' });
check('family2 gửi tin', fam2Msg.status === 200 && fam2Msg.data.fromFamily && fam2Msg.data.senderName === 'Trần Quốc Bảo');
await call('POST', `/api/conversations/${conv2.data.id}/messages`, family2.accessToken, { body: 'Nhờ cơ sở nhắn giúp nhé.' });
const cgChatNotes = (await call('GET', '/api/notifications', caregiver.accessToken)).data.items.filter((n) => n.title === 'Tin nhắn mới từ gia đình NH-0002');
check('Người phụ trách chính nhận chuông 1 lần cho cả lượt tin chưa đọc', cgChatNotes.length === 1, `${cgChatNotes.length} thông báo`);
const cgConvs = await call('GET', '/api/conversations', caregiver.accessToken);
check('Caregiver chỉ thấy cuộc trò chuyện của cư dân mình phụ trách', cgConvs.status === 200
  && cgConvs.data.some((c) => c.id === conv2.data.id && c.unread === 2) && !cgConvs.data.some((c) => c.id === 1));
check('Caregiver đọc cuộc trò chuyện ngoài phạm vi -> 404', (await call('GET', '/api/conversations/1/messages', caregiver.accessToken)).status === 404);
const cgReply = await call('POST', `/api/conversations/${conv2.data.id}/messages`, caregiver.accessToken, { body: 'Dạ bà đã hết sốt từ sáng, ăn được nửa bát cháo ạ.' });
check('Caregiver trả lời gia đình', cgReply.status === 200 && !cgReply.data.fromFamily);
check('Gia đình nhận chuông tin nhắn từ nhân viên', (await call('GET', '/api/notifications', family2.accessToken)).data.items.some((n) => n.title === 'Tin nhắn mới từ Hoàng Văn Nam'));
check('Gia đình thấy 1 tin chưa đọc', (await call('GET', '/api/conversations', family2.accessToken)).data.find((c) => c.id === conv2.data.id)?.unread === 1);

// Realtime: dùng client SignalR trong FE/node_modules (bỏ qua nếu chưa npm install ở FE).
let signalR = null;
try {
  signalR = (await import('node:module')).createRequire(import.meta.url)('../FE/node_modules/@microsoft/signalr');
} catch {
  console.log('SKIP  Tin nhắn realtime (chưa cài FE/node_modules)');
}
if (signalR) {
  const hub = new signalR.HubConnectionBuilder()
    .withUrl(`${API}/hubs/notifications`, { accessTokenFactory: () => family.accessToken })
    .configureLogging(signalR.LogLevel.None)
    .build();
  const pushed = new Promise((resolve) => {
    hub.on('chat', (m) => resolve(m));
    setTimeout(() => resolve(null), 5000);
  });
  await hub.start();
  await call('POST', '/api/conversations/1/messages', nurse.accessToken, { body: 'Dạ cuối tuần 9h gia đình vào thăm được ạ.' });
  const live = await pushed;
  check('Gia đình nhận tin realtime (SignalR sự kiện "chat")', live?.body === 'Dạ cuối tuần 9h gia đình vào thăm được ạ.' && live.conversationId === 1);
  await hub.stop();
}

let chatStatus = 200;
let sentInMinute = 0;
while (chatStatus === 200 && sentInMinute < 25) {
  chatStatus = (await call('POST', `/api/conversations/${conv2.data.id}/messages`, family2.accessToken, { body: `Tin thử giới hạn ${sentInMinute}` })).status;
  sentInMinute++;
}
check('Gửi quá 20 tin/phút -> 429', chatStatus === 429 && sentInMinute <= 21, `dừng ở tin thứ ${sentInMinute}`);

// ---- Sprint 4 · S4-V1: chatbot tư vấn công khai (provider Fake: Ai__Enabled=true, Ai__Provider=Fake) ----
const botKey = crypto.randomUUID();
const ask = async (message) => (await call('POST', '/api/ai/public/chat', null, { conversationKey: botKey, message })).data?.reply ?? '';
const botConfig = await call('GET', '/api/ai/public/config', null);
check('Cấu hình chatbot công khai bật', botConfig.status === 200 && botConfig.data.enabled && botConfig.data.greeting.includes('trợ lý'));
const priceReply = await ask('Giá gói cơ bản bao nhiêu một tháng?');
check('Hỏi giá: trả lời lấy từ tool list_packages', priceReply.includes('8.000.000'), priceReply);
const bedReply = await ask('Còn giường trống không?');
check('Hỏi giường trống: trả lời từ dữ liệu', /\d+\/\d+ giường trống/.test(bedReply), bedReply);
const visitReply = await ask('Giờ thăm thế nào ạ?');
check('Hỏi giờ thăm: tra tài liệu tri thức', visitReply.includes('08:00–11:00'), visitReply);
const medicalReply = await ask('Bà tôi bị tiểu đường dùng thuốc gì?');
check('Câu hỏi thuốc: từ chối tư vấn y tế', medicalReply.includes('không tư vấn'), medicalReply);
check('Tài liệu nội bộ không lộ qua chatbot công khai', !(await ask('/tool search_knowledge {"query":"té ngã nội bộ"}')).includes('điều dưỡng trực'));
const inquiryArgs = { fullName: 'Khách chatbot', phone: '0987654321', careNeeds: 'Ông cần hỗ trợ đi lại', confirmed: true };
const askConfirm = await ask(`Tôi muốn được tư vấn /tool create_inquiry ${JSON.stringify(inquiryArgs)}`);
check('Tạo yêu cầu khi khách CHƯA xác nhận -> chỉ hỏi lại thông tin', askConfirm.includes('xác nhận') && askConfirm.includes('0987654321'), askConfirm);
const created = await ask(`Đồng ý /tool create_inquiry ${JSON.stringify(inquiryArgs)}`);
check('Khách xác nhận -> gửi yêu cầu tư vấn', created.startsWith('Đã gửi yêu cầu tư vấn'), created);
const chatbotInquiries = await call('GET', `/odata/Inquiries?$filter=source eq 'Chatbot'`, recep.accessToken);
check('Lễ tân thấy yêu cầu nguồn Chatbot', chatbotInquiries.status === 200 && chatbotInquiries.data.value.some((i) => i.phone === '0987654321'));
const tourArgs = { fullName: 'Khách chatbot', phone: '0987654321', date: plusDays(6), slot: 'Afternoon', guestCount: 1, confirmed: true };
const tourBooked = await ask(`Xác nhận /tool book_tour ${JSON.stringify(tourArgs)}`);
check('Đặt lịch tham quan qua chatbot sau khi xác nhận', tourBooked.startsWith('Đã đặt lịch tham quan buổi chiều'), tourBooked);
check('Lịch tham quan từ chatbot ở trạng thái chờ xác nhận', (await call('GET', `/api/tour-bookings?from=${plusDays(6)}&to=${plusDays(6)}`, recep.accessToken)).data
  .some((t) => t.phone === '0987654321' && t.status === 'Requested'));
const badTour = await ask(`Xác nhận /tool book_tour ${JSON.stringify({ ...tourArgs, date: 'ngày mai' })}`);
check('Tham số sai -> chatbot báo lỗi, không 500', badTour.includes('yyyy-MM-dd'), badTour);
const botHistory = await call('GET', `/api/ai/public/history?conversationKey=${botKey}`, null);
check('Lịch sử chatbot chỉ gồm user/assistant', botHistory.status === 200 && botHistory.data.length >= 16
  && botHistory.data.every((m) => m.role === 'user' || m.role === 'assistant'), `${botHistory.data?.length} tin`);
check('conversationKey không phải UUID -> 400', (await call('POST', '/api/ai/public/chat', null, { conversationKey: 'abc', message: 'hi' })).status === 400);
check('Lễ tân xem tri thức AI -> 403', (await call('GET', '/api/ai/knowledge', recep.accessToken)).status === 403);
const newDoc = await call('POST', '/api/ai/knowledge', adminToken, { title: 'Chỗ đậu xe', content: 'Bãi đậu xe miễn phí cho gia đình ở cổng sau, mở 07:00–21:00.', tags: 'đậu xe, gửi xe', isPublic: true, isActive: true });
check('Admin thêm tài liệu tri thức', newDoc.status === 200 && newDoc.data.id > 0);
check('Chatbot dùng tài liệu mới', (await ask('/tool search_knowledge {"query":"gửi xe ở đâu"}')).includes('cổng sau'));
check('Admin xóa tài liệu', (await call('DELETE', `/api/ai/knowledge/${newDoc.data.id}`, adminToken)).status === 204);
let aiStatus = 200;
let aiSent = 0;
while (aiStatus === 200 && aiSent < 25) {
  aiStatus = (await call('POST', '/api/ai/public/chat', null, { conversationKey: botKey, message: `Xin chào ${aiSent}` })).status;
  aiSent++;
}
check('Chatbot công khai quá 20 tin/phút/IP -> 429', aiStatus === 429, `dừng ở tin thứ ${aiSent}`);

console.log(failures === 0 ? '\nTẤT CẢ PASS' : `\n${failures} FAIL`);
process.exit(failures === 0 ? 0 : 1);
