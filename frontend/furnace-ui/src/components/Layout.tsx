import { useState, useEffect } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useSignalR } from '../context/SignalRContext';
import { useTheme } from '../context/ThemeContext';
import { systemApi, NetworkInfoDto } from '../services/api';

export default function Layout() {
  const { user, logout } = useAuth();
  const { connected } = useSignalR();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();

  const [showTvModal, setShowTvModal] = useState(false);
  const [networkInfo, setNetworkInfo] = useState<NetworkInfoDto | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (showTvModal) {
      void systemApi.getNetworkInfo().then(setNetworkInfo).catch(() => {
        setNetworkInfo({
          success: false,
          lanIp: window.location.hostname || 'localhost',
          port: 5005,
          localUrl: 'http://localhost:5005',
          tvUrl: `http://${window.location.hostname || 'localhost'}:5005/display`,
          spectroPdfPath: 'Desktop\\Spectro PDF',
        });
      });
    }
  }, [showTvModal]);

  const handleCopy = () => {
    if (networkInfo?.tvUrl) {
      void navigator.clipboard.writeText(networkInfo.tvUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleLogout = () => {
    void logout().then(() => navigate('/login'));
  };

  return (
    <div className="app-shell">
      <aside
        className="sidebar d-flex flex-column flex-shrink-0 p-3"
        style={{ width: '230px' }}
      >
        <div className="sidebar-header d-flex align-items-center justify-content-between mb-3 text-white">
          <div className="d-flex align-items-center">
            <img
              src="/logo.png"
              alt="Logo"
              style={{ width: '38px', height: '26px', objectFit: 'contain', marginRight: '10px' }}
              onError={(e) => {
                const target = e.currentTarget;
                if (target.src.endsWith('/logo.png')) {
                  target.src = '/logo.svg';
                } else {
                  target.style.display = 'none';
                }
              }}
            />
            <div>
              <div className="fw-bold" style={{ color: 'var(--fm-text)' }}>Chemistry monitor</div>
              <small className="fm-muted">Plant Control</small>
            </div>
          </div>
        </div>
        <ul className="nav nav-pills flex-column mb-auto">
          <li className="nav-item">
            <NavLink to="/" className="nav-link px-2 py-2">
              <i className="bi bi-grid-1x2 me-2" />
              Dashboard
            </NavLink>
          </li>
          {user?.role !== 'Viewer' && (
            <li className="nav-item">
              <NavLink to="/operator" className="nav-link px-2 py-2">
                <i className="bi bi-sliders me-2" />
                Operator Panel
              </NavLink>
            </li>
          )}
          {user?.role !== 'Operator' && (
            <li className="nav-item">
              <NavLink to="/history" className="nav-link px-2 py-2">
                <i className="bi bi-clock-history me-2" />
                History
              </NavLink>
            </li>
          )}
          {user?.role === 'Admin' && (
            <li className="nav-item">
              <NavLink to="/admin" className="nav-link px-2 py-2">
                <i className="bi bi-people me-2" />
                Admin Panel
              </NavLink>
            </li>
          )}
          {user?.role !== 'Operator' && (
            <li className="nav-item">
              <NavLink to="/settings" className="nav-link px-2 py-2">
                <i className="bi bi-gear me-2" />
                Settings
              </NavLink>
            </li>
          )}
          <li className="nav-item">
            <NavLink to="/display" className="nav-link px-2 py-2" target="_blank">
              <i className="bi bi-display me-2" />
              Open Display
            </NavLink>
          </li>
          <li className="nav-item mt-2">
            <button
              type="button"
              className="btn btn-outline-info btn-sm w-100 d-flex align-items-center justify-content-between px-2 py-2"
              onClick={() => setShowTvModal(true)}
              style={{
                borderRadius: '8px',
                fontSize: '0.82rem',
                fontWeight: 600,
                borderStyle: 'dashed'
              }}
              title="Get Smart TV LAN URL and Spectro PDF Watch status"
            >
              <span>
                <i className="bi bi-broadcast me-2 text-warning" />
                Smart TV LAN Link
              </span>
              <span className="badge bg-warning text-dark" style={{ fontSize: '0.62rem' }}>LAN</span>
            </button>
          </li>
        </ul>

        <div className="sidebar-footer mb-3 px-1">
          <button
            className="btn btn-sm w-100 d-flex align-items-center justify-content-between px-3 py-2"
            onClick={toggleTheme}
            style={{
              background: 'var(--theme-accent-subtle)',
              border: '1px solid var(--theme-border)',
              color: 'var(--fm-text)',
              borderRadius: '8px',
              fontWeight: 500,
            }}
            title="Toggle Light / Dark Theme"
          >
            <span className="d-flex align-items-center">
              <i
                className={`bi ${theme === 'dark' ? 'bi-moon-stars-fill text-info' : 'bi-sun-fill text-warning'} me-2`}
              />
              {theme === 'dark' ? 'Deep Space' : 'Airy Zenith'}
            </span>
            <span className="badge bg-secondary opacity-75 text-uppercase" style={{ fontSize: '0.65rem' }}>
              {theme}
            </span>
          </button>
        </div>

        <hr className="sidebar-footer text-secondary my-2" />
        <div className="sidebar-footer small text-white">
          <div className="fw-semibold">{user?.displayName}</div>
          <div className="fm-muted">
            {user?.role}
            <span
              className={`ms-2 badge ${connected ? 'text-bg-success' : 'text-bg-danger'}`}
              title={connected ? 'SignalR connected' : 'SignalR disconnected'}
            >
              {connected ? 'LIVE' : 'OFFLINE'}
            </span>
          </div>
          <button className="btn btn-outline-secondary btn-sm mt-2 w-100" onClick={handleLogout}>
            <i className="bi bi-box-arrow-right me-1" /> Logout
          </button>
        </div>
      </aside>

      <main className="main-content">
        <Outlet />
      </main>

      {/* --- SMART TV LAN LINK MODAL --- */}
      {showTvModal && (
        <div
          className="modal fade show d-block"
          tabIndex={-1}
          style={{ backgroundColor: 'rgba(0, 0, 0, 0.75)', backdropFilter: 'blur(5px)', zIndex: 1050 }}
        >
          <div className="modal-dialog modal-dialog-centered modal-lg">
            <div
              className="modal-content shadow-lg"
              style={{
                backgroundColor: 'var(--theme-bg-card, #1e293b)',
                color: 'var(--theme-text-primary, #f8fafc)',
                border: '1px solid var(--theme-accent, #00e5ff)',
                borderRadius: '16px',
              }}
            >
              <div className="modal-header border-bottom border-secondary d-flex align-items-center justify-content-between p-3">
                <h5 className="modal-title fw-bold d-flex align-items-center gap-2 m-0 text-info">
                  <i className="bi bi-broadcast fs-4 text-warning"></i>
                  Smart TV LAN Connection & Setup
                </h5>
                <button
                  type="button"
                  className="btn-close btn-close-white"
                  onClick={() => setShowTvModal(false)}
                />
              </div>

              <div className="modal-body p-4">
                {/* TV Link Banner */}
                <div className="mb-4 p-3 rounded" style={{ background: 'rgba(0, 229, 255, 0.08)', border: '1px solid rgba(0, 229, 255, 0.3)' }}>
                  <label className="text-info fw-bold small text-uppercase mb-1 d-block" style={{ letterSpacing: '0.08em' }}>
                    Smart TV Web Browser Link (Over LAN):
                  </label>
                  <div className="d-flex align-items-center gap-2">
                    <input
                      type="text"
                      readOnly
                      className="form-control form-control-lg fw-bold text-warning"
                      style={{
                        background: 'rgba(0, 0, 0, 0.4)',
                        border: '1px solid rgba(0, 229, 255, 0.5)',
                        fontFamily: 'monospace',
                        fontSize: '1.25rem',
                        letterSpacing: '0.04em'
                      }}
                      value={networkInfo?.tvUrl || 'Loading TV URL...'}
                    />
                    <button
                      className={`btn ${copied ? 'btn-success' : 'btn-info'} px-3 d-flex align-items-center gap-1`}
                      onClick={handleCopy}
                      style={{ whiteSpace: 'nowrap', height: '48px' }}
                    >
                      <i className={`bi ${copied ? 'bi-check-lg' : 'bi-clipboard'}`}></i>
                      {copied ? 'Copied!' : 'Copy Link'}
                    </button>
                    <a
                      href={networkInfo?.tvUrl || '/display'}
                      target="_blank"
                      rel="noreferrer"
                      className="btn btn-outline-light px-3 d-flex align-items-center"
                      style={{ whiteSpace: 'nowrap', height: '48px' }}
                    >
                      <i className="bi bi-box-arrow-up-right"></i>
                    </a>
                  </div>
                </div>

                {/* Instructions */}
                <div className="row g-3 mb-4">
                  <div className="col-md-6">
                    <div className="p-3 rounded h-100" style={{ background: 'rgba(255, 255, 255, 0.04)', border: '1px solid rgba(255, 255, 255, 0.1)' }}>
                      <h6 className="fw-bold text-white mb-2 d-flex align-items-center gap-2">
                        <i className="bi bi-tv-fill text-warning"></i> How to open on Smart TV:
                      </h6>
                      <ol className="small mb-0 ps-3" style={{ lineHeight: '1.7', opacity: 0.9 }}>
                        <li>Connect the Smart TV to the <strong>same LAN Ethernet or Wi-Fi</strong>.</li>
                        <li>Open the TV's <strong>Web Browser</strong> (Samsung, LG, Sony, Android TV).</li>
                        <li>Type the URL above into the address bar.</li>
                        <li>Bookmark it and click <strong>Fullscreen</strong> on the TV.</li>
                      </ol>
                    </div>
                  </div>

                  <div className="col-md-6">
                    <div className="p-3 rounded h-100" style={{ background: 'rgba(255, 255, 255, 0.04)', border: '1px solid rgba(255, 255, 255, 0.1)' }}>
                      <h6 className="fw-bold text-white mb-2 d-flex align-items-center gap-2">
                        <i className="bi bi-file-earmark-pdf-fill text-danger"></i> Spectro PDF Watch Folder:
                      </h6>
                      <p className="small mb-2" style={{ opacity: 0.9 }}>
                        Save or drop spectrometer test PDF files directly into this Desktop folder for instant automated parsing:
                      </p>
                      <div
                        className="p-2 rounded small fw-bold text-break"
                        style={{ background: 'rgba(0, 0, 0, 0.4)', border: '1px solid rgba(255, 255, 255, 0.2)', fontFamily: 'monospace' }}
                      >
                        {networkInfo?.spectroPdfPath || 'Desktop\\Spectro PDF'}
                      </div>
                    </div>
                  </div>
                </div>

                {/* Network diagnostics status */}
                <div className="d-flex justify-content-between align-items-center small text-secondary border-top pt-2">
                  <span>Host IP: <strong className="text-white">{networkInfo?.lanIp || '127.0.0.1'}</strong> | Port: <strong className="text-white">{networkInfo?.port || 5005}</strong></span>
                  <span className="badge bg-success text-white">Listening on All Interfaces (0.0.0.0)</span>
                </div>
              </div>

              <div className="modal-footer border-top border-secondary p-2">
                <button
                  type="button"
                  className="btn btn-secondary px-4"
                  onClick={() => setShowTvModal(false)}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}


