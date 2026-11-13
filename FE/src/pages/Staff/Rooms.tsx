import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { App, Button, Card, Checkbox, Col, Empty, Form, Input, InputNumber, Modal, Popconfirm, Row, Space, Switch, Tag, Typography } from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { roomsApi, type RoomForm } from '../../api/rooms';
import { useAuth } from '../../auth/AuthContext';
import { FRONT_DESK, hasRole } from '../../auth/roles';
import { PageTitle, StatusTag } from '../../components/Common';
import type { Bed, Room } from '../../types';

/** UC-34, UC-35, UC-40: sơ đồ phòng/giường theo tầng + quản lý phòng, giường. */
export default function Rooms() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const canEdit = hasRole(user?.role, FRONT_DESK);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [roomEditing, setRoomEditing] = useState<Room | 'new' | null>(null);
  const [bedEditing, setBedEditing] = useState<{ room: Room; bed?: Bed } | null>(null);
  const [roomForm] = Form.useForm<RoomForm>();
  const [bedForm] = Form.useForm<{ number: string; maintenance: boolean }>();

  const load = useCallback(() => roomsApi.list().then(setRooms).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
  }, [load]);

  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      void load();
      return true;
    } catch (e) {
      message.error(extractErrorMessage(e));
      return false;
    }
  };

  const floors = [...new Set(rooms.map((r) => r.floor ?? 0))].sort((a, b) => a - b);
  const count = (s: Bed['status']) => rooms.filter((r) => r.isActive).flatMap((r) => r.beds).filter((b) => b.status === s).length;

  return (
    <>
      <PageTitle
        title="Phòng & giường"
        extra={canEdit && <Button type="primary" icon={<PlusOutlined />} onClick={() => { roomForm.resetFields(); roomForm.setFieldsValue({ isActive: true }); setRoomEditing('new'); }}>Thêm phòng</Button>}
      />
      <Space style={{ marginBottom: 16 }}>
        <Tag color="green">Trống: {count('Available')}</Tag>
        <Tag color="blue">Có người: {count('Occupied')}</Tag>
        <Tag color="orange">Bảo trì: {count('Maintenance')}</Tag>
      </Space>
      {rooms.length === 0 && <Empty />}
      {floors.map((floor) => (
        <div key={floor} style={{ marginBottom: 24 }}>
          <Typography.Title level={5}>Tầng {floor}</Typography.Title>
          <Row gutter={[16, 16]}>
            {rooms.filter((r) => (r.floor ?? 0) === floor).map((room) => (
              <Col xs={24} md={12} xl={8} key={room.id}>
                <Card
                  size="small"
                  title={<>Phòng {room.number} {!room.isActive && <Tag>Ngừng sử dụng</Tag>}</>}
                  extra={canEdit && (
                    <Space size={4}>
                      <Button size="small" icon={<PlusOutlined />} onClick={() => { bedForm.resetFields(); bedForm.setFieldsValue({ maintenance: false }); setBedEditing({ room }); }}>Giường</Button>
                      <Button size="small" icon={<EditOutlined />} onClick={() => { roomForm.setFieldsValue(room); setRoomEditing(room); }} />
                      <Popconfirm title="Xóa phòng? (chỉ khi chưa có giường)" onConfirm={() => run(() => roomsApi.remove(room.id))}>
                        <Button size="small" danger icon={<DeleteOutlined />} />
                      </Popconfirm>
                    </Space>
                  )}
                >
                  <Typography.Paragraph type="secondary" style={{ marginTop: -4 }}>{room.description}</Typography.Paragraph>
                  <Row gutter={[8, 8]}>
                    {room.beds.map((bed) => (
                      <Col span={12} key={bed.id}>
                        <div className={`bed-card ${bed.status}`}>
                          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                            <b>Giường {bed.number}</b>
                            {canEdit && bed.status !== 'Occupied' && (
                              <a onClick={() => { bedForm.setFieldsValue({ number: bed.number, maintenance: bed.status === 'Maintenance' }); setBedEditing({ room, bed }); }}>Sửa</a>
                            )}
                          </div>
                          {bed.residentId
                            ? <Link to={`/staff/residents/${bed.residentId}`}>{bed.residentCode} · {bed.residentName}</Link>
                            : <StatusTag kind="bed" value={bed.status} />}
                        </div>
                      </Col>
                    ))}
                  </Row>
                </Card>
              </Col>
            ))}
          </Row>
        </div>
      ))}

      <Modal
        open={roomEditing !== null}
        title={roomEditing === 'new' ? 'Thêm phòng' : 'Sửa phòng'}
        onOk={async () => {
          const v = await roomForm.validateFields();
          const ok = await run(() => (roomEditing === 'new' ? roomsApi.create(v) : roomsApi.update((roomEditing as Room).id, v)));
          if (ok) setRoomEditing(null);
        }}
        onCancel={() => setRoomEditing(null)}
        okText="Lưu"
        destroyOnHidden
      >
        <Form form={roomForm} layout="vertical">
          <Form.Item name="number" label="Số phòng" rules={[{ required: true, max: 30 }]}><Input /></Form.Item>
          <Form.Item name="floor" label="Tầng"><InputNumber min={-5} max={200} /></Form.Item>
          <Form.Item name="description" label="Mô tả"><Input /></Form.Item>
          <Form.Item name="isActive" label="Đang sử dụng" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>

      <Modal
        open={bedEditing !== null}
        title={bedEditing?.bed ? `Sửa giường — phòng ${bedEditing.room.number}` : `Thêm giường — phòng ${bedEditing?.room.number}`}
        onOk={async () => {
          if (!bedEditing) return;
          const v = await bedForm.validateFields();
          const ok = await run(() => (bedEditing.bed
            ? roomsApi.updateBed(bedEditing.room.id, bedEditing.bed.id, v)
            : roomsApi.addBed(bedEditing.room.id, v)));
          if (ok) setBedEditing(null);
        }}
        onCancel={() => setBedEditing(null)}
        okText="Lưu"
        footer={(node) => (
          <>
            {bedEditing?.bed && (
              <Popconfirm title="Xóa giường? (chỉ khi chưa có lịch sử)" onConfirm={async () => { if (await run(() => roomsApi.removeBed(bedEditing.room.id, bedEditing.bed!.id))) setBedEditing(null); }}>
                <Button danger style={{ float: 'left' }}>Xóa giường</Button>
              </Popconfirm>
            )}
            {node}
          </>
        )}
        destroyOnHidden
      >
        <Form form={bedForm} layout="vertical">
          <Form.Item name="number" label="Số giường" rules={[{ required: true, max: 30 }]}><Input /></Form.Item>
          <Form.Item name="maintenance" valuePropName="checked"><Checkbox>Đang bảo trì (không phân được)</Checkbox></Form.Item>
        </Form>
      </Modal>
    </>
  );
}
