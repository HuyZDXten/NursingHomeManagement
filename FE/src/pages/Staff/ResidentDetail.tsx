import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { App, Button, Card, Descriptions, Input, Modal, Space, Tabs, Tag } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { residentRecordsApi } from '../../api/residentRecords';
import { residentsApi } from '../../api/residents';
import { useAuth } from '../../auth/AuthContext';
import { BILLING_STAFF, CLINICAL_STAFF, FINANCE_STAFF, FRONT_DESK, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import { PhotoPicker } from '../../components/PhotoPicker';
import { dateOnly, dateTime } from '../../utils/format';
import type { ResidentDetail as Resident, Role } from '../../types';
import type { ResidentLeave } from '../../types/operations';
import { DepositsPanel, LeavesPanel, ReadmitModal, RecordsTab } from '../Operations/ResidentRecords';
import {
  AssessmentsTab, BedTab, BillingTab, CarePlansTab, CareTab, ContactsTab, DailyLogsTab, DietPlansTab,
  DoctorVisitsTab, FamilyTab, IncidentsTab, MedicalConditionsTab, MedicationTab, VitalsTab,
} from './resident/Tabs';
import { AllergyBadge } from '../Care/AllergyBadge';
import { ResidentFormModal } from './resident/ResidentFormModal';

/** BL-V2: ai ghi nhận tạm vắng / trở về (policy LeaveRecorders). */
const LEAVE_RECORDERS: Role[] = ['Admin', 'Receptionist', 'Nurse'];

/** UC-26: hồ sơ 360°. Mỗi tab chỉ hiện với role được phép; API vẫn kiểm tra quyền theo cư dân. */
export default function ResidentDetail() {
  const id = Number(useParams().id);
  const { message } = App.useApp();
  const { user } = useAuth();
  const [resident, setResident] = useState<Resident>();
  const [editing, setEditing] = useState(false);
  const [readmitting, setReadmitting] = useState(false);
  const [onLeave, setOnLeave] = useState<ResidentLeave>();

  const load = useCallback(() => residentsApi.get(id).then(setResident).catch((e) => message.error(extractErrorMessage(e))), [id, message]);
  const loadLeave = useCallback(() => residentRecordsApi.leaves(id).then((rows) => setOnLeave(rows.find((l) => l.isOpen))).catch(() => undefined), [id]);
  useEffect(() => {
    void load();
    void loadLeave();
  }, [load, loadLeave]);

  const frontDesk = hasRole(user?.role, FRONT_DESK);
  const active = resident?.status === 'Active';

  const discharge = () => {
    let reason = '';
    Modal.confirm({
      title: `Ngừng lưu trú cho ${resident?.fullName}?`,
      content: (
        <>
          <p>Hệ thống sẽ trả giường, dừng lịch thuốc và hủy các công việc còn mở. Hồ sơ được giữ lại.</p>
          <Input.TextArea placeholder="Lý do (bắt buộc)" onChange={(e) => { reason = e.target.value; }} />
        </>
      ),
      okText: 'Ngừng lưu trú',
      okButtonProps: { danger: true },
      onOk: async () => {
        if (!reason.trim()) {
          message.warning('Nhập lý do ngừng lưu trú.');
          throw new Error('missing reason');
        }
        try {
          await residentsApi.discharge(id, reason);
          message.success('Đã ngừng lưu trú.');
          void load();
        } catch (e) {
          message.error(extractErrorMessage(e));
          throw e;
        }
      },
    });
  };

  if (!resident) return <Card loading />;

  const tabs = [
    { key: 'contacts', label: 'Người liên hệ', children: <ContactsTab residentId={id} canEdit={frontDesk} /> },
    ...(frontDesk ? [
      { key: 'bed', label: 'Giường', children: <BedTab resident={resident} onChanged={load} /> },
      { key: 'family', label: 'Tài khoản gia đình', children: <FamilyTab residentId={id} active={active} /> },
      { key: 'records', label: 'Hồ sơ lưu trú', children: <RecordsTab resident={resident} /> },
    ] : []),
    {
      key: 'leaves', label: 'Tạm vắng',
      children: <LeavesPanel residentId={id} active={active} canRecord={hasRole(user?.role, LEAVE_RECORDERS)} onChanged={loadLeave} />,
    },
    ...(hasRole(user?.role, CLINICAL_STAFF) ? [
      { key: 'care', label: 'Chăm sóc', children: <CareTab residentId={id} /> },
      { key: 'care-plans', label: 'Kế hoạch chăm sóc', children: <CarePlansTab residentId={id} active={active} /> },
      { key: 'daily-logs', label: 'ADL & bữa ăn', children: <DailyLogsTab residentId={id} active={active} /> },
      { key: 'incidents', label: 'Sự cố', children: <IncidentsTab residentId={id} active={active} /> },
      { key: 'medical', label: 'Bệnh nền & dị ứng', children: <MedicalConditionsTab residentId={id} active={active} /> },
      { key: 'diet', label: 'Chế độ ăn', children: <DietPlansTab residentId={id} active={active} /> },
      { key: 'assessments', label: 'Đánh giá', children: <AssessmentsTab residentId={id} active={active} /> },
      { key: 'doctor-visits', label: 'Khám bác sĩ', children: <DoctorVisitsTab residentId={id} active={active} /> },
      { key: 'vitals', label: 'Sinh hiệu', children: <VitalsTab residentId={id} canRecord={active} /> },
      { key: 'medication', label: 'Thuốc', children: <MedicationTab residentId={id} active={active} /> },
    ] : []),
    ...(hasRole(user?.role, BILLING_STAFF) ? [
      {
        key: 'billing', label: 'Gói & hóa đơn',
        children: <><DepositsPanel residentId={id} canCollect={hasRole(user?.role, FINANCE_STAFF)} /><BillingTab residentId={id} active={active} /></>,
      },
    ] : []),
  ];

  return (
    <>
      <PageTitle
        title={
          <>
            {resident.fullName} <StatusTag kind="resident" value={resident.status} />
            {onLeave && <Tag color={onLeave.overdue ? 'red' : 'orange'}>Tạm vắng</Tag>}
            {hasRole(user?.role, CLINICAL_STAFF) && <AllergyBadge residentId={id} />}
          </>
        }
        extra={frontDesk && (
          <Space>
            <Button onClick={() => setEditing(true)}>Sửa hồ sơ</Button>
            {active && <Button danger onClick={discharge}>Ngừng lưu trú</Button>}
            {!active && <Button type="primary" onClick={() => setReadmitting(true)}>Tái nhập viện</Button>}
          </Space>
        )}
      />
      <ReadmitModal resident={resident} open={readmitting} onClose={() => setReadmitting(false)}
        onDone={(r) => { setReadmitting(false); setResident(r); }} />
      <Card style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', alignItems: 'flex-start' }}>
        <PhotoPicker
          alt={resident.fullName}
          url={resident.photoUrl}
          onUpload={frontDesk ? async (file) => setResident(await residentsApi.uploadPhoto(id, file)) : undefined}
          onRemove={frontDesk ? async () => setResident(await residentsApi.removePhoto(id)) : undefined}
        />
        <Descriptions column={{ xs: 1, md: 3 }} size="small" style={{ flex: 1, minWidth: 260 }}>
          <Descriptions.Item label="Mã">{resident.code}</Descriptions.Item>
          <Descriptions.Item label="Ngày sinh">{dateOnly(resident.dateOfBirth)} ({resident.age} tuổi)</Descriptions.Item>
          <Descriptions.Item label="Giới tính">{resident.gender}</Descriptions.Item>
          <Descriptions.Item label="Giường">{resident.bedLabel ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Nhập viện">{dateTime(resident.admittedAt)}</Descriptions.Item>
          <Descriptions.Item label="Ngừng lưu trú">{dateTime(resident.dischargedAt) || '—'}</Descriptions.Item>
          <Descriptions.Item label="Ghi chú" span={3}>{resident.notes}</Descriptions.Item>
        </Descriptions>
        </div>
      </Card>
      <Card><Tabs items={tabs} /></Card>
      <ResidentFormModal open={editing} resident={resident} onClose={() => setEditing(false)} onSaved={(r) => { setEditing(false); setResident(r); }} />
    </>
  );
}
