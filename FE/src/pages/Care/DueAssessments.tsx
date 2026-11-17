import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, App, Button, Table } from 'antd';
import { clinicalApi } from '../../api/clinical';
import { extractErrorMessage } from '../../api/client';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly, dateTime } from '../../utils/format';
import type { DueAssessment } from '../../types/care';

/** S3-H5 (UC-64): danh sách cư dân đến hạn hoặc chưa từng làm đánh giá sàng lọc định kỳ. */
export default function DueAssessments() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [rows, setRows] = useState<DueAssessment[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    clinicalApi
      .dueAssessments()
      .then(setRows)
      .catch((e) => message.error(extractErrorMessage(e)))
      .finally(() => setLoading(false));
  }, [message]);

  return (
    <>
      <PageTitle title="Đánh giá định kỳ đến hạn" />
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title="Công cụ sàng lọc, không thay thế chẩn đoán."
      />
      <Table
        scroll={{ x: 'max-content' }}
        rowKey={(r) => `${r.residentId}-${r.type}`}
        loading={loading}
        dataSource={rows}
        pagination={{ pageSize: 15 }}
        columns={[
          { title: 'Mã', dataIndex: 'residentCode', width: 100 },
          { title: 'Cư dân', dataIndex: 'residentName' },
          { title: 'Thang đánh giá', dataIndex: 'type', render: (v: string) => <StatusTag kind="assessmentType" value={v} /> },
          { title: 'Đánh giá gần nhất', dataIndex: 'lastAssessedAt', render: (d?: string) => (d ? dateTime(d) : 'Chưa từng đánh giá') },
          {
            title: 'Mức nguy cơ gần nhất',
            dataIndex: 'lastRiskLevel',
            render: (v?: string) => (v ? <StatusTag kind="riskLevel" value={v} /> : '—'),
          },
          { title: 'Hạn đánh giá lại', dataIndex: 'nextDueOn', render: (d?: string) => (d ? dateOnly(d) : 'Cần làm ngay') },
          {
            title: '',
            key: 'action',
            render: (_, r) => (
              <Button size="small" type="primary" onClick={() => navigate(`/staff/residents/${r.residentId}`)}>
                Mở hồ sơ
              </Button>
            ),
          },
        ]}
      />
    </>
  );
}
