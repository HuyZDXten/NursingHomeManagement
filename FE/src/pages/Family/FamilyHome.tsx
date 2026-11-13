import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, Col, Empty, Row, Tag, Typography } from 'antd';
import { portalApi } from '../../api/portal';
import { PageTitle, StatusTag } from '../../components/Common';
import { dateOnly } from '../../utils/format';
import type { FamilyResident } from '../../types';

/** UC-100: người thân được liên kết với tài khoản này. */
export default function FamilyHome() {
  const navigate = useNavigate();
  const [rows, setRows] = useState<FamilyResident[]>();
  useEffect(() => {
    portalApi.familyResidents().then(setRows).catch(() => setRows([]));
  }, []);

  return (
    <>
      <PageTitle title="Người thân của tôi" />
      {rows?.length === 0 && <Empty description="Chưa có người thân nào được liên kết. Vui lòng liên hệ lễ tân." />}
      <Row gutter={[16, 16]}>
        {rows?.map((r) => (
          <Col xs={24} md={12} lg={8} key={r.residentId}>
            <Card hoverable onClick={() => navigate(`/family/residents/${r.residentId}`)} title={r.fullName} extra={<StatusTag kind="resident" value={r.status} />}>
              <Typography.Paragraph>Mã {r.code} · Sinh {dateOnly(r.dateOfBirth)}</Typography.Paragraph>
              {r.canViewHealth && <Tag color="green">Sức khỏe</Tag>}
              {r.canViewCare && <Tag color="blue">Chăm sóc</Tag>}
              {r.canViewMedication && <Tag color="purple">Thuốc</Tag>}
              {r.canViewBilling && <Tag color="gold">Hóa đơn</Tag>}
            </Card>
          </Col>
        ))}
      </Row>
    </>
  );
}
