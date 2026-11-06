import { FormEvent, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

interface LocationState {
  from?: { pathname?: string };
}

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as LocationState | null)?.from?.pathname ?? '/';

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login({ userName: username.trim(), password });
      navigate(from, { replace: true });
    } catch (err) {
      setError((err as Error)?.message || 'Login failed. Check credentials.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      className="d-flex align-items-center justify-content-center"
      style={{ minHeight: '100vh', background: 'linear-gradient(135deg,#0f1419 0%,#16222b 100%)' }}
    >
      <div className="fm-panel p-4" style={{ width: '380px' }}>
        <div className="text-center mb-4">
          <div className="d-flex align-items-center justify-content-center mb-3">
            <div
              className="logo-space d-flex align-items-center justify-content-center shadow-sm"
              style={{
                width: '100px',
                height: '64px',
                borderRadius: '14px',
                background: 'linear-gradient(135deg, rgba(37,99,235,0.08) 0%, rgba(6,182,212,0.08) 100%)',
                border: '1.5px solid rgba(59,130,246,0.22)',
                padding: '6px 12px',
                boxShadow: '0 4px 16px -2px rgba(37,99,235,0.12)',
              }}
              title="Logo"
            >
              <img
                src="/logo.png"
                alt="Chemistry monitor logo"
                style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                onError={(e) => {
                  const target = e.currentTarget;
                  if (target.src.endsWith('/logo.png')) {
                    target.src = '/logo.svg';
                  } else {
                    target.style.display = 'none';
                    const fb = target.parentElement?.querySelector('.logo-fallback') as HTMLElement | null;
                    if (fb) fb.style.display = 'flex';
                  }
                }}
              />
              <div className="logo-fallback" style={{ display: 'none', alignItems: 'center', justifyContent: 'center' }}>
                <i className="bi bi-droplet-half fs-2 text-primary" />
              </div>
            </div>
          </div>
          <h4 className="mb-0 fw-bold" style={{ color: 'var(--fm-text)' }}>
            Chemistry monitor
          </h4>
        </div>

        {error && (
          <div className="alert alert-danger py-2" role="alert">
            <i className="bi bi-exclamation-triangle me-1" /> {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="mb-3">
            <label className="form-label" htmlFor="username">
              Username
            </label>
            <input
              id="username"
              className="form-control"
              type="text"
              value={username}
              autoComplete="username"
              onChange={(e) => setUsername(e.target.value)}
              required
            />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              className="form-control"
              type="password"
              value={password}
              autoComplete="current-password"
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          <button className="btn btn-fm w-100 py-2" type="submit" disabled={loading}>
            {loading ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" />
                Signing in…
              </>
            ) : (
              'Sign In'
            )}
          </button>
        </form>

        
        <hr className="my-4" style={{ borderColor: 'var(--fm-border)' }} />
        
        <button 
          type="button" 
          className="btn btn-outline-info w-100" 
          onClick={() => navigate('/display')}
        >
          <i className="bi bi-tv me-2"></i> Go to Display Screen
        </button>
      </div>
    </div>
  );
}

