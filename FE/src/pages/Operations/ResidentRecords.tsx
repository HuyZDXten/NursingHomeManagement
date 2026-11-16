import { useCallback, useEffect, useState } from 'react';
import {
  Alert, App, Button, Card, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table, Typography, Upload,
} from 'antd';
import { DownloadOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { billingApi } from '../../api/billing';
import { extractErrorMessage } from '../../api/client';
import { residentRecordsApi } from '../../api/residentRecords';
import { roomsApi } from '../../api/rooms';
import { StatusTag } from '../../components/Common';
import { dateOnly, dateTime, dayjs, money } from '../../utils/format';
import type { ResidentDetail, Room, ServicePackage } from '../../types';
import type {
  ContractForm, DepositSummary, DocumentKind, LeaveKind, ResidentContract, ResidentDocument, ResidentLeave, ResidentStay,
} from '../../types/operations';

const DOCUMENT_ACCEPT = 'application/pdf,image/jpeg,image/png';
const MAX_DOCUMENT_BYTES = 10 * 1024 * 1024;
const iso = (value?: dayjs.Dayjs | null) => (value ? value.format('YYYY-MM-DD') : undefined);

function useRunner() {
  const { message } = App.useApp();
  return useCallback(async (action: () => Promise<unknown>, ok?: string) => {
    try {
      await action();
      if (ok) message.success(ok);
      return true;
    } catch (e) {
      message.error(extractErrorMessage(e));
      return false;
    }
  }, [message]);
}

function pickFile(file: File, onPicked: (file: File) => void, warn: (text: string) => void) {
  if (!['application/pdf', 'image/jpeg', 'image/png'].includes(file.type)) warn('Chỉ nhận file PDF, JPG hoặc PNG.');
  else if (file.size > MAX_DOCUMENT_BYTES) warn('File tối đa 10 MB.');
  else onPicked(file);
  return false;
}

// ======================= UC-29 giấy tờ =======================
function DocumentsCard({ residentId }: { residentId: number }) {
  const { message } = App.useApp();
  const run = useRunner();
  const [rows, setRows] = useState<ResidentDocument[]>([]);
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File>();
  const [form] = Form.useForm<{ kind: DocumentKind; title: string }>();
  const load = useCallback(() => residentRecordsApi.documents(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const values = await form.validateFields();
    if (!file) {
      message.warning('Chọn file giấy tờ.');
      return;
    }
    if (await run(() => residentRecordsApi.uploadDocument(residentId, values.kind, values.title, file), 'Đã lưu giấy tờ.')) {
      setOpen(false);
      void load();
    }
  };

  return (
    <Card size="small" title="Giấy tờ" extra={<Button size="small" icon={<PlusOutlined />} onClick={() => { form.resetFields(); setFile(undefined); setOpen(true); }}>Thêm</Button>} style={{ marginBottom: 16 }}>
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} locale={{ emptyText: 'Chưa có giấy tờ' }} columns={[
        { title: 'Loại', render: (_, d) => <StatusTag kind="documentKind" value={d.kind} /> },
        { title: 'Tên', dataIndex: 'title' },
        { title: 'File', render: (_, d) => <>{d.fileName} <small>({Math.max(1, Math.round(d.sizeBytes / 1024))} KB)</small></> },
        { title: 'Người tải', render: (_, d) => <>{d.uploadedByName}<br /><small>{dateTime(d.createdAt)}</small></> },
        {
          title: '', key: 'actions', render: (_, d) => (
            <Space>
              <Button size="small" icon={<DownloadOutlined />} onClick={() => run(() => residentRecordsApi.downloadDocument(residentId, d))} />
              <Popconfirm title="Xóa giấy tờ này?" onConfirm={async () => { if (await run(() => residentRecordsApi.deleteDocument(residentId, d.id), 'Đã xóa.')) void load(); }}>
                <Button size="small" danger>Xóa</Button>
              </Popconfirm>
            </Space>
          ),
        },
      ]} />
      <Modal open={open} title="Thêm giấy tờ" okText="Lưu" onOk={save} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ kind: 'IdCard' }}>
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select options={[
              { value: 'IdCard', label: 'CCCD/CMND' }, { value: 'Insurance', label: 'Thẻ BHYT' },
              { value: 'Medical', label: 'Giấy tờ y tế' }, { value: 'Other', label: 'Khác' },
            ]} />
          </Form.Item>
          <Form.Item name="title" label="Tên giấy tờ" rules={[{ required: true, max: 200 }]}><Input placeholder="vd. CCCD mặt trước" /></Form.Item>
          <Upload accept={DOCUMENT_ACCEPT} maxCount={1} beforeUpload={(f) => pickFile(f, setFile, message.error)} onRemove={() => setFile(undefined)}
            fileList={file ? [{ uid: '1', name: file.name, status: 'done' }] : []}>
            <Button icon={<UploadOutlined />}>Chọn file (PDF, JPG, PNG ≤ 10 MB)</Button>
          </Upload>
        </Form>
      </Modal>
    </Card>
  );
}

