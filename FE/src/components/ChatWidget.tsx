import { useEffect, useRef, useState } from 'react';
import { Button, Card, FloatButton, Grid, Input, Space, Typography } from 'antd';
import { CloseOutlined, MessageOutlined, SendOutlined } from '@ant-design/icons';
import { extractErrorMessage } from '../api/client';

export interface ChatWidgetMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatWidgetProps {
  title: string;
  greeting: string;
  /** Gửi câu hỏi, trả câu trả lời của trợ lý. */
  send: (text: string) => Promise<string>;
  /** Lịch sử để hiện lại khi mở (tùy chọn). */
  loadHistory?: () => Promise<ChatWidgetMessage[]>;
  /** Dòng nhỏ dưới ô nhập, ví dụ cảnh báo "không thay thế tư vấn y tế". */
  disclaimer?: string;
}

const MAX_LENGTH = 1000;

/**
 * S4-V1 — khung chat AI dùng chung (công khai, gia đình S4-H1, nhân viên). Nút nổi góc phải; trên điện thoại mở toàn màn hình.
 * Không giữ state toàn cục: mỗi trang tự truyền send/loadHistory.
 */
export function ChatWidget({ title, greeting, send, loadHistory, disclaimer }: ChatWidgetProps) {
  const screens = Grid.useBreakpoint();
  const mobile = !screens.sm;
  const [open, setOpen] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [items, setItems] = useState<ChatWidgetMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const bottom = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open || loaded) return;
    setLoaded(true);
    loadHistory?.().then(setItems).catch(() => undefined);
  }, [open, loaded, loadHistory]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: 'end' });
  }, [items.length, busy, open]);

  const submit = async () => {
    const text = draft.trim();
    if (!text || busy) return;
    setDraft('');
    setItems((current) => [...current, { role: 'user', content: text }]);
    setBusy(true);
    try {
      const answer = await send(text);
      setItems((current) => [...current, { role: 'assistant', content: answer }]);
    } catch (e) {
      setItems((current) => [...current, { role: 'assistant', content: extractErrorMessage(e) }]);
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return <FloatButton type="primary" icon={<MessageOutlined />} tooltip={title} onClick={() => setOpen(true)} style={{ insetInlineEnd: 24 }} />;
  }

  const panelStyle: React.CSSProperties = mobile
    ? { position: 'fixed', inset: 0, zIndex: 1100, borderRadius: 0, display: 'flex', flexDirection: 'column' }
    : { position: 'fixed', right: 24, bottom: 24, width: 360, height: 520, zIndex: 1100, display: 'flex', flexDirection: 'column', boxShadow: '0 8px 24px rgba(0,0,0,0.18)' };

  return (
    <Card size="small" title={title} style={panelStyle}
      styles={{ body: { flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0, padding: 12 } }}
      extra={<Button type="text" size="small" icon={<CloseOutlined />} aria-label="Đóng" onClick={() => setOpen(false)} />}>
      <div style={{ flex: 1, overflowY: 'auto', paddingRight: 4 }} aria-live="polite">
        <Bubble role="assistant" content={greeting} />
        {items.map((item, index) => <Bubble key={index} role={item.role} content={item.content} />)}
        {busy && <Typography.Text type="secondary" italic>Đang trả lời…</Typography.Text>}
        <div ref={bottom} />
      </div>
      <Space.Compact style={{ width: '100%', marginTop: 8 }}>
        <Input.TextArea value={draft} maxLength={MAX_LENGTH} autoSize={{ minRows: 1, maxRows: 4 }} placeholder="Nhập câu hỏi…"
          onChange={(e) => setDraft(e.target.value)}
          onPressEnter={(e) => { if (!e.shiftKey) { e.preventDefault(); void submit(); } }} />
        <Button type="primary" icon={<SendOutlined />} loading={busy} disabled={!draft.trim()} onClick={submit} aria-label="Gửi" />
      </Space.Compact>
      {disclaimer && <Typography.Text type="secondary" style={{ fontSize: 11, marginTop: 4 }}>{disclaimer}</Typography.Text>}
    </Card>
  );
}

function Bubble({ role, content }: ChatWidgetMessage) {
  const mine = role === 'user';
  return (
    <div style={{ display: 'flex', justifyContent: mine ? 'flex-end' : 'flex-start', marginBottom: 8 }}>
      <div style={{
        maxWidth: '85%', padding: '6px 10px', borderRadius: 10, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere',
        background: mine ? '#2f7d6d' : 'rgba(0, 0, 0, 0.05)', color: mine ? '#fff' : undefined,
      }}>
        {content}
      </div>
    </div>
  );
}
