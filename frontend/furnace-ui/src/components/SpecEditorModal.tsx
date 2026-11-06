import React, { useState, useEffect } from 'react';
import { PartSpecDto } from '../services/api';

const STANDARD_CHEMICALS = ['Si', 'Fe', 'Cu', 'Mn', 'Mg', 'Zn', 'Ti', 'Sr', 'Pb', 'Sn', 'Ni', 'Cr'];

interface SpecEditorModalProps {
  spec: PartSpecDto | null;
  onClose: () => void;
  onSave: (data: Partial<PartSpecDto>) => Promise<void>;
}

export default function SpecEditorModal({ spec, onClose, onSave }: SpecEditorModalProps) {
  const isEdit = !!spec;
  const [partNo, setPartNo] = useState('');
  const [partName, setPartName] = useState('');
  const [gradeName, setGradeName] = useState('');
  const [colorCode, setColorCode] = useState('');

  const [minTargets, setMinTargets] = useState<Record<string, string>>({});
  const [maxTargets, setMaxTargets] = useState<Record<string, string>>({});
  const [custMinTargets, setCustMinTargets] = useState<Record<string, string>>({});
  const [custMaxTargets, setCustMaxTargets] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (spec) {
      setPartNo(spec.partNo);
      setPartName(spec.partName);
      setGradeName(spec.gradeName);
      setColorCode(spec.colorCode || '');

      try {
        setMinTargets(spec.minTargetsJson ? JSON.parse(spec.minTargetsJson) : {});
        setMaxTargets(spec.maxTargetsJson ? JSON.parse(spec.maxTargetsJson) : {});
        setCustMinTargets(spec.customerMinTargetsJson ? JSON.parse(spec.customerMinTargetsJson) : {});
        setCustMaxTargets(spec.customerMaxTargetsJson ? JSON.parse(spec.customerMaxTargetsJson) : {});
      } catch (e) {
        console.error(e);
      }
    }
  }, [spec]);

  const handleSave = async () => {
    if (!partNo || !partName || !gradeName) {
      setError('Part No, Part Name, and Grade are required.');
      return;
    }

    // Clean up empty strings
    const cleanTargets = (targets: Record<string, string>) => {
      const clean: Record<string, number> = {};
      for (const [k, v] of Object.entries(targets)) {
        if (v && !isNaN(Number(v))) {
          clean[k] = Number(v);
        }
      }
      return clean;
    };

    const payload: Partial<PartSpecDto> = {
      partNo,
      partName,
      gradeName,
      colorCode: colorCode || undefined,
      minTargetsJson: JSON.stringify(cleanTargets(minTargets)),
      maxTargetsJson: JSON.stringify(cleanTargets(maxTargets)),
      customerMinTargetsJson: JSON.stringify(cleanTargets(custMinTargets)),
      customerMaxTargetsJson: JSON.stringify(cleanTargets(custMaxTargets))
    };

    setBusy(true);
    setError('');
    try {
      await onSave(payload);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const handleTargetChange = (chem: string, type: 'min' | 'max' | 'custMin' | 'custMax', value: string) => {
    if (type === 'min') setMinTargets(prev => ({ ...prev, [chem]: value }));
    else if (type === 'max') setMaxTargets(prev => ({ ...prev, [chem]: value }));
    else if (type === 'custMin') setCustMinTargets(prev => ({ ...prev, [chem]: value }));
    else setCustMaxTargets(prev => ({ ...prev, [chem]: value }));
  };

  const targetInputStyle: React.CSSProperties = {
    backgroundColor: 'var(--theme-input-bg, #0B111D)',
    color: 'var(--fm-text, #F1F5F9)',
    borderColor: 'var(--theme-input-border, rgba(255, 255, 255, 0.2))',
    fontSize: '0.85rem',
    height: '32px',
    padding: '0.2rem',
    fontWeight: 700,
  };

  return (
    <div
      className="modal d-block"
      tabIndex={-1}
      style={{
        backgroundColor: 'rgba(0,0,0,0.75)',
        backdropFilter: 'blur(5px)',
        zIndex: 1050,
      }}
    >
      <div className="modal-dialog modal-xl modal-dialog-centered">
        <div
          className="modal-content shadow-lg"
          style={{
            backgroundColor: 'var(--fm-panel, var(--theme-bg-card, #131B2B))',
            color: 'var(--fm-text, var(--theme-text-primary, #F1F5F9))',
            border: '1px solid var(--fm-border, rgba(0, 229, 255, 0.25))',
            borderRadius: '14px',
          }}
        >
          {/* Modal Header */}
          <div
            className="modal-header d-flex align-items-center justify-content-between p-3"
            style={{
              borderBottom: '1px solid var(--fm-border, rgba(255, 255, 255, 0.12))',
              backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.02))',
            }}
          >
            <h5
              className="modal-title fw-bold d-flex align-items-center gap-2 m-0"
              style={{ color: 'var(--fm-text, #F1F5F9)', fontSize: '1.1rem' }}
            >
              <i className="bi bi-pencil-square text-info"></i>
              <span>{isEdit ? 'Edit Specification' : 'Add Specification'}</span>
            </h5>
            <button
              type="button"
              className="btn-close"
              onClick={onClose}
              disabled={busy}
              style={{ filter: 'var(--theme-btn-close-filter, invert(1))' }}
            ></button>
          </div>

          {/* Modal Body */}
          <div className="modal-body p-3.5">
            {error && (
              <div className="alert alert-danger py-2 px-3 mb-3 small d-flex align-items-center gap-2">
                <i className="bi bi-exclamation-triangle-fill"></i>
                <span>{error}</span>
              </div>
            )}

            {/* Top Row Inputs */}
            <div className="row g-3 mb-4">
              <div className="col-md-3">
                <label className="form-label mb-1 fw-semibold" style={{ color: 'var(--fm-muted, #94A3B8)', fontSize: '0.8rem' }}>
                  Part No *
                </label>
                <input
                  type="text"
                  className="form-control form-control-sm op-select"
                  style={{
                    backgroundColor: 'var(--theme-input-bg, #0B111D)',
                    color: 'var(--fm-text, #F1F5F9)',
                    borderColor: 'var(--theme-input-border, #1E293B)',
                    fontSize: '0.85rem',
                    fontWeight: 600,
                  }}
                  value={partNo}
                  onChange={e => setPartNo(e.target.value)}
                  disabled={busy}
                  placeholder="e.g. 1465C01139,1465C01153"
                />
              </div>
              <div className="col-md-4">
                <label className="form-label mb-1 fw-semibold" style={{ color: 'var(--fm-muted, #94A3B8)', fontSize: '0.8rem' }}>
                  Part Name *
                </label>
                <input
                  type="text"
                  className="form-control form-control-sm op-select"
                  style={{
                    backgroundColor: 'var(--theme-input-bg, #0B111D)',
                    color: 'var(--fm-text, #F1F5F9)',
                    borderColor: 'var(--theme-input-border, #1E293B)',
                    fontSize: '0.85rem',
                    fontWeight: 600,
                  }}
                  value={partName}
                  onChange={e => setPartName(e.target.value)}
                  disabled={busy}
                  placeholder="e.g. CP4"
                />
              </div>
              <div className="col-md-3">
                <label className="form-label mb-1 fw-semibold" style={{ color: 'var(--fm-muted, #94A3B8)', fontSize: '0.8rem' }}>
                  Grade *
                </label>
                <input
                  type="text"
                  className="form-control form-control-sm op-select"
                  style={{
                    backgroundColor: 'var(--theme-input-bg, #0B111D)',
                    color: 'var(--fm-text, #F1F5F9)',
                    borderColor: 'var(--theme-input-border, #1E293B)',
                    fontSize: '0.85rem',
                    fontWeight: 600,
                  }}
                  value={gradeName}
                  onChange={e => setGradeName(e.target.value)}
                  disabled={busy}
                  placeholder="e.g. AlSi12CuNiMg"
                />
              </div>
              <div className="col-md-2">
                <label className="form-label mb-1 fw-semibold" style={{ color: 'var(--fm-muted, #94A3B8)', fontSize: '0.8rem' }}>
                  Color Code
                </label>
                <input
                  type="text"
                  className="form-control form-control-sm op-select"
                  style={{
                    backgroundColor: 'var(--theme-input-bg, #0B111D)',
                    color: 'var(--fm-text, #F1F5F9)',
                    borderColor: 'var(--theme-input-border, #1E293B)',
                    fontSize: '0.85rem',
                    fontWeight: 600,
                  }}
                  value={colorCode}
                  onChange={e => setColorCode(e.target.value)}
                  disabled={busy}
                  placeholder="e.g. Yellow-blue"
                />
              </div>
            </div>

            {/* Chemical Targets Section */}
            <div className="d-flex align-items-center justify-content-between mb-2">
              <h6 className="fw-bold mb-0 d-flex align-items-center gap-2" style={{ color: 'var(--fm-text, #F1F5F9)', fontSize: '0.92rem' }}>
                <i className="bi bi-calculator text-primary"></i>
                <span>Chemical Targets (%)</span>
              </h6>
              <span className="badge bg-secondary bg-opacity-25 text-fm border border-fm border-opacity-20" style={{ fontSize: '0.7rem' }}>
                Master Specification Limits
              </span>
            </div>

            <div className="table-responsive rounded-2 border" style={{ borderColor: 'var(--fm-border, rgba(255, 255, 255, 0.12))' }}>
              <table className="table table-bordered mb-0 text-center align-middle" style={{ fontSize: '0.82rem', borderColor: 'var(--fm-border, rgba(255, 255, 255, 0.12))' }}>
                <thead style={{ backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.04))' }}>
                  <tr>
                    <th style={{ color: 'var(--fm-text, #F1F5F9)', width: '90px', padding: '0.55rem' }}>Type</th>
                    {STANDARD_CHEMICALS.map(c => (
                      <th key={c} style={{ color: 'var(--fm-accent, #00E5FF)', minWidth: '58px', padding: '0.55rem' }}>{c}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>
                      Our Min
                    </td>
                    {STANDARD_CHEMICALS.map(chem => (
                      <td key={chem} className="p-1" style={{ backgroundColor: 'var(--fm-panel, transparent)' }}>
                        <input
                          type="number"
                          step="any"
                          className="form-control form-control-sm text-center fw-bold spec-target-input"
                          style={targetInputStyle}
                          value={minTargets[chem] || ''}
                          onChange={e => handleTargetChange(chem, 'min', e.target.value)}
                          disabled={busy}
                          placeholder="—"
                        />
                      </td>
                    ))}
                  </tr>
                  <tr>
                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>
                      Our Max
                    </td>
                    {STANDARD_CHEMICALS.map(chem => (
                      <td key={chem} className="p-1" style={{ backgroundColor: 'var(--fm-panel, transparent)' }}>
                        <input
                          type="number"
                          step="any"
                          className="form-control form-control-sm text-center fw-bold spec-target-input"
                          style={targetInputStyle}
                          value={maxTargets[chem] || ''}
                          onChange={e => handleTargetChange(chem, 'max', e.target.value)}
                          disabled={busy}
                          placeholder="—"
                        />
                      </td>
                    ))}
                  </tr>
                  <tr>
                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>
                      Cust Min
                    </td>
                    {STANDARD_CHEMICALS.map(chem => (
                      <td key={chem} className="p-1" style={{ backgroundColor: 'var(--fm-panel, transparent)' }}>
                        <input
                          type="number"
                          step="any"
                          className="form-control form-control-sm text-center fw-bold spec-target-input"
                          style={targetInputStyle}
                          value={custMinTargets[chem] || ''}
                          onChange={e => handleTargetChange(chem, 'custMin', e.target.value)}
                          disabled={busy}
                          placeholder="—"
                        />
                      </td>
                    ))}
                  </tr>
                  <tr>
                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>
                      Cust Max
                    </td>
                    {STANDARD_CHEMICALS.map(chem => (
                      <td key={chem} className="p-1" style={{ backgroundColor: 'var(--fm-panel, transparent)' }}>
                        <input
                          type="number"
                          step="any"
                          className="form-control form-control-sm text-center fw-bold spec-target-input"
                          style={targetInputStyle}
                          value={custMaxTargets[chem] || ''}
                          onChange={e => handleTargetChange(chem, 'custMax', e.target.value)}
                          disabled={busy}
                          placeholder="—"
                        />
                      </td>
                    ))}
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          {/* Modal Footer */}
          <div
            className="modal-footer p-2.5 d-flex justify-content-end gap-2"
            style={{
              borderTop: '1px solid var(--fm-border, rgba(255, 255, 255, 0.12))',
              backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.02))',
            }}
          >
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary px-3"
              style={{ height: '36px' }}
              onClick={onClose}
              disabled={busy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-sm btn-primary px-4 fw-bold d-flex align-items-center gap-1.5"
              style={{ height: '36px' }}
              onClick={handleSave}
              disabled={busy}
            >
              {busy ? (
                <><span className="spinner-border spinner-border-sm me-1" /> Saving...</>
              ) : (
                <><i className="bi bi-check2-circle"></i> Save Specification</>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
