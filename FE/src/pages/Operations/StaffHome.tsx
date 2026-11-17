import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Card, Col, Empty, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import { ClockCircleOutlined, LoginOutlined, LogoutOutlined } from '@ant-design/icons';
import { aiApi } from '../../api/ai';
import { extractErrorMessage } from '../../api/client';
import { staffHrApi } from '../../api/staffHr';
import { useAuth } from '../../auth/AuthContext';
import { ChatWidget } from '../../components/ChatWidget';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dateTime, timeOf } from '../../utils/format';
import type { MyShift, StaffHome as Home } from '../../types/operations';

const shiftText = (shift: MyShift) => `${shift.shiftName} ngày ${dateOnly(shift.workDate)} (${timeOf(shift.startsAt)}–${timeOf(shift.endsAt)})`;

/**
 * BL-V4, UC-127: trang chủ cá nhân (điều dưỡng, hộ lý, bác sĩ): chấm công vào/ra ca, việc hôm nay, ca kế tiếp,
 * bàn giao và tin nhắn chưa đọc, chứng chỉ sắp hết hạn, trợ lý công việc (AI-05).
 */
export default function StaffHome() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const [home, setHome] = useState<Home>();
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => staffHrApi.home().then(setHome).catch((e) => message.error(extractErrorMessage(e))), [message]);
  useEffect(() => {
    void load();
    const timer = setInterval(() => void load(), 60_000); // nút Vào ca bật/tắt theo giờ
    return () => clearInterval(timer);
  }, [load]);

  const check = async (shift: MyShift, kind: 'in' | 'out') => {
    setBusy(true);
    try {
      await (kind === 'in' ? staffHrApi.checkIn(shift.shiftAssignmentId) : staffHrApi.checkOut(shift.shiftAssignmentId));
      message.success(kind === 'in' ? 'Đã vào ca.' : 'Đã ra ca.');
      await load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  if (!home) return <Card loading />;
  const current = home.currentShift;

  return (
    <>
      <PageTitle title={`Xin chào, ${user?.name ?? ''}`} />
      {home.certificates.length > 0 && (
        <Alert type={home.certificates.some((c) => c.state === 'Expired') ? 'error' : 'warning'} showIcon style={{ marginBottom: 16 }}
          title="Chứng chỉ cần gia hạn"
          description={home.certificates.map((c) => `${c.name}: ${c.state === 'Expired' ? `đã hết hạn ngày ${dateOnly(c.expiresOn)}` : `còn ${c.daysLeft} ngày (${dateOnly(c.expiresOn)})`}`).join('; ')} />
      )}
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title={<><ClockCircleOutlined /> Chấm công</>} style={{ height: '100%' }}>
            {current ? (
              <Space orientation="vertical" style={{ width: '100%' }}>
                <Typography.Text strong>{shiftText(current)}</Typography.Text>
                <Space wrap>
                  <StatusTag kind="myShift" value={current.status} />
                  {current.checkInAt && <Tag>Vào ca {timeOf(current.checkInAt)}</Tag>}
                  {current.lateMinutes > 0 && <Tag color="warning">Đi muộn {current.lateMinutes} phút</Tag>}
                </Space>
                <Space>
                  {current.canCheckIn && <Button type="primary" icon={<LoginOutlined />} loading={busy} onClick={() => check(current, 'in')}>Vào ca</Button>}
                  {current.canCheckOut && <Button icon={<LogoutOutlined />} loading={busy} onClick={() => check(current, 'out')}>Ra ca</Button>}
                </Space>
              </Space>
            ) : <Typography.Text type="secondary">Hiện chưa có ca nào để vào/ra ca.</Typography.Text>}
            <div style={{ marginTop: 12 }}>
              {home.nextShift
                ? <Typography.Text>Ca kế tiếp: <b>{shiftText(home.nextShift)}</b></Typography.Text>
                : <Typography.Text type="secondary">Chưa có ca nào sắp tới trong 7 ngày.</Typography.Text>}
              {' '}<Link to="/staff/shifts">Xem lịch ca</Link>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6} lg={3}><Card><Statistic title="Việc còn lại" value={home.tasksOpen} styles={{ content: { color: home.tasksOverdue ? '#d03b3b' : undefined } }} />{home.tasksOverdue > 0 && <Typography.Text type="danger">{home.tasksOverdue} quá hạn</Typography.Text>}</Card></Col>
        <Col xs={12} sm={6} lg={3}><Card><Statistic title="Đã xong" value={home.tasksDone} /></Card></Col>
        <Col xs={12} sm={6} lg={3}>
          <Card><Statistic title={<Link to="/staff/shifts?tab=handover">Bàn giao chưa đọc</Link>} value={home.unreadHandovers} /></Card>
        </Col>
        <Col xs={12} sm={6} lg={3}>
          <Card><Statistic title={<Link to="/staff/messages">Tin nhắn chưa đọc</Link>} value={home.unreadMessages} /></Card>
        </Col>
        <Col span={24}>
          <Card title="Việc hôm nay" extra={<Link to="/staff/care-tasks">Mở danh sách việc</Link>}>
            {home.tasks.length === 0 ? <Empty description="Hôm nay bạn không có việc chăm sóc nào được giao." /> : (
              <Table rowKey="id" size="small" dataSource={home.tasks} pagination={false} scroll={{ x: 600 }} columns={[
                { title: 'Hạn', render: (_, t) => <Typography.Text type={t.overdue ? 'danger' : undefined}>{dateTime(t.dueAt)}</Typography.Text> },
                { title: 'Cư dân', render: (_, t) => <Link to={`/staff/residents/${t.residentId}`}>{t.residentCode} · {t.residentName}</Link> },
                { title: 'Việc', dataIndex: 'title' },
                { title: 'Trạng thái', render: (_, t) => <><StatusTag kind="care" value={t.status} />{t.overdue && <Tag color="error">Quá hạn</Tag>}</> },
              ]} />
            )}
          </Card>
        </Col>
      </Row>
      {/* AI-05: trợ lý công việc, chỉ đọc; không tư vấn thuốc/chẩn đoán */}
      <ChatWidget title="Trợ lý công việc" greeting="Hỏi tôi về việc hôm nay, ca kế tiếp, bàn giao cần đọc hoặc quy trình nội bộ. Ví dụ: Hôm nay tôi có những việc gì?"
        send={aiApi.assistantChat} loadHistory={aiApi.assistantHistory} disclaimer="Trợ lý chỉ tra cứu công việc của bạn và tài liệu nội bộ, không tư vấn thuốc hay chẩn đoán." />
    </>
  );
}