// ======================= UC-30 hợp đồng =======================
export function ContractsCard({ residentId, canEdit, active }: { residentId: number; canEdit: boolean; active: boolean }) {
  const { message } = App.useApp();
  const run = useRunner();
  const [rows, setRows] = useState<ResidentContract[]>([]);
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File>();
  const [form] = Form.useForm();
  const load = useCallback(() => residentRecordsApi.contracts(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const v = await form.validateFields();
    const body: ContractForm = {
      number: v.number, signedOn: iso(v.signedOn)!, startDate: iso(v.startDate)!, endDate: iso(v.endDate), monthlyPrice: v.monthlyPrice, notes: v.notes,
    };
    if (await run(() => residentRecordsApi.createContract(residentId, body, file), 'Đã lưu hợp đồng.')) {
      setOpen(false);
      void load();
    }
  };

  const hasActive = rows.some((c) => c.status === 'Active');
  return (
    <Card size="small" title="Hợp đồng lưu trú" style={{ marginBottom: 16 }}
      extra={canEdit && active && !hasActive && (
        <Button size="small" icon={<PlusOutlined />} onClick={() => { form.resetFields(); setFile(undefined); setOpen(true); }}>Thêm hợp đồng</Button>
      )}>
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} locale={{ emptyText: 'Chưa có hợp đồng' }} columns={[
        { title: 'Số', dataIndex: 'number' },
        { title: 'Trạng thái', render: (_, c) => <StatusTag kind="contract" value={c.status} /> },
        { title: 'Hiệu lực', render: (_, c) => `${dateOnly(c.startDate)} – ${c.endDate ? dateOnly(c.endDate) : 'không thời hạn'}` },
        { title: 'Giá tháng', render: (_, c) => (c.monthlyPrice != null ? money(c.monthlyPrice) : '—') },
        { title: 'Ghi chú', dataIndex: 'notes' },
        {
          title: '', key: 'actions', render: (_, c) => (
            <Space>
              {c.hasFile && <Button size="small" icon={<DownloadOutlined />} onClick={() => run(() => residentRecordsApi.downloadContract(residentId, c))}>File</Button>}
              {canEdit && c.status === 'Active' && (
                <Popconfirm title="Kết thúc hợp đồng hôm nay?" onConfirm={async () => { if (await run(() => residentRecordsApi.endContract(residentId, c.id), 'Đã kết thúc hợp đồng.')) void load(); }}>
                  <Button size="small">Kết thúc</Button>
                </Popconfirm>
              )}
            </Space>
          ),
        },
      ]} />
      <Modal open={open} title="Thêm hợp đồng lưu trú" okText="Lưu" onOk={save} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ signedOn: dayjs(), startDate: dayjs() }}>
          <Form.Item name="number" label="Số hợp đồng" rules={[{ required: true, max: 30 }]}><Input placeholder="HD-2026-0002" /></Form.Item>
          <Space wrap>
            <Form.Item name="signedOn" label="Ngày ký" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" /></Form.Item>
            <Form.Item name="startDate" label="Bắt đầu" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" /></Form.Item>
            <Form.Item name="endDate" label="Kết thúc (nếu có)"><DatePicker format="DD/MM/YYYY" /></Form.Item>
          </Space>
          <Form.Item name="monthlyPrice" label="Giá tháng theo hợp đồng (đ)"><InputNumber min={0} step={100000} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="notes" label="Ghi chú" rules={[{ max: 500 }]}><Input.TextArea rows={2} /></Form.Item>
          <Upload accept={DOCUMENT_ACCEPT} maxCount={1} beforeUpload={(f) => pickFile(f, setFile, message.error)} onRemove={() => setFile(undefined)}
            fileList={file ? [{ uid: '1', name: file.name, status: 'done' }] : []}>
            <Button icon={<UploadOutlined />}>Đính kèm bản scan (không bắt buộc)</Button>
          </Upload>
        </Form>
      </Modal>
    </Card>
  );
}

