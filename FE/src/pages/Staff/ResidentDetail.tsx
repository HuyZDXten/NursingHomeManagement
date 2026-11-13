import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { App, Button, Card, Descriptions, Input, Modal, Space, Tabs } from 'antd';
import { extractErrorMessage } from '../../api/client';
import { residentsApi } from '../../api/residents';
import { useAuth } from '../../auth/AuthContext';
import { BILLING_STAFF, CLINICAL_STAFF, FRONT_DESK, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dateTime } from '../../utils/format';
import type { ResidentDetail as Resident } from '../../types';
import { BedTab, BillingTab, CareTab, ContactsTab, FamilyTab, MedicationTab, VitalsTab } from './resident/Tabs';
import { ResidentFormModal } from './resident/ResidentFormModal';

/** UC-26: hồ sơ 360°. Mỗi tab chỉ hiện với role được phép; API vẫn kiểm tra quyền theo cư dân. */
export default function ResidentDetail() {
  const id = Number(useParams().id);
  const { message } = App.useApp();
  const { user } = useAuth();
  const [resident, setResident] = useState<Resident>();
  const [editing, setEditing] = useState(false);

  const load = useCallback(() => residentsApi.get(id).then(setResident).catch((e) => message.error(extractErrorMessage(e))), [id, message]);
  useEffect(() => {
    void load();
  }, [load]);

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
      { key: 'family', label: 'Tài khoản gia đình', children: <FamilyTab residentId={id} /> },
    ] : []),
    ...(hasRole(user?.role, CLINICAL_STAFF) ? [
      { key: 'care', label: 'Chăm sóc', children: <CareTab residentId={id} /> },
      { key: 'vitals', label: 'Sinh hiệu', children: <VitalsTab residentId={id} canRecord={active} /> },
      { key: 'medication', label: 'Thuốc', children: <MedicationTab residentId={id} active={active} /> },
    ] : []),
    ...(hasRole(user?.role, BILLING_STAFF) ? [
      { key: 'billing', label: 'Gói & hóa đơn', children: <BillingTab residentId={id} active={active} /> },
    ] : []),
  ];

  return (
    <>
      <PageTitle
        title={<>{resident.fullName} <StatusTag kind="resident" value={resident.status} /></>}
        extra={frontDesk && (
          <Space>
            <Button onClick={() => setEditing(true)}>Sửa hồ sơ</Button>
            {active && <Button danger onClick={discharge}>Ngừng lưu trú</Button>}
          </Space>
        )}
      />
      <Card style={{ marginBottom: 16 }}>
        <Descriptions column={{ xs: 1, md: 3 }} size="small">
          <Descriptions.Item label="Mã">{resident.code}</Descriptions.Item>
          <Descriptions.Item label="Ngày sinh">{dateOnly(resident.dateOfBirth)} ({resident.age} tuổi)</Descriptions.Item>
          <Descriptions.Item label="Giới tính">{resident.gender}</Descriptions.Item>
          <Descriptions.Item label="Giường">{resident.bedLabel ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Nhập viện">{dateTime(resident.admittedAt)}</Descriptions.Item>
          <Descriptions.Item label="Ngừng lưu trú">{dateTime(resident.dischargedAt) || '—'}</Descriptions.Item>
          <Descriptions.Item label="Ghi chú" span={3}>{resident.notes}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Card><Tabs items={tabs} /></Card>
      <ResidentFormModal open={editing} resident={resident} onClose={() => setEditing(false)} onSaved={(r) => { setEditing(false); setResident(r); }} />
    </>
  );
}
