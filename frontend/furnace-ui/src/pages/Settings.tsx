import { useAuth } from '../context/AuthContext';
import { useSignalR } from '../context/SignalRContext';
import { useTheme } from '../context/ThemeContext';

export default function Settings() {
  const { user } = useAuth();
  const { connected, connect, disconnect, setSystemLive } = useSignalR();

  const handleConnect = async () => {
    await connect();
    await setSystemLive(true);
  };

  const handleDisconnect = async () => {
    await setSystemLive(false);
    await disconnect();
  };
  const { theme, setTheme } = useTheme();

  return (
    <div>
      <header className="mb-4">
        <h4 className="text-white mb-0">Settings</h4>
        <small className="fm-muted">Application, theme, and connection settings</small>
      </header>

      <div className="row g-4">
        {/* Theme Settings */}
        <div className="col-12">
          <div className="fm-panel">
            <div className="d-flex align-items-center justify-content-between mb-3">
              <div>
                <h6 className="text-white mb-1">
                  <i className="bi bi-palette-fill me-2 text-info" />
                  Theme
                </h6>
              </div>
              <span className="badge bg-primary px-3 py-2">
                Active: {theme === 'dark' ? 'Deep Space Gravity' : 'Airy Zenith'}
              </span>
            </div>

            <div className="row g-3">
              {/* Dark Mode Card */}
              <div className="col-md-6">
                <div
                  className={`p-3 rounded-3 border transition-all cursor-pointer ${
                    theme === 'dark' ? 'border-info bg-opacity-10 bg-info' : 'border-secondary'
                  }`}
                  style={{
                    background: '#12161F',
                    color: '#E8EEF2',
                    boxShadow: theme === 'dark' ? '0 0 20px rgba(0, 229, 255, 0.25)' : 'none',
                    cursor: 'pointer',
                  }}
                  onClick={() => setTheme('dark')}
                >
                  <div className="d-flex align-items-center justify-content-between mb-2">
                    <span className="fw-bold d-flex align-items-center">
                      <i className="bi bi-moon-stars-fill text-info me-2" />
                      Theme 1: Dark Mode (Deep Space Gravity)
                    </span>
                    {theme === 'dark' && <i className="bi bi-check-circle-fill text-info fs-5" />}
                  </div>
                  <p className="small text-secondary mb-3" style={{ color: '#9BA3B5' }}>
                    Deep Space Slate (#12161F) with Matte Charcoal (#1E232F) floating cards, Cyan glow shadows, and a geometric dot grid background.
                  </p>
                  <div className="d-flex gap-2 align-items-center">
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#12161F', border: '1px solid #00E5FF' }} title="Deep Space Slate (#12161F)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#1E232F', border: '1px solid #00E5FF' }} title="Matte Charcoal (#1E232F)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#00E5FF' }} title="Gravity Cyan (#00E5FF)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#E8EEF2' }} title="Ice White (#E8EEF2)" />
                  </div>
                </div>
              </div>

              {/* Light Mode Card */}
              <div className="col-md-6">
                <div
                  className={`p-3 rounded-3 border transition-all cursor-pointer ${
                    theme === 'light' ? 'border-primary bg-opacity-10 bg-primary' : 'border-secondary'
                  }`}
                  style={{
                    background: '#F4F6F9',
                    color: '#1A2130',
                    boxShadow: theme === 'light' ? '0 0 20px rgba(37, 99, 235, 0.25)' : 'none',
                    cursor: 'pointer',
                  }}
                  onClick={() => setTheme('light')}
                >
                  <div className="d-flex align-items-center justify-content-between mb-2">
                    <span className="fw-bold d-flex align-items-center" style={{ color: '#1A2130' }}>
                      <i className="bi bi-sun-fill text-warning me-2" />
                      Theme 2: Light Mode (Airy Zenith)
                    </span>
                    {theme === 'light' && <i className="bi bi-check-circle-fill text-primary fs-5" />}
                  </div>
                  <p className="small mb-3" style={{ color: '#6B7280' }}>
                    Cloud Silver (#F4F6F9) with Pure White (#FFFFFF) floating cards and deep blue-tinted soft drop shadows.
                  </p>
                  <div className="d-flex gap-2 align-items-center">
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#F4F6F9', border: '1px solid #2563EB' }} title="Cloud Silver (#F4F6F9)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#FFFFFF', border: '1px solid #2563EB' }} title="Pure White (#FFFFFF)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#2563EB' }} title="Magnetic Blue (#2563EB)" />
                    <span className="d-inline-block rounded-circle" style={{ width: 22, height: 22, background: '#7C3AED' }} title="Vibrant Violet (#7C3AED)" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="col-lg-6">
          <div className="fm-panel">
            <h6 className="text-white mb-3">Account</h6>
            <div className="mb-2">
              <span className="fm-muted">Username:</span>{' '}
              <span className="fw-semibold">{user?.username}</span>
            </div>
            <div className="mb-2">
              <span className="fm-muted">Display Name:</span>{' '}
              <span className="fw-semibold">{user?.displayName}</span>
            </div>
            <div className="mb-2">
              <span className="fm-muted">Role:</span>{' '}
              <span className="badge text-bg-info">{user?.role}</span>
            </div>
          </div>
        </div>

        <div className="col-lg-6">
          <div className="fm-panel">
            <h6 className="text-white mb-3">Real-Time Connection</h6>
            <div className="mb-3">
              <span className="fm-muted">Status:</span>{' '}
              <span className={`badge ${connected ? 'text-bg-success' : 'text-bg-danger'}`}>
                {connected ? 'CONNECTED' : 'DISCONNECTED'}
              </span>
            </div>
            <div className="d-flex gap-2">
              <button className="btn btn-fm" onClick={() => void handleConnect()} disabled={connected}>
                Connect
              </button>
              <button
                className="btn btn-outline-danger"
                onClick={() => void handleDisconnect()}
                disabled={!connected}
              >
                Disconnect
              </button>
            </div>
            <p className="fm-muted small mt-3 mb-0">
              The client uses SignalR with automatic reconnection and exponential backoff to
              guarantee low-latency furnace updates.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
