import { useRef, useState } from 'react';
import { ParsedSpectroResultDto } from '../types';
import api from '../services/api';

interface PdfUploaderProps {
  onParseSuccess: (data: ParsedSpectroResultDto) => void;
}

export default function PdfUploader({ onParseSuccess }: PdfUploaderProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.type !== 'application/pdf') {
      setError('Please upload a valid PDF file.');
      return;
    }

    setError(null);
    setLoading(true);

    const formData = new FormData();
    formData.append('file', file);

    try {
      const response = await api.post('/pdf-upload/parse', formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      });
      onParseSuccess(response.data);
    } catch (err: any) {
      if (err.response?.data?.message) {
        setError(err.response.data.message);
      } else {
        setError(err.message || 'An error occurred during upload.');
      }
    } finally {
      setLoading(false);
      // Reset input so the same file can be selected again
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  return (
    <div className="pdf-upload-banner mb-2 d-flex align-items-center justify-content-between">
      <div className="d-flex align-items-center gap-2.5">
        <button
          type="button"
          className="btn-pdf-trigger d-flex align-items-center gap-1.5"
          onClick={() => fileInputRef.current?.click()}
          disabled={loading}
        >
          <i className="bi bi-file-earmark-arrow-up-fill text-warning"></i>
          <span>{loading ? 'Analyzing Spectro PDF...' : 'Upload Spectro PDF'}</span>
        </button>
        <input
          type="file"
          accept="application/pdf"
          ref={fileInputRef}
          onChange={handleFileChange}
          style={{ display: 'none' }}
        />
        {error ? (
          <span className="text-danger small fw-bold d-flex align-items-center gap-1">
            <i className="bi bi-exclamation-circle-fill"></i> {error}
          </span>
        ) : loading ? (
          <span className="text-primary small fw-semibold d-flex align-items-center gap-1.5">
            <span className="spinner-border spinner-border-sm" role="status"></span>
            Parsing chemical composition...
          </span>
        ) : (
          <span className="text-fm-muted small d-none d-sm-inline" style={{ fontSize: '0.76rem' }}>
            <i className="bi bi-magic text-primary me-1"></i> Auto-fills Batch Code, Furnace, & Calculated Additions
          </span>
        )}
      </div>
      <span className="badge pdf-ai-badge">
        <i className="bi bi-cpu me-1"></i> Spectro Parser
      </span>
    </div>
  );
}
