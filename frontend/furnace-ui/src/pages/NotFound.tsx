import { useNavigate } from 'react-router-dom';

export default function NotFound() {
  const navigate = useNavigate();
  return (
    <div className="d-flex flex-column align-items-center justify-content-center vh-100">
      <div className="display-1 fw-bold" style={{ color: 'var(--fm-muted)' }}>
        404
      </div>
      <p className="fm-muted">Page not found</p>
      <button className="btn btn-fm" onClick={() => navigate('/')}>
        Back to Dashboard
      </button>
    </div>
  );
}

