import { useEffect, useState } from 'react';
import { Tag } from 'antd';
import { WarningFilled } from '@ant-design/icons';
import { medicalProfileApi } from '../../api/medicalProfile';
import type { Allergy } from '../../types/care';

/** S3-H2: Badge đỏ "Dị ứng: …" đặt cạnh tên cư dân ở trang hồ sơ. */
export function AllergyBadge({ residentId }: { residentId: number }) {
  const [allergies, setAllergies] = useState<Allergy[]>([]);

  useEffect(() => {
    medicalProfileApi
      .get(residentId)
      .then((p) => setAllergies(p.allergies))
      .catch(() => setAllergies([]));
  }, [residentId]);

  if (allergies.length === 0) return null;

  return (
    <Tag color="red" icon={<WarningFilled />} style={{ marginLeft: 8, verticalAlign: 'middle' }}>
      Dị ứng: {allergies.map((a) => a.allergen).join(', ')}
    </Tag>
  );
}
