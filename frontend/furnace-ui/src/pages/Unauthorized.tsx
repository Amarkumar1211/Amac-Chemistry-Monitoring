import { useNavigate } from 'react-router-dom';

export default function Unauthorized() {
  const navigate = useNavigate();
  return (
    <div className="d-flex flex-column align-items-center justify-content-center vh-100">
      <i className="bi bi-shield-lock" style={{ fontSize: '3rem', color: 'var(--fm-warn)' }} />
      <h4 className="mt-3 text-white">403 — Access Denied</h4>
      <p className="fm-muted">You do not have permission to view this page.</p>
      <button className="btn btn-fm" onClick={() => navigate('/')}>
        Back to Dashboard
      </button>
    </div>
  );
}

