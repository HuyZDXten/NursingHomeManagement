import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Alert, App, Badge, Button, Card, Col, Empty, Input, List, Row, Select, Space, Typography } from 'antd';
import { SendOutlined } from '@ant-design/icons';
import { chatApi } from '../../api/chat';
import { extractErrorMessage } from '../../api/client';
import { portalApi } from '../../api/portal';
import { useAuth } from '../../auth/AuthContext';
import { PageTitle } from '../../components/Common';
import { useChat } from '../../hooks/useChat';
import { dateTime, timeOf } from '../../utils/format';
import { ROLE_LABEL } from '../../utils/status';
import type { FamilyResident, Role } from '../../types';
import type { ChatMessage, Conversation } from '../../types/operations';

const PAGE = 30;
const MAX_LENGTH = 2000;

/**
 * UC-106: tin nhắn gia đình – nhân viên, dùng chung cho /staff/messages và /family/messages.
 * Cột trái: cuộc trò chuyện (mới nhất trước, số chưa đọc). Cột phải: tin nhắn, Enter để gửi, Shift+Enter xuống dòng.
 */
export default function Messages() {
  const { message } = App.useApp();
  const { user } = useAuth();
  const isFamily = user?.role === 'Family';
  const [params, setParams] = useSearchParams();
  const selectedId = Number(params.get('c')) || undefined;
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [relatives, setRelatives] = useState<FamilyResident[]>([]);
  const [error, setError] = useState<string>();

  const loadConversations = useCallback(async () => {
    try {
      setConversations(await chatApi.conversations());
      setError(undefined);
    } catch (e) {
      setError(extractErrorMessage(e));
    }
  }, []);

  useEffect(() => {
    void loadConversations();
    const timer = window.setInterval(() => void loadConversations(), 30000); // dự phòng khi mất kết nối realtime
    return () => window.clearInterval(timer);
  }, [loadConversations]);

  useEffect(() => {
    if (isFamily) portalApi.familyResidents().then(setRelatives).catch(() => undefined);
  }, [isFamily]);

  const select = (id: number) => setParams({ c: String(id) }, { replace: true });

  const startWith = async (residentId: number) => {
    try {
      const opened = await chatApi.open(residentId);
      await loadConversations();
      select(opened.id);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  // Tin realtime: cập nhật danh sách bên trái; khung đang mở tự xử lý trong ConversationPane.
  const [incoming, setIncoming] = useState<ChatMessage>();
  useChat(useCallback((item: ChatMessage) => {
    setIncoming(item);
    setConversations((current) => {
      const found = current.find((c) => c.id === item.conversationId);
      if (!found) {
        void loadConversations();
        return current;
      }
      const mine = item.senderId === user?.accountId;
      const updated = {
        ...found,
        lastMessage: item.body,
        lastMessageAt: item.sentAt,
        unread: mine || item.conversationId === selectedId ? found.unread : found.unread + 1,
      };
      return [updated, ...current.filter((c) => c.id !== item.conversationId)];
    });
  }, [user?.accountId, selectedId, loadConversations]));

  const selected = conversations.find((c) => c.id === selectedId);
  const withoutConversation = relatives.filter((r) => !conversations.some((c) => c.residentId === r.residentId));

  if (error) return <><PageTitle title="Tin nhắn" /><Alert type="error" showIcon message={error} /></>;

  return (
    <>
      <PageTitle title="Tin nhắn" />
      <Row gutter={16}>
        <Col xs={24} md={9} lg={8} style={{ marginBottom: 16 }}>
          <Card size="small" title={isFamily ? 'Trò chuyện với cơ sở' : 'Cuộc trò chuyện với gia đình'}>
            {isFamily && withoutConversation.length > 0 && (
              <Select style={{ width: '100%', marginBottom: 12 }} placeholder="Nhắn tin về người thân…" value={null}
                onChange={(v: number) => void startWith(v)}
                options={withoutConversation.map((r) => ({ value: r.residentId, label: `${r.code} · ${r.fullName}` }))} />
            )}
            <List
              dataSource={conversations}
              locale={{ emptyText: <Empty description="Chưa có cuộc trò chuyện" /> }}
              renderItem={(c) => (
                <List.Item onClick={() => select(c.id)}
                  style={{ cursor: 'pointer', paddingInline: 8, background: c.id === selectedId ? 'rgba(47, 125, 109, 0.08)' : undefined }}>
                  <List.Item.Meta
                    title={(
                      <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                        <span>{c.residentCode} · {c.residentName}</span>
                        <Badge count={c.unread} size="small" />
                      </Space>
                    )}
                    description={(
                      <>
                        {!isFamily && <div><Typography.Text type="secondary">Gia đình: {c.familyName}</Typography.Text></div>}
                        <Typography.Text type="secondary" ellipsis style={{ maxWidth: '100%' }}>
                          {c.lastMessage ?? 'Chưa có tin nhắn'}{c.lastMessageAt ? ` · ${timeOf(c.lastMessageAt)}` : ''}
                        </Typography.Text>
                      </>
                    )}
                  />
                </List.Item>
              )}
            />
          </Card>
        </Col>
        <Col xs={24} md={15} lg={16}>
          {selected
            ? <ConversationPane key={selected.id} conversation={selected} incoming={incoming}
                onRead={() => setConversations((current) => current.map((c) => (c.id === selected.id ? { ...c, unread: 0 } : c)))} />
            : <Card><Empty description={isFamily ? 'Chọn người thân để nhắn tin với cơ sở' : 'Chọn một cuộc trò chuyện'} /></Card>}
        </Col>
      </Row>
    </>
  );
}

function ConversationPane({ conversation, incoming, onRead }: { conversation: Conversation; incoming?: ChatMessage; onRead: () => void }) {
  const { message } = App.useApp();
  const { user } = useAuth();
  const [items, setItems] = useState<ChatMessage[]>([]);
  const [hasOlder, setHasOlder] = useState(false);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const bottom = useRef<HTMLDivElement>(null);
  // onRead của trang cha đổi mỗi lần render; giữ bản mới nhất trong ref để markRead ổn định theo cuộc trò chuyện.
  const onReadRef = useRef(onRead);
  useEffect(() => {
    onReadRef.current = onRead;
  }, [onRead]);

  const markRead = useCallback(() => {
    chatApi.markRead(conversation.id).then(() => onReadRef.current()).catch(() => undefined);
  }, [conversation.id]);

  useEffect(() => {
    chatApi.messages(conversation.id).then((page) => {
      setItems(page);
      setHasOlder(page.length === PAGE);
      markRead();
    }).catch((e) => message.error(extractErrorMessage(e)));
  }, [conversation.id, message, markRead]);

  useEffect(() => {
    if (!incoming || incoming.conversationId !== conversation.id) return;
    setItems((current) => (current.some((m) => m.id === incoming.id) ? current : [...current, incoming]));
    if (incoming.senderId !== user?.accountId) markRead();
  }, [incoming, conversation.id, user?.accountId, markRead]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: 'end' });
  }, [items.length]);

  const loadOlder = async () => {
    try {
      const older = await chatApi.messages(conversation.id, items[0]?.id);
      setItems((current) => [...older, ...current]);
      setHasOlder(older.length === PAGE);
    } catch (e) {
      message.error(extractErrorMessage(e));
    }
  };

  const send = async () => {
    const body = draft.trim();
    if (!body) return;
    setSending(true);
    try {
      const sent = await chatApi.send(conversation.id, body);
      setItems((current) => (current.some((m) => m.id === sent.id) ? current : [...current, sent]));
      setDraft('');
    } catch (e) {
      message.error(extractErrorMessage(e));
    } finally {
      setSending(false);
    }
  };

  return (
    <Card size="small" title={`${conversation.residentCode} · ${conversation.residentName}`}
      extra={<Typography.Text type="secondary">Gia đình: {conversation.familyName}</Typography.Text>}>
      <div style={{ height: 'min(60vh, 520px)', overflowY: 'auto', padding: '4px 4px 8px' }}>
        {hasOlder && <div style={{ textAlign: 'center', marginBottom: 8 }}><Button size="small" onClick={loadOlder}>Tải tin cũ hơn</Button></div>}
        {items.length === 0 && <Empty description="Hãy gửi tin nhắn đầu tiên" />}
        {items.map((m) => {
          const mine = m.senderId === user?.accountId;
          return (
            <div key={m.id} style={{ display: 'flex', justifyContent: mine ? 'flex-end' : 'flex-start', marginBottom: 8 }}>
              <div style={{
                maxWidth: '78%', padding: '8px 12px', borderRadius: 12, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere',
                background: mine ? '#2f7d6d' : 'rgba(0, 0, 0, 0.05)', color: mine ? '#fff' : undefined,
              }}>
                {!mine && (
                  <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 2 }}>
                    {m.senderName}{m.fromFamily ? '' : ` · ${ROLE_LABEL[m.senderRole as Role] ?? m.senderRole}`}
                  </div>
                )}
                <div>{m.body}</div>
                <div style={{ fontSize: 11, opacity: 0.75, textAlign: 'right', marginTop: 2 }} title={dateTime(m.sentAt)}>
                  {timeOf(m.sentAt)}{mine && m.readAt ? ' · Đã xem' : ''}
                </div>
              </div>
            </div>
          );
        })}
        <div ref={bottom} />
      </div>
      <Space.Compact style={{ width: '100%', marginTop: 8 }}>
        <Input.TextArea value={draft} maxLength={MAX_LENGTH} autoSize={{ minRows: 1, maxRows: 5 }}
          placeholder="Nhập tin nhắn (Enter để gửi, Shift+Enter xuống dòng)"
          onChange={(e) => setDraft(e.target.value)}
          onPressEnter={(e) => { if (!e.shiftKey) { e.preventDefault(); void send(); } }} />
        <Button type="primary" icon={<SendOutlined />} loading={sending} onClick={send} disabled={!draft.trim()}>Gửi</Button>
      </Space.Compact>
    </Card>
  );
}
