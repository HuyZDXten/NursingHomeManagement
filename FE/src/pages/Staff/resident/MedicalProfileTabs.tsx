import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, DatePicker, Descriptions, Form, Input, Modal, Popconfirm, Select, Space, Table, Typography } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../../api/client';
import { medicalProfileApi, type SaveAllergyForm, type SaveConditionForm, type SaveDietPlanForm } from '../../../api/medicalProfile';
import { useAuth } from '../../../auth/AuthContext';
import { CARE_PLANNERS, hasRole } from '../../../auth/roles';
import { StatusTag } from '../../../components/Common';
import { dateOnly, dayjs } from '../../../utils/format';
import type { Allergy, DietPlan, MedicalCondition, MedicalProfile } from '../../../types/care';

const EDITOR_ROLES = [...CARE_PLANNERS, 'Doctor'] as const;

// ======================= S3-H2 (UC-66, UC-71): Bệnh nền & dị ứng =======================
export function MedicalConditionsTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canEdit = active && hasRole(user?.role, [...EDITOR_ROLES]);

  const [profile, setProfile] = useState<MedicalProfile>();
  const [condModal, setCondModal] = useState<MedicalCondition | 'new' | null>(null);
  const [allergyModal, setAllergyModal] = useState(false);
  const [condForm] = Form.useForm();
  const [allergyForm] = Form.useForm<SaveAllergyForm>();

  const load = useCallback(() => {
    medicalProfileApi.get(residentId).then(setProfile).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const saveCondition = async () => {
    const v = await condForm.validateFields();
    const body: SaveConditionForm = {
      name: v.name,
      diagnosedOn: v.diagnosedOn ? v.diagnosedOn.format('YYYY-MM-DD') : null,
      status: v.status,
      notes: v.notes,
    };
    try {
      if (condModal === 'new') {
        await medicalProfileApi.addCondition(residentId, body);
      } else if (condModal) {
        await medicalProfileApi.updateCondition(residentId, condModal.id, body);
      }
      message.success('Đã lưu thông tin bệnh nền.');
      setCondModal(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const saveAllergy = async () => {
    const v = await allergyForm.validateFields();
    try {
      await medicalProfileApi.addAllergy(residentId, v);
      message.success('Đã thêm dị ứng.');
      setAllergyModal(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  return (
    <>
      <Space style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Bệnh nền</Typography.Title>
        {canEdit && (
          <Button
            icon={<PlusOutlined />}
            onClick={() => {
              condForm.resetFields();
              condForm.setFieldsValue({ status: 'Active' });
              setCondModal('new');
            }}
          >
            Thêm bệnh nền
          </Button>
        )}
      </Space>

      <Table
        rowKey="id"
        size="small"
        dataSource={profile?.conditions ?? []}
        pagination={false}
        columns={[
          { title: 'Tên bệnh', dataIndex: 'name' },
          { title: 'Ngày chẩn đoán', dataIndex: 'diagnosedOn', render: (d?: string) => (d ? dateOnly(d) : '—') },
          { title: 'Trạng thái', dataIndex: 'status', render: (v: string) => <StatusTag kind="conditionStatus" value={v} /> },
          { title: 'Ghi chú', dataIndex: 'notes' },
          { title: 'Người ghi', dataIndex: 'recordedByName' },
          ...(canEdit
            ? [
                {
                  title: '',
                  key: 'a',
                  render: (_: unknown, c: MedicalCondition) => (
                    <Space>
                      <Button
                        size="small"
                        onClick={() => {
                          condForm.setFieldsValue({
                            name: c.name,
                            diagnosedOn: c.diagnosedOn ? dayjs(c.diagnosedOn) : null,
                            status: c.status,
                            notes: c.notes,
                          });
                          setCondModal(c);
                        }}
                      >
                        Sửa
                      </Button>
                      <Popconfirm
                        title="Xóa bệnh nền này?"
                        onConfirm={async () => {
                          await medicalProfileApi.deleteCondition(residentId, c.id);
                          void load();
                        }}
                      >
                        <Button size="small" danger>Xóa</Button>
                      </Popconfirm>
                    </Space>
                  ),
                },
              ]
            : []),
        ]}
      />

      <Space style={{ marginTop: 24, marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={5} style={{ margin: 0 }}>Dị ứng</Typography.Title>
        {canEdit && (
          <Button
            icon={<PlusOutlined />}
            danger
            onClick={() => {
              allergyForm.resetFields();
              allergyForm.setFieldsValue({ type: 'Drug', severity: 'Moderate' });
              setAllergyModal(true);
            }}
          >
            Thêm dị ứng
          </Button>
        )}
      </Space>

      <Table
        rowKey="id"
        size="small"
        dataSource={profile?.allergies ?? []}
        pagination={false}
        columns={[
          { title: 'Tác nhân', dataIndex: 'allergen' },
          { title: 'Loại', dataIndex: 'type', render: (v: string) => <StatusTag kind="allergyType" value={v} /> },
          { title: 'Mức độ', dataIndex: 'severity', render: (v: string) => <StatusTag kind="allergySeverity" value={v} /> },
          { title: 'Phản ứng', dataIndex: 'reaction' },
          { title: 'Người ghi', dataIndex: 'recordedByName' },
          ...(canEdit
            ? [
                {
                  title: '',
                  key: 'a',
                  render: (_: unknown, a: Allergy) => (
                    <Popconfirm
                      title="Xóa thông tin dị ứng này?"
                      onConfirm={async () => {
                        await medicalProfileApi.deleteAllergy(residentId, a.id);
                        void load();
                      }}
                    >
                      <Button size="small" danger>Xóa</Button>
                    </Popconfirm>
                  ),
                },
              ]
            : []),
        ]}
      />

      <Modal
        open={condModal !== null}
        title={condModal === 'new' ? 'Thêm bệnh nền' : 'Cập nhật bệnh nền'}
        onOk={saveCondition}
        onCancel={() => setCondModal(null)}
        okText="Lưu"
        destroyOnHidden
      >
        <Form form={condForm} layout="vertical">
          <Form.Item name="name" label="Tên bệnh" rules={[{ required: true, message: 'Nhập tên bệnh' }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Space wrap>
            <Form.Item name="diagnosedOn" label="Ngày chẩn đoán">
              <DatePicker format="DD/MM/YYYY" />
            </Form.Item>
            <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
              <Select
                style={{ width: 170 }}
                options={[
                  { value: 'Active', label: 'Đang điều trị' },
                  { value: 'Resolved', label: 'Đã ổn / khỏi' },
                ]}
              />
            </Form.Item>
          </Space>
          <Form.Item name="notes" label="Ghi chú">
            <Input.TextArea rows={2} maxLength={1000} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={allergyModal}
        title="Thêm dị ứng"
        onOk={saveAllergy}
        onCancel={() => setAllergyModal(false)}
        okText="Lưu"
        destroyOnHidden
      >
        <Form form={allergyForm} layout="vertical">
          <Form.Item name="allergen" label="Tác nhân dị ứng" rules={[{ required: true, message: 'Nhập tên tác nhân' }]}>
            <Input maxLength={150} placeholder="Ví dụ: Penicillin, Tôm, Phấn hoa..." />
          </Form.Item>
          <Space wrap>
            <Form.Item name="type" label="Loại" rules={[{ required: true }]}>
              <Select
                style={{ width: 150 }}
                options={[
                  { value: 'Drug', label: 'Thuốc' },
                  { value: 'Food', label: 'Thực phẩm' },
                  { value: 'Other', label: 'Khác' },
                ]}
              />
            </Form.Item>
            <Form.Item name="severity" label="Mức độ" rules={[{ required: true }]}>
              <Select
                style={{ width: 150 }}
                options={[
                  { value: 'Mild', label: 'Nhẹ' },
                  { value: 'Moderate', label: 'Trung bình' },
                  { value: 'Severe', label: 'Nặng' },
                ]}
              />
            </Form.Item>
          </Space>
          <Form.Item name="reaction" label="Phản ứng khi gặp">
            <Input.TextArea rows={2} maxLength={500} placeholder="Phát ban, khó thở, nổi mề đay..." />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}

// ======================= S3-H2 (UC-82): Chế độ ăn =======================
export function DietPlansTab({ residentId, active }: { residentId: number; active: boolean }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canEdit = active && hasRole(user?.role, [...EDITOR_ROLES]);

  const [profile, setProfile] = useState<MedicalProfile>();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();

  const load = useCallback(() => {
    medicalProfileApi.get(residentId).then(setProfile).catch(() => undefined);
  }, [residentId]);

  useEffect(() => {
    void load();
  }, [load]);

  const saveDiet = async () => {
    const v = await form.validateFields();
    const body: SaveDietPlanForm = {
      dietType: v.dietType,
      texture: v.texture,
      restrictions: v.restrictions,
      startDate: v.startDate ? v.startDate.format('YYYY-MM-DD') : undefined,
    };
    try {
      await medicalProfileApi.setDietPlan(residentId, body);
      message.success('Đã cập nhật chế độ ăn mới.');
      setOpen(false);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const current = profile?.activeDiet;

  return (
    <>
      {canEdit && (
        <Button
          type="primary"
          icon={<PlusOutlined />}
          style={{ marginBottom: 12 }}
          onClick={() => {
            form.resetFields();
            form.setFieldsValue({
              dietType: current?.dietType ?? 'Normal',
              texture: current?.texture ?? 'Normal',
              startDate: dayjs(),
            });
            setOpen(true);
          }}
        >
          Đặt chế độ ăn mới
        </Button>
      )}

      <Card size="small" title="Chế độ ăn đang áp dụng" style={{ marginBottom: 16 }}>
        {current ? (
          <Descriptions size="small" column={{ xs: 1, md: 3 }}>
            <Descriptions.Item label="Loại chế độ">
              <StatusTag kind="dietType" value={current.dietType} />
            </Descriptions.Item>
            <Descriptions.Item label="Độ mềm / kết cấu">
              <StatusTag kind="texture" value={current.texture} />
            </Descriptions.Item>
            <Descriptions.Item label="Áp dụng từ">{dateOnly(current.startDate)}</Descriptions.Item>
            <Descriptions.Item label="Hạn chế / ghi chú" span={2}>{current.restrictions || '—'}</Descriptions.Item>
            <Descriptions.Item label="Người chỉ định">{current.prescribedByName}</Descriptions.Item>
          </Descriptions>
        ) : (
          <Typography.Text type="secondary">Chưa thiết lập chế độ ăn riêng.</Typography.Text>
        )}
      </Card>

      <Typography.Title level={5}>Lịch sử chế độ ăn</Typography.Title>
      <Table
        rowKey="id"
        size="small"
        dataSource={profile?.dietHistory ?? []}
        pagination={{ pageSize: 8 }}
        columns={[
          { title: 'Loại', dataIndex: 'dietType', render: (v: string) => <StatusTag kind="dietType" value={v} /> },
          { title: 'Kết cấu', dataIndex: 'texture', render: (v: string) => <StatusTag kind="texture" value={v} /> },
          {
            title: 'Hiệu lực',
            render: (_: unknown, d: DietPlan) =>
              `${dateOnly(d.startDate)} – ${d.endDate ? dateOnly(d.endDate) : 'Hiện tại'}`,
          },
          { title: 'Hạn chế', dataIndex: 'restrictions' },
          { title: 'Người đặt', dataIndex: 'prescribedByName' },
        ]}
      />

      <Modal
        open={open}
        title="Đặt chế độ ăn mới"
        onOk={saveDiet}
        onCancel={() => setOpen(false)}
        okText="Lưu chế độ mới"
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">
          Nếu cư dân đang có chế độ ăn hiện hành, hệ thống sẽ tự kết thúc chế độ cũ vào ngày trước khi áp dụng chế độ mới.
        </Typography.Paragraph>
        <Form form={form} layout="vertical">
          <Space wrap>
            <Form.Item name="dietType" label="Loại chế độ" rules={[{ required: true }]}>
              <Select
                style={{ width: 180 }}
                options={[
                  { value: 'Normal', label: 'Bình thường' },
                  { value: 'Diabetic', label: 'Tiểu đường' },
                  { value: 'LowSalt', label: 'Ít muối' },
                  { value: 'LowFat', label: 'Ít béo' },
                  { value: 'Renal', label: 'Bệnh thận' },
                  { value: 'Vegetarian', label: 'Chay' },
                  { value: 'Other', label: 'Khác' },
                ]}
              />
            </Form.Item>
            <Form.Item name="texture" label="Kết cấu thức ăn" rules={[{ required: true }]}>
              <Select
                style={{ width: 180 }}
                options={[
                  { value: 'Normal', label: 'Cơm thường' },
                  { value: 'Soft', label: 'Mềm / cháo' },
                  { value: 'Minced', label: 'Băm nhỏ' },
                  { value: 'Pureed', label: 'Xay nhuyễn' },
                  { value: 'Liquid', label: 'Lỏng / súp' },
                ]}
              />
            </Form.Item>
          </Space>
          <Form.Item name="startDate" label="Ngày bắt đầu" rules={[{ required: true }]}>
            <DatePicker format="DD/MM/YYYY" allowClear={false} />
          </Form.Item>
          <Form.Item name="restrictions" label="Lưu ý / hạn chế cụ thể">
            <Input.TextArea rows={2} maxLength={1000} placeholder="Không ăn đồ ngọt, tránh thức ăn cứng..." />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
