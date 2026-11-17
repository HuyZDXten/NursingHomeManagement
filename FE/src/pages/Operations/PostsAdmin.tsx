import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Checkbox, Form, Image, Input, Modal, Popconfirm, Segmented, Select, Space, Table, Tag, Upload } from 'antd';
import { ArrowDownOutlined, ArrowUpOutlined, DeleteOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../../api/client';
import { systemApi } from '../../api/system';
import { PageTitle } from '../../components/Common';
import { dateTime } from '../../utils/format';
import { checkImageFile, mediaUrl } from '../../utils/media';
import type { PageResult, Post, PostForm, PostKind, PostSummary } from '../../types/system';

const KIND_LABEL: Record<PostKind, string> = { News: 'Tin tức', Album: 'Album ảnh' };
const MAX_IMAGES = 30;

interface EditorImage {
  url: string;
  caption?: string;
}

/** BL-V5, UC-19 tin tức và UC-115 album (Admin, Lễ tân). Bài tạo ở dạng nháp; album chỉ đăng khi đã xác nhận có sự đồng ý. */
export default function PostsAdmin() {
  const { message } = App.useApp();
  const [kind, setKind] = useState<PostKind | 'all'>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PageResult<PostSummary>>();
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<Post | 'new' | null>(null);
  const [images, setImages] = useState<EditorImage[]>([]);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [form] = Form.useForm<PostForm>();
  const formKind = Form.useWatch('kind', form);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await systemApi.posts({ kind: kind === 'all' ? undefined : kind, page, pageSize: 10 }));
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [kind, page, message]);
  useEffect(() => {
    void load();
  }, [load]);

  const open = async (target: PostSummary | 'new') => {
    try {
      if (target === 'new') {
        form.setFieldsValue({ kind: 'News', title: '', summary: '', content: '', consentConfirmed: false });
        setImages([]);
        setEditing('new');
      } else {
        const post = await systemApi.post(target.id);
        form.setFieldsValue({ kind: post.kind, title: post.title, summary: post.summary ?? '', content: post.content, consentConfirmed: post.consentConfirmed });
        setImages(post.images.map((i) => ({ url: i.url, caption: i.caption ?? '' })));
        setEditing(post);
      }
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const save = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const body: PostForm = {
        ...values,
        consentConfirmed: values.kind === 'Album' && !!values.consentConfirmed,
        images: images.map((i) => ({ url: i.url, caption: i.caption?.trim() || undefined })),
      };
      if (editing === 'new') await systemApi.createPost(body);
      else if (editing) await systemApi.updatePost(editing.id, body);
      message.success(editing === 'new' ? 'Đã lưu bản nháp. Bấm "Đăng" để hiện ở trang công khai.' : 'Đã lưu bài viết.');
      setEditing(null);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const act = async (action: () => Promise<unknown>, done: string) => {
    try {
      await action();
      message.success(done);
      void load();
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const addImages = async (files: File[]) => {
    const room = MAX_IMAGES - images.length;
    if (files.length > room) message.warning(`Chỉ thêm được ${Math.max(room, 0)} ảnh nữa (tối đa ${MAX_IMAGES}).`);
    setUploading(true);
    const added: EditorImage[] = [];
    for (const file of files.slice(0, Math.max(room, 0))) {
      const problem = checkImageFile(file);
      if (problem) {
        message.error(`${file.name}: ${problem}`);
        continue;
      }
      try {
        added.push({ url: (await systemApi.uploadImage(file)).url, caption: '' });
      } catch (e) {
        message.error(`${file.name}: ${extractErrorMessage(e)}`);
      }
    }
    setImages((current) => [...current, ...added]);
    setUploading(false);
  };

  const move = (index: number, delta: number) =>
    setImages((current) => {
      const next = [...current];
      const [item] = next.splice(index, 1);
      next.splice(index + delta, 0, item);
      return next;
    });

  return (
    <>
      <PageTitle title="Tin tức & album" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => void open('new')}>Viết bài mới</Button>} />
      <Segmented<PostKind | 'all'> style={{ marginBottom: 16 }} value={kind} onChange={(v) => { setKind(v); setPage(1); }}
        options={[{ value: 'all', label: 'Tất cả' }, { value: 'News', label: 'Tin tức' }, { value: 'Album', label: 'Album ảnh' }]} />
      <Table<PostSummary> rowKey="id" size="small" loading={loading} dataSource={data?.items ?? []} scroll={{ x: 760 }}
        pagination={{ current: page, pageSize: 10, total: data?.total ?? 0, onChange: setPage, hideOnSinglePage: true }}
        columns={[
          { title: 'Ảnh bìa', width: 90, render: (_, r) => (r.coverUrl ? <Image src={mediaUrl(r.coverUrl)} width={64} height={44} style={{ objectFit: 'cover' }} /> : null) },
          { title: 'Tiêu đề', render: (_, r) => <>{r.title}<br /><small style={{ color: '#888' }}>/tin-tuc/{r.slug}</small></> },
          { title: 'Loại', width: 110, render: (_, r) => <Tag color={r.kind === 'Album' ? 'purple' : 'blue'}>{KIND_LABEL[r.kind]}{r.kind === 'Album' ? ` · ${r.imageCount} ảnh` : ''}</Tag> },
          { title: 'Trạng thái', width: 150, render: (_, r) => (r.isPublished ? <Tag color="green">Đã đăng {dateTime(r.publishedAt).slice(0, 10)}</Tag> : <Tag>Nháp</Tag>) },
          {
            title: '', width: 280, render: (_, r) => (
              <Space wrap>
                <Button size="small" onClick={() => void open(r)}>Sửa</Button>
                {r.isPublished
                  ? <>
                      <Link to={`/tin-tuc/${r.slug}`} target="_blank">Xem</Link>
                      <Button size="small" onClick={() => act(() => systemApi.unpublishPost(r.id), 'Đã gỡ bài khỏi trang công khai.')}>Gỡ</Button>
                    </>
                  : <Button size="small" type="primary" onClick={() => act(() => systemApi.publishPost(r.id), 'Đã đăng bài.')}>Đăng</Button>}
                <Popconfirm title="Xóa bài viết này?" onConfirm={() => act(() => systemApi.deletePost(r.id), 'Đã xóa bài viết.')}><Button size="small" danger>Xóa</Button></Popconfirm>
              </Space>
            ),
          },
        ]} />

      <Modal open={editing !== null} title={editing === 'new' ? 'Bài viết mới' : 'Sửa bài viết'} width={760} okText="Lưu" cancelText="Hủy" confirmLoading={saving}
        onOk={save} onCancel={() => setEditing(null)} destroyOnHidden>
        <Form form={form} layout="vertical">
          <Form.Item name="kind" label="Loại bài" rules={[{ required: true }]}>
            <Select disabled={editing !== 'new'} options={[{ value: 'News', label: 'Tin tức' }, { value: 'Album', label: 'Album ảnh' }]} />
          </Form.Item>
          <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, whitespace: true, max: 200 }]}><Input /></Form.Item>
          <Form.Item name="summary" label="Tóm tắt (hiện ở danh sách)" rules={[{ max: 400 }]}><Input.TextArea rows={2} showCount maxLength={400} /></Form.Item>
          <Form.Item name="content" label="Nội dung (chữ thuần, xuống dòng giữ nguyên)" rules={[{ required: true, whitespace: true, max: 20000 }]}><Input.TextArea rows={8} /></Form.Item>
          {formKind === 'Album' && (
            <Form.Item name="consentConfirmed" valuePropName="checked">
              <Checkbox>Tôi xác nhận đã có sự đồng ý của cư dân hoặc người thân cho những hình ảnh này xuất hiện công khai.</Checkbox>
            </Form.Item>
          )}
          <Form.Item label={`Ảnh (${images.length}/${MAX_IMAGES}) — ảnh đầu tiên là ảnh bìa`}>
            <Upload accept="image/jpeg,image/png,image/webp" multiple showUploadList={false} disabled={uploading || images.length >= MAX_IMAGES}
              beforeUpload={(file, list) => {
                if (file === list[0]) void addImages(list); // gọi một lần cho cả lượt chọn nhiều ảnh
                return false;
              }}>
              <Button icon={<UploadOutlined />} loading={uploading} disabled={images.length >= MAX_IMAGES}>Tải ảnh lên</Button>
            </Upload>
            {images.map((image, index) => (
              <div key={`${image.url}-${index}`} style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 8 }}>
                <Image src={mediaUrl(image.url)} width={72} height={52} style={{ objectFit: 'cover' }} />
                <Input placeholder="Chú thích (không bắt buộc)" maxLength={200} value={image.caption}
                  onChange={(e) => setImages((current) => current.map((item, i) => (i === index ? { ...item, caption: e.target.value } : item)))} />
                <Button icon={<ArrowUpOutlined />} disabled={index === 0} onClick={() => move(index, -1)} />
                <Button icon={<ArrowDownOutlined />} disabled={index === images.length - 1} onClick={() => move(index, 1)} />
                <Button danger icon={<DeleteOutlined />} onClick={() => setImages((current) => current.filter((_, i) => i !== index))} />
              </div>
            ))}
          </Form.Item>
          {editing !== 'new' && editing?.isPublished && (
            <Alert type="warning" showIcon title="Bài đang hiển thị công khai: lưu là cập nhật ngay trên trang công khai." />
          )}
        </Form>
      </Modal>
    </>
  );
}
