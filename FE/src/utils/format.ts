import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import timezone from 'dayjs/plugin/timezone';
import 'dayjs/locale/vi';

dayjs.extend(utc);
dayjs.extend(timezone);
dayjs.locale('vi');

/** Cơ sở dùng giờ Việt Nam; API trả thời điểm UTC (có "Z"). */
export const FACILITY_TZ = 'Asia/Ho_Chi_Minh';

export const money = (value?: number | null) => (value == null ? '' : `${value.toLocaleString('vi-VN')} đ`);

/** Thời điểm UTC -> "dd/MM/yyyy HH:mm" giờ VN. */
export const dateTime = (iso?: string | null) => (iso ? dayjs(iso).tz(FACILITY_TZ).format('DD/MM/YYYY HH:mm') : '');

export const timeOf = (iso?: string | null) => (iso ? dayjs(iso).tz(FACILITY_TZ).format('HH:mm') : '');

/** DateOnly "yyyy-MM-dd" -> "dd/MM/yyyy". */
export const dateOnly = (value?: string | null) => (value ? dayjs(value).format('DD/MM/YYYY') : '');

export const monthOf = (value?: string | null) => (value ? dayjs(value).format('MM/YYYY') : '');

/** TimeOnly "HH:mm:ss" -> "HH:mm". */
export const hhmm = (value?: string | null) => (value ? value.slice(0, 5) : '');

export const todayVn = () => dayjs().tz(FACILITY_TZ).format('YYYY-MM-DD');

export { dayjs };
