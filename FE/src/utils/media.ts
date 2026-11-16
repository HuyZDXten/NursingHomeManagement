import { API_BASE_URL } from '../api/client';

/**
 * BL-V1: ảnh lưu local trả đường dẫn "/uploads/..." trên API (DB không ghi cứng địa chỉ máy chủ);
 * ảnh Cloudinary là URL tuyệt đối. Hàm này cho ra URL dùng được trong <img src>.
 */
export function mediaUrl(url?: string | null): string | undefined {
  if (!url) return undefined;
  return url.startsWith('/') ? `${API_BASE_URL.replace(/\/$/, '')}${url}` : url;
}

/** Kiểm tra trước khi gửi (API vẫn kiểm chữ ký file): JPG/PNG/WebP, tối đa 5 MB. */
export function checkImageFile(file: File): string | null {
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) return 'Chỉ nhận ảnh JPG, PNG hoặc WebP.';
  if (file.size > 5 * 1024 * 1024) return 'Ảnh tối đa 5 MB.';
  return null;
}
