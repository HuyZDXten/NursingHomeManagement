import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, Col, Empty, Pagination, Row, Segmented, Skeleton, Tag, Typography } from 'antd';
import { PictureOutlined } from '@ant-design/icons';
import { systemApi } from '../../api/system';
import { dateTime } from '../../utils/format';
import { mediaUrl } from '../../utils/media';
import type { PageResult, PostKind, PostSummary } from '../../types/system';

const PAGE_SIZE = 9;

/** BL-V5, UC-19/115: tin tức và album ảnh công khai (không cần đăng nhập). */
export default function News() {
  const [kind, setKind] = useState<PostKind | 'all'>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PageResult<PostSummary>>();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let alive = true;
    setData(undefined);
    setFailed(false);
    systemApi.publicPosts({ kind: kind === 'all' ? undefined : kind, page, pageSize: PAGE_SIZE })
      .then((result) => { if (alive) setData(result); })
      .catch(() => { if (alive) setFailed(true); });
    return () => {
      alive = false;
    };
  }, [kind, page]);

  return (
    <div className="container" style={{ paddingTop: 24, paddingBottom: 32 }}>
      <Typography.Title level={2}>Tin tức & hình ảnh sinh hoạt</Typography.Title>
      <Segmented<PostKind | 'all'> style={{ marginBottom: 24 }} value={kind} onChange={(v) => { setKind(v); setPage(1); }}
        options={[{ value: 'all', label: 'Tất cả' }, { value: 'News', label: 'Tin tức' }, { value: 'Album', label: 'Album ảnh' }]} />
      {failed && <Empty description="Không tải được tin tức. Vui lòng thử lại sau." />}
      {!failed && !data && <Skeleton active />}
      {data && data.items.length === 0 && <Empty description="Chưa có bài viết nào." />}
      <Row gutter={[16, 16]}>
        {data?.items.map((post) => (
          <Col xs={24} sm={12} lg={8} key={post.id}>
            <Link to={`/tin-tuc/${post.slug}`}>
              <Card hoverable style={{ height: '100%' }}
                cover={post.coverUrl
                  ? <img alt={post.title} src={mediaUrl(post.coverUrl)} style={{ height: 180, objectFit: 'cover' }} />
                  : <div style={{ height: 180, display: 'grid', placeItems: 'center', background: '#e8f5f1', color: '#2f7d6d', fontSize: 40 }}><PictureOutlined /></div>}>
                <Card.Meta
                  title={post.title}
                  description={
                    <>
                      <Typography.Paragraph type="secondary" ellipsis={{ rows: 2 }} style={{ marginBottom: 8 }}>{post.summary}</Typography.Paragraph>
                      <Tag color={post.kind === 'Album' ? 'purple' : 'blue'}>{post.kind === 'Album' ? `Album · ${post.imageCount} ảnh` : 'Tin tức'}</Tag>
                      <small>{dateTime(post.publishedAt).slice(0, 10)}</small>
                    </>
                  } />
              </Card>
            </Link>
          </Col>
        ))}
      </Row>
      {data && data.total > PAGE_SIZE && (
        <Pagination style={{ marginTop: 24, textAlign: 'center' }} align="center" current={page} pageSize={PAGE_SIZE} total={data.total} showSizeChanger={false} onChange={setPage} />
      )}
    </div>
  );
}
