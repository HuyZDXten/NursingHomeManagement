import { useEffect, useState } from 'react';
import { systemApi } from '../api/system';
import type { LookupCategory } from '../types/system';

/**
 * BL-V5 (UC-130): tên các mục đang hiện của một danh mục (quan hệ, bộ phận, chức danh), theo thứ tự Admin đặt.
 * Trong lúc tải hoặc khi lỗi trả danh sách dự phòng để form vẫn dùng được. Gợi ý nhập, không chặn giá trị khác.
 */
export function useLookupNames(category: LookupCategory, fallback: string[] = []): string[] {
  const [names, setNames] = useState<string[]>(fallback);
  useEffect(() => {
    let alive = true;
    systemApi.lookups(category)
      .then((rows) => {
        if (alive && rows.length > 0) setNames(rows.map((row) => row.name));
      })
      .catch(() => undefined);
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- fallback chỉ là giá trị khởi tạo
  }, [category]);
  return names;
}
