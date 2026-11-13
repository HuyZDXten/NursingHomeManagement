import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Empty } from 'antd';
import { dateTime } from '../utils/format';
import type { VitalSign } from '../types';

/** UC-63: biểu đồ sinh hiệu theo thời gian (huyết áp, mạch; nhiệt độ ở trục phải). */
export function VitalsChart({ vitals }: { vitals: VitalSign[] }) {
  if (vitals.length === 0) return <Empty description="Chưa có số liệu" />;
  const data = [...vitals].reverse().map((v) => ({
    at: dateTime(v.recordedAt).slice(0, 11),
    'Tâm thu': v.systolic,
    'Tâm trương': v.diastolic,
    Mạch: v.heartRate,
    'Nhiệt độ': v.temperature,
  }));
  return (
    <div style={{ height: 280 }}>
      <ResponsiveContainer>
        <LineChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="at" />
          <YAxis yAxisId="left" width={40} />
          <YAxis yAxisId="right" orientation="right" domain={[35, 41]} width={40} />
          <Tooltip />
          <Legend />
          <Line yAxisId="left" type="monotone" dataKey="Tâm thu" stroke="#cf1322" dot />
          <Line yAxisId="left" type="monotone" dataKey="Tâm trương" stroke="#fa8c16" dot />
          <Line yAxisId="left" type="monotone" dataKey="Mạch" stroke="#1677ff" dot />
          <Line yAxisId="right" type="monotone" dataKey="Nhiệt độ" stroke="#2f7d6d" strokeDasharray="4 4" dot />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
