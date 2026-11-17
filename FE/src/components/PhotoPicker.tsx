import { useState } from 'react';
import { App, Avatar, Button, Popconfirm, Space, Upload } from 'antd';
import { DeleteOutlined, UploadOutlined, UserOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../api/client';
import { checkImageFile, mediaUrl } from '../utils/media';

interface PhotoPickerProps {
  url?: string | null;
  /** Không truyền = chỉ xem. */
  onUpload?: (file: File) => Promise<unknown>;
  onRemove?: () => Promise<unknown>;
  size?: number;
  alt: string;
}

/** BL-V1 (UC-07, UC-24): ảnh tròn + nút đổi/xóa ảnh. Kiểm loại và cỡ file trước khi gửi; API kiểm lại chữ ký file. */
export function PhotoPicker({ url, onUpload, onRemove, size = 96, alt }: PhotoPickerProps) {
  const { message } = App.useApp();
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await action();
      message.success(done);
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Space orientation="vertical" align="center">
      <Avatar size={size} src={mediaUrl(url)} icon={<UserOutlined />} alt={alt} />
      {onUpload && (
        <Space size="small">
          <Upload
            accept="image/jpeg,image/png,image/webp"
            showUploadList={false}
            beforeUpload={(file) => {
              const problem = checkImageFile(file);
              if (problem) message.error(problem);
              else void run(() => onUpload(file), 'Đã cập nhật ảnh.');
              return false;
            }}
          >
            <Button size="small" icon={<UploadOutlined />} loading={busy}>{url ? 'Đổi ảnh' : 'Tải ảnh'}</Button>
          </Upload>
          {url && onRemove && (
            <Popconfirm title="Xóa ảnh?" onConfirm={() => run(onRemove, 'Đã xóa ảnh.')}>
              <Button size="small" danger icon={<DeleteOutlined />} disabled={busy} />
            </Popconfirm>
          )}
        </Space>
      )}
    </Space>
  );
}
