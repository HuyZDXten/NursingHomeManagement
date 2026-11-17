import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, DatePicker, Empty, Input, Segmented, Space, Typography } from 'antd';
import { CheckOutlined, CloseOutlined, RobotOutlined, SyncOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { aiInsightsApi } from '../../api/aiInsights';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import type { AiInsight, InsightStatus } from '../../types/care';

export default function AiDigestReview() {
  const { message } = App.useApp();
  const [date, setDate] = useState<Dayjs>(() => dayjs().subtract(1, 'day'));
  const [statusFilter, setStatusFilter] = useState<InsightStatus | 'All'>('Draft');
  const [items, setItems] = useState<AiInsight[]>([]);
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const list = await aiInsightsApi.list(
        date.format('YYYY-MM-DD'),
        'DailyDigest',
        statusFilter === 'All' ? undefined : statusFilter,
      );
      setItems(list);
      setDrafts((prev) => {
        const next: Record<number, string> = { ...prev };
        for (const it of list) {
          if (!(it.id in next)) next[it.id] = it.content;
        }
        return next;
      });
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [date, statusFilter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleGenerate = async () => {
    setGenerating(true);
    try {
      const res = await aiInsightsApi.generate('DailyDigest', date.format('YYYY-MM-DD'));
      message.success(
        res.createdCount > 0
          ? `Đã tạo ${res.createdCount} bản nháp nhật ký AI.`
          : 'Không có thêm cư dân nào đủ điều kiện hoặc đã có bản ghi trong ngày này.',
      );
      setStatusFilter('Draft');
      await load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setGenerating(false);
    }
  };

  const handleApprove = async (item: AiInsight) => {
    const content = (drafts[item.id] ?? item.content).trim();
    if (!content) {
      message.warning('Nội dung nhật ký không được để trống.');
      return;
    }
    setBusyId(item.id);
    try {
      await aiInsightsApi.approve(item.id, content);
      message.success(`Đã duyệt và gửi nhật ký cho gia đình cư dân ${item.residentName}.`);
      await load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setBusyId(null);
    }
  };

  const handleReject = async (item: AiInsight) => {
    setBusyId(item.id);
    try {
      await aiInsightsApi.reject(item.id);
      message.success(`Đã bỏ bản nháp của cư dân ${item.residentName}.`);
      await load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <>
      <PageTitle
        title="Duyệt nhật ký AI"
        extra={
          <Space wrap>
            <DatePicker
              value={date}
              onChange={(d) => d && setDate(d)}
              format="DD/MM/YYYY"
              allowClear={false}
              disabledDate={(d) => d > dayjs().endOf('day')}
            />
            <Segmented
              value={statusFilter}
              onChange={(v) => setStatusFilter(v as InsightStatus | 'All')}
              options={[
                { label: 'Chờ duyệt', value: 'Draft' },
                { label: 'Đã duyệt', value: 'Approved' },
                { label: 'Đã bỏ', value: 'Rejected' },
                { label: 'Tất cả', value: 'All' },
              ]}
            />
            <Button
              type="primary"
              icon={<SyncOutlined spin={generating} />}
              loading={generating}
              onClick={() => void handleGenerate()}
            >
              Tạo nháp ngay
            </Button>
          </Space>
        }
      />

      {loading && items.length === 0 ? (
        <Card loading />
      ) : items.length === 0 ? (
        <Card>
          <Empty description="Không có bản nhật ký nào theo bộ lọc hiện tại." />
        </Card>
      ) : (
        <Space orientation="vertical" size={12} style={{ width: '100%' }}>
          {items.map((item) => {
            const isDraft = item.status === 'Draft';
            const value = drafts[item.id] ?? item.content;
            return (
              <Card
                key={item.id}
                size="small"
                title={
                  <Space>
                    <RobotOutlined />
                    <span>
                      {item.residentName} ({item.residentCode})
                    </span>
                    <StatusTag kind="insightStatus" value={item.status} />
                    <Typography.Text type="secondary">
                      Ngày {dayjs(item.forDate).format('DD/MM/YYYY')}
                    </Typography.Text>
                  </Space>
                }
                extra={
                  isDraft && (
                    <Space>
                      <Button
                        size="small"
                        danger
                        icon={<CloseOutlined />}
                        loading={busyId === item.id}
                        onClick={() => void handleReject(item)}
                      >
                        Bỏ
                      </Button>
                      <Button
                        size="small"
                        type="primary"
                        icon={<CheckOutlined />}
                        loading={busyId === item.id}
                        onClick={() => void handleApprove(item)}
                      >
                        Duyệt & gửi gia đình
                      </Button>
                    </Space>
                  )
                }
              >
                {isDraft ? (
                  <Input.TextArea
                    rows={4}
                    value={value}
                    maxLength={4000}
                    onChange={(e) => setDrafts((prev) => ({ ...prev, [item.id]: e.target.value }))}
                  />
                ) : (
                  <Typography.Paragraph style={{ marginBottom: 8, whiteSpace: 'pre-wrap' }}>
                    {item.content}
                  </Typography.Paragraph>
                )}
                <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                  Do AI soạn, không thay thế đánh giá của nhân viên y tế.
                  {item.reviewedByName && ` · Xử lý bởi ${item.reviewedByName}`}
                </Typography.Text>
              </Card>
            );
          })}
        </Space>
      )}
    </>
  );
}
