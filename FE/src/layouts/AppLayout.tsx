import { useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Avatar, Badge, Button, Dropdown, Empty, Layout, List, Menu, Popover, Typography } from 'antd';
import { BellOutlined, HomeOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';
import { PublicChatbot } from '../components/PublicChatbot';
import { hasRole } from '../auth/roles';
import { useNotifications } from '../hooks/useNotifications';
import { dateTime } from '../utils/format';
import { ROLE_LABEL } from '../utils/status';
import { MENU } from './menu';

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const notes = useNotifications(!!user);

  const items = MENU.filter((entry) => hasRole(user?.role, entry.roles));
  const selected = items.map((i) => i.key).filter((k) => location.pathname.startsWith(k)).sort((a, b) => b.length - a.length)[0];

  const bell = (
    <div style={{ width: 360, maxHeight: 420, overflow: 'auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
        <Typography.Text strong>Thông báo</Typography.Text>
        <Button size="small" type="link" onClick={() => void notes.markAllRead()}>Đánh dấu đã đọc</Button>
      </div>
      {notes.items.length === 0 ? <Empty description="Chưa có thông báo" /> : (
        <List
          size="small"
          dataSource={notes.items}
          renderItem={(item) => (
            <List.Item
              style={{ cursor: 'pointer', background: item.isRead ? undefined : '#f0f7f5' }}
              onClick={() => {
                if (!item.isRead) void notes.markRead(item.id);
                if (item.link) navigate(item.link);
              }}
            >
              <List.Item.Meta
                title={item.title}
                description={<>{item.body && <div>{item.body}</div>}<small>{dateTime(item.createdAt)}</small></>}
              />
            </List.Item>
          )}
        />
      )}
    </div>
  );

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Sider collapsible collapsed={collapsed} onCollapse={setCollapsed} theme="light" breakpoint="lg" width={230}>
        <Link to="/" className="brand">
          <HomeOutlined /> {!collapsed && <span>Viện Dưỡng Lão</span>}
        </Link>
        <Menu
          mode="inline"
          selectedKeys={selected ? [selected] : []}
          items={items.map((i) => ({ key: i.key, icon: i.icon, label: i.label }))}
          onClick={(e) => navigate(e.key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header className="app-header">
          <span />
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <Popover content={bell} trigger="click" placement="bottomRight">
              <Badge count={notes.unread} size="small">
                <Button shape="circle" icon={<BellOutlined />} aria-label="Thông báo" />
              </Badge>
            </Popover>
            <Dropdown
              menu={{
                items: [
                  { key: 'profile', icon: <UserOutlined />, label: 'Hồ sơ & mật khẩu', onClick: () => navigate('/profile') },
                  { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất', onClick: async () => { await logout(); navigate('/login'); } },
                ],
              }}
            >
              <span style={{ cursor: 'pointer' }}>
                <Avatar style={{ background: '#2f7d6d' }}>{user?.name.charAt(0)}</Avatar>{' '}
                <span className="hide-sm">{user?.name} · {user ? ROLE_LABEL[user.role] : ''}</span>
              </span>
            </Dropdown>
          </div>
        </Layout.Header>
        <Layout.Content style={{ padding: 24 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}

export function PublicLayout() {
  const { user } = useAuth();
  const navigate = useNavigate();
  return (
    <Layout style={{ minHeight: '100vh', background: '#f6faf9' }}>
      <Layout.Header className="app-header public">
        <Link to="/" className="brand"><HomeOutlined /> <span>Viện Dưỡng Lão</span></Link>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button onClick={() => navigate('/dat-lich-tham-quan')}>Đặt lịch tham quan</Button>
          {user
            ? <Button type="primary" onClick={() => navigate('/app')}>Vào hệ thống</Button>
            : <Button type="primary" onClick={() => navigate('/login')}>Đăng nhập</Button>}
        </div>
      </Layout.Header>
      <Layout.Content><Outlet /></Layout.Content>
      <Layout.Footer style={{ textAlign: 'center' }}>Nursing Home Management · PRN232</Layout.Footer>
      <PublicChatbot />
    </Layout>
  );
}