// ======================= UC-33 lịch sử lưu trú =======================
function StaysCard({ residentId }: { residentId: number }) {
  const [rows, setRows] = useState<ResidentStay[]>([]);
  useEffect(() => {
    residentRecordsApi.stays(residentId).then(setRows).catch(() => undefined);
  }, [residentId]);
  if (rows.length === 0) return null;
  return (
    <Card size="small" title="Các lần lưu trú trước">
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} columns={[
        { title: 'Nhập viện', render: (_, s) => dateTime(s.admittedAt) },
        { title: 'Ngừng lưu trú', render: (_, s) => dateTime(s.dischargedAt) },
      ]} />
    </Card>
  );
}

/** Tab "Hồ sơ lưu trú" (lễ tân, Admin): giấy tờ, hợp đồng, các lần lưu trú trước. */
export function RecordsTab({ resident }: { resident: ResidentDetail }) {
  return (
    <>
      <DocumentsCard residentId={resident.id} />
      <ContractsCard residentId={resident.id} canEdit active={resident.status === 'Active'} />
      <StaysCard residentId={resident.id} />
    </>
  );
}

// ======================= UC-31 tạm vắng =======================
export function LeavesPanel({ residentId, active, canRecord, onChanged }: { residentId: number; active: boolean; canRecord: boolean; onChanged?: () => void }) {
  const run = useRunner();
  const [rows, setRows] = useState<ResidentLeave[]>([]);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();
  const load = useCallback(() => residentRecordsApi.leaves(residentId).then(setRows).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  const current = rows.find((l) => l.isOpen);
  const refresh = () => {
    void load();
    onChanged?.();
  };
  const save = async () => {
    const v = await form.validateFields();
    if (await run(() => residentRecordsApi.startLeave(residentId, { kind: v.kind as LeaveKind, expectedReturnOn: iso(v.expectedReturnOn), reason: v.reason }), 'Đã ghi nhận tạm vắng.')) {
      setOpen(false);
      refresh();
    }
  };

  return (
    <>
      {current
        ? (
          <Alert type={current.overdue ? 'error' : 'warning'} showIcon style={{ marginBottom: 12 }}
            title={<>Đang tạm vắng (<StatusTag kind="leaveKind" value={current.kind} />) từ {dateTime(current.startAt)}
              {current.expectedReturnOn && <>, dự kiến về {dateOnly(current.expectedReturnOn)}{current.overdue && ' — đã quá hạn'}</>}</>}
            description={current.reason}
            action={canRecord && (
              <Popconfirm title="Xác nhận cư dân đã trở về?" onConfirm={async () => { if (await run(() => residentRecordsApi.returnFromLeave(residentId, current.id), 'Đã ghi nhận trở về.')) refresh(); }}>
                <Button size="small" type="primary">Đã về</Button>
              </Popconfirm>
            )} />
        )
        : canRecord && active && (
          <Button icon={<PlusOutlined />} style={{ marginBottom: 12 }} onClick={() => { form.resetFields(); setOpen(true); }}>Ghi nhận tạm vắng</Button>
        )}
      <Table rowKey="id" size="small" dataSource={rows} pagination={false} locale={{ emptyText: 'Chưa có lần tạm vắng' }} columns={[
        { title: 'Loại', render: (_, l) => <StatusTag kind="leaveKind" value={l.kind} /> },
        { title: 'Đi', render: (_, l) => dateTime(l.startAt) },
        { title: 'Dự kiến về', render: (_, l) => dateOnly(l.expectedReturnOn) || '—' },
        { title: 'Đã về', render: (_, l) => dateTime(l.returnedAt) || '—' },
        { title: 'Lý do', dataIndex: 'reason' },
        { title: 'Người ghi', dataIndex: 'createdByName' },
      ]} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
        Tạm vắng chỉ đánh dấu cư dân không có mặt; không tự hủy việc chăm sóc, lịch thuốc hay thay đổi hóa đơn.
      </Typography.Paragraph>
      <Modal open={open} title="Ghi nhận tạm vắng" okText="Lưu" onOk={save} onCancel={() => setOpen(false)} destroyOnHidden>
        <Form form={form} layout="vertical" initialValues={{ kind: 'Home' }}>
          <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
            <Select options={[{ value: 'Home', label: 'Về nhà' }, { value: 'Hospital', label: 'Nằm viện' }, { value: 'Other', label: 'Khác' }]} />
          </Form.Item>
          <Form.Item name="expectedReturnOn" label="Dự kiến về"><DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isBefore(dayjs(), 'day')} /></Form.Item>
          <Form.Item name="reason" label="Lý do" rules={[{ required: true, max: 500 }]}><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= UC-99 tiền cọc =======================
export function DepositsPanel({ residentId, canCollect }: { residentId: number; canCollect: boolean }) {
  const run = useRunner();
  const [summary, setSummary] = useState<DepositSummary>();
  const [mode, setMode] = useState<'collect' | number | null>(null);
  const [form] = Form.useForm();
  const load = useCallback(() => residentRecordsApi.deposits(residentId).then(setSummary).catch(() => undefined), [residentId]);
  useEffect(() => {
    void load();
  }, [load]);

  if (!summary) return null;
  const held = summary.deposits.find((d) => d.held);
  const discharged = summary.residentStatus === 'Discharged';
  const refunding = typeof mode === 'number' ? summary.deposits.find((d) => d.id === mode) : undefined;

  const save = async () => {
    const v = await form.validateFields();
    const ok = mode === 'collect'
      ? await run(async () => setSummary(await residentRecordsApi.createDeposit(residentId, v)), 'Đã ghi nhận tiền cọc.')
      : await run(async () => setSummary(await residentRecordsApi.refundDeposit(mode as number, v)), 'Đã hoàn cọc.');
    if (ok) setMode(null);
  };

  return (
    <Card size="small" title="Tiền cọc" style={{ marginBottom: 16 }}
      extra={canCollect && !held && !discharged && <Button size="small" icon={<PlusOutlined />} onClick={() => { form.resetFields(); setMode('collect'); }}>Thu cọc</Button>}>
      <Descriptions size="small" column={{ xs: 1, md: 2 }} style={{ marginBottom: 8 }}>
        <Descriptions.Item label="Đang giữ">{money(summary.heldAmount)}</Descriptions.Item>
        <Descriptions.Item label="Công nợ hiện tại">{money(summary.outstanding)}</Descriptions.Item>
      </Descriptions>
      <Table rowKey="id" size="small" dataSource={summary.deposits} pagination={false} locale={{ emptyText: 'Chưa thu cọc' }} columns={[
        { title: 'Số tiền', render: (_, d) => money(d.amount) },
        { title: 'Hình thức', render: (_, d) => <StatusTag kind="depositMethod" value={d.method} /> },
        { title: 'Thu', render: (_, d) => <>{dateOnly(d.receivedAt)}<br /><small>{d.receivedByName} {d.reference}</small></> },
        { title: 'Hoàn', render: (_, d) => (d.held ? 'Đang giữ' : <>{money(d.refundAmount ?? 0)} ({dateOnly(d.refundedAt)})<br /><small>{d.refundNote}</small></>) },
        {
          title: '', key: 'refund', render: (_, d) => canCollect && d.held && discharged && (
            <Button size="small" onClick={() => { form.setFieldsValue({ amount: Math.max(0, d.amount - summary.outstanding), note: '' }); setMode(d.id); }}>Hoàn cọc</Button>
          ),
        },
      ]} />
      {held && !discharged && <Typography.Text type="secondary">Hoàn cọc sau khi cư dân ngừng lưu trú.</Typography.Text>}
      <Modal open={mode !== null} title={mode === 'collect' ? 'Thu tiền cọc' : 'Hoàn cọc'} okText="Lưu" onOk={save} onCancel={() => setMode(null)} destroyOnHidden>
        {refunding && (
          <Alert type="info" showIcon style={{ marginBottom: 12 }}
            title={`Cọc ${money(refunding.amount)}, công nợ chưa trả ${money(summary.outstanding)}. Gợi ý hoàn ${money(Math.max(0, refunding.amount - summary.outstanding))}.`} />
        )}
        <Form form={form} layout="vertical" initialValues={{ method: 'Cash' }}>
          <Form.Item name="amount" label="Số tiền (đ)" rules={[{ required: true }]}>
            <InputNumber min={mode === 'collect' ? 1000 : 0} max={refunding?.amount} step={100000} style={{ width: '100%' }} />
          </Form.Item>
          {mode === 'collect'
            ? (
              <>
                <Form.Item name="method" label="Hình thức" rules={[{ required: true }]}>
                  <Select options={[{ value: 'Cash', label: 'Tiền mặt' }, { value: 'BankTransfer', label: 'Chuyển khoản' }]} />
                </Form.Item>
                <Form.Item name="reference" label="Số phiếu thu / mã giao dịch" rules={[{ max: 100 }]}><Input /></Form.Item>
              </>
            )
            : <Form.Item name="note" label="Ghi chú (khoản trừ, lý do)" rules={[{ max: 500 }]}><Input.TextArea rows={2} /></Form.Item>}
        </Form>
      </Modal>
    </Card>
  );
}

// ======================= UC-33 tái nhập viện =======================
export function ReadmitModal({ resident, open, onClose, onDone }: { resident: ResidentDetail; open: boolean; onClose: () => void; onDone: (r: ResidentDetail) => void }) {
  const run = useRunner();
  const [rooms, setRooms] = useState<Room[]>([]);
  const [packages, setPackages] = useState<ServicePackage[]>([]);
  const [form] = Form.useForm();
  useEffect(() => {
    if (!open) return;
    roomsApi.list().then(setRooms).catch(() => undefined);
    billingApi.packages(true).then(setPackages).catch(() => undefined);
  }, [open]);

  const freeBeds = rooms.filter((r) => r.isActive).flatMap((r) => r.beds.filter((b) => b.status === 'Available').map((b) => ({
    value: b.id, label: `Phòng ${r.number} - Giường ${b.number}`,
  })));
  const save = async () => {
    const v = await form.validateFields();
    let updated: ResidentDetail | undefined;
    if (await run(async () => {
      updated = await residentRecordsApi.readmit(resident.id, { bedId: v.bedId, servicePackageId: v.servicePackageId, agreedMonthlyPrice: v.agreedMonthlyPrice, startDate: iso(v.startDate)! });
    }, 'Đã tái nhập viện.') && updated) onDone(updated);
  };

  return (
    <Modal open={open} title={`Tái nhập viện: ${resident.fullName}`} okText="Tái nhập viện" onOk={save} onCancel={onClose} destroyOnHidden>
      <Typography.Paragraph type="secondary">
        Lần lưu trú trước ({dateTime(resident.admittedAt)} – {dateTime(resident.dischargedAt)}) được lưu vào lịch sử; gói dịch vụ cũ được đóng ở ngày ngừng lưu trú.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" initialValues={{ startDate: dayjs() }}>
        <Form.Item name="bedId" label="Giường" rules={[{ required: true, message: 'Chọn giường trống' }]}><Select options={freeBeds} placeholder="Chọn giường trống" /></Form.Item>
        <Form.Item name="servicePackageId" label="Gói dịch vụ" rules={[{ required: true, message: 'Chọn gói' }]}>
          <Select options={packages.map((p) => ({ value: p.id, label: `${p.name} — ${money(p.monthlyPrice)}` }))} />
        </Form.Item>
        <Form.Item name="agreedMonthlyPrice" label="Giá thỏa thuận (bỏ trống = giá gói)"><InputNumber min={0} step={100000} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="startDate" label="Bắt đầu tính gói" rules={[{ required: true }]}><DatePicker format="DD/MM/YYYY" /></Form.Item>
      </Form>
    </Modal>
  );
}
