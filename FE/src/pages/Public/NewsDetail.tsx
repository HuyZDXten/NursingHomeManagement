import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Image, Result, Skeleton, Tag, Typography } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { systemApi } from '../../api/system';
import { dateTime } from '../../utils/format';
import { mediaUrl } from '../../utils/media';
import type { PublicPost } from '../../types/system';

/** BL-V5, UC-19/115: một bài tin tức hoặc album công khai. Nội dung là chữ thuần nên không bao giờ render HTML. */
export default function NewsDetail() {
  const { slug = '' } = useParams();
  const [post, setPost] = useState<PublicPost>();
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    let alive = true;
    setPost(undefined);
    setMissing(false);
    systemApi.publicPost(slug)
      .then((result) => { if (alive) setPost(result); })
      .catch(() => { if (alive) setMissing(true); });
    return () => {
      alive = false;
    };
  }, [slug]);

  if (missing) {
    return <Result status="404" title="Không tìm thấy bài viết" subTitle="Bài viết không tồn tại hoặc đã được gỡ." extra={<Link to="/tin-tuc"><Button type="primary">Về trang tin tức</Button></Link>} />;
  }
  if (!post) return <div className="container" style={{ paddingTop: 24 }}><Skeleton active /></div>;

  return (
    <div className="container" style={{ paddingTop: 24, paddingBottom: 32, maxWidth: 860 }}>
      <Link to="/tin-tuc"><ArrowLeftOutlined /> Tin tức</Link>
      <Typography.Title level={2} style={{ marginTop: 12 }}>{post.title}</Typography.Title>
      <div style={{ marginBottom: 16 }}>
        <Tag color={post.kind === 'Album' ? 'purple' : 'blue'}>{post.kind === 'Album' ? 'Album ảnh' : 'Tin tức'}</Tag>
        <small>{dateTime(post.publishedAt).slice(0, 10)}</small>
      </div>
      {post.summary && <Typography.Paragraph strong>{post.summary}</Typography.Paragraph>}
      <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', fontSize: 16 }}>{post.content}</Typography.Paragraph>
      {post.images.length > 0 && (
        <Image.PreviewGroup>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 12, marginTop: 16 }}>
            {post.images.map((image) => (
              <figure key={image.id} style={{ margin: 0 }}>
                <Image src={mediaUrl(image.url)} alt={image.caption ?? post.title} style={{ width: '100%', height: 160, objectFit: 'cover' }} />
                {image.caption && <figcaption style={{ fontSize: 13, color: '#666', marginTop: 4 }}>{image.caption}</figcaption>}
              </figure>
            ))}
          </div>
        </Image.PreviewGroup>
      )}
    </div>
  );
}
