import { apiClient } from './client';
import type { AdherenceReport, OccupancyReport, RevenueReport } from '../types/operations';

type Params = Record<string, string | number | undefined>;

/** Tải file (Excel) qua apiClient để có JWT, rồi lưu bằng link tạm. */
export async function downloadFile(url: string, fileName: string, params?: Params): Promise<void> {
  const { data } = await apiClient.get<Blob>(url, { params, responseType: 'blob' });
  const href = URL.createObjectURL(data);
  const link = document.createElement('a');
  link.href = href;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
}

/** S3-V3 — UC-122, 123, 124 (UC-125 sự cố sau S3-H1). */
export const reportsApi = {
  revenue: (year: number) => apiClient.get<RevenueReport>('/api/reports/revenue', { params: { year } }).then((r) => r.data),
  exportRevenue: (year: number) => downloadFile('/api/reports/revenue/export', `doanh-thu-${year}.xlsx`, { year }),

  occupancy: (year: number) => apiClient.get<OccupancyReport>('/api/reports/occupancy', { params: { year } }).then((r) => r.data),
  exportOccupancy: (year: number) => downloadFile('/api/reports/occupancy/export', `cong-suat-giuong-${year}.xlsx`, { year }),

  adherence: (from: string, to: string) =>
    apiClient.get<AdherenceReport>('/api/reports/medication-adherence', { params: { from, to } }).then((r) => r.data),
  exportAdherence: (from: string, to: string) =>
    downloadFile('/api/reports/medication-adherence/export', `tuan-thu-thuoc-${from.replaceAll('-', '')}-${to.replaceAll('-', '')}.xlsx`, { from, to }),
};
