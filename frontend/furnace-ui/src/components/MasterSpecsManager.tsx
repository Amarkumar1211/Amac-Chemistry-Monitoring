import React, { useState, useEffect } from 'react';
import { partSpecsApi, PartSpecDto } from '../services/api';
import SpecEditorModal from './SpecEditorModal';

const STANDARD_CHEMICALS = ['Si', 'Fe', 'Cu', 'Mn', 'Mg', 'Zn', 'Ti', 'Sr', 'Pb', 'Sn', 'Ni', 'Cr'];

export default function MasterSpecsManager() {
  const [specs, setSpecs] = useState<PartSpecDto[]>([]);
  const [msg, setMsg] = useState('');
  const [busy, setBusy] = useState(false);
  const [expandedRows, setExpandedRows] = useState<Set<number>>(new Set());

  const [showEditor, setShowEditor] = useState(false);
  const [editingSpec, setEditingSpec] = useState<PartSpecDto | null>(null);

  const [customSearch, setCustomSearch] = useState('');
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  useEffect(() => {
    void loadSpecs();
  }, []);

  const loadSpecs = async () => {
    try {
      setSpecs(await partSpecsApi.getAll());
    } catch (err) {
      setMsg((err as Error).message);
    }
  };

  const deleteSpec = async (id: number) => {
    if (!window.confirm('Delete this specification?')) return;
    try {
      await partSpecsApi.delete(id);
      void loadSpecs();
    } catch (err) {
      setMsg((err as Error).message);
    }
  };

  const handleDeleteAll = async () => {
    if (!window.confirm('Are you sure you want to delete ALL specifications? This cannot be undone.')) return;
    try {
      await partSpecsApi.deleteAll();
      setMsg('All specifications deleted successfully.');
      void loadSpecs();
    } catch (err) {
      setMsg((err as Error).message);
    }
  };

  const performUpload = async (file: File, force: boolean) => {
    await partSpecsApi.uploadExcel(file, force);
    setMsg('Excel uploaded successfully!');
    void loadSpecs();
    setBusy(false);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    setMsg('Uploading...');

    try {
      await performUpload(file, false);
    } catch (err: any) {
      if (err.message === 'CONFLICT_EXISTING') {
        setBusy(false);
        if (window.confirm('Some Part Specifications (Name, No, Grade) already exist in the database. Do you want to replace them?')) {
          setBusy(true);
          setMsg('Overwriting existing specifications...');
          try {
            await performUpload(file, true);
          } catch (overwriteErr) {
            setMsg((overwriteErr as Error).message || 'Upload failed');
            setBusy(false);
            if (fileInputRef.current) fileInputRef.current.value = '';
          }
        } else {
          setMsg('Upload cancelled by user.');
          if (fileInputRef.current) fileInputRef.current.value = '';
        }
      } else {
        setMsg((err as Error).message || 'Upload failed');
        setBusy(false);
        if (fileInputRef.current) fileInputRef.current.value = '';
      }
    }
  };

  const handleDownload = () => {
    partSpecsApi.exportExcel().catch((err) => setMsg((err as Error).message));
  };

  const handleDownloadTemplate = () => {
    partSpecsApi.downloadTemplate().catch((err) => setMsg((err as Error).message));
  };

  const toggleRow = (id: number) => {
    const newExpanded = new Set(expandedRows);
    if (newExpanded.has(id)) {
      newExpanded.delete(id);
    } else {
      newExpanded.add(id);
    }
    setExpandedRows(newExpanded);
  };

  const handleAddClick = () => {
    setEditingSpec(null);
    setShowEditor(true);
  };

  const handleEditClick = (e: React.MouseEvent, spec: PartSpecDto) => {
    e.stopPropagation();
    setEditingSpec(spec);
    setShowEditor(true);
  };

  const handleSaveSpec = async (data: Partial<PartSpecDto>) => {
    if (editingSpec) {
      await partSpecsApi.update(editingSpec.id, data);
      setMsg('Specification updated successfully!');
    } else {
      await partSpecsApi.create(data);
      setMsg('Specification added successfully!');
    }
    setShowEditor(false);
    void loadSpecs();
  };

  const getChemicalTargets = (spec: PartSpecDto) => {
    const minTargets = spec.minTargetsJson ? JSON.parse(spec.minTargetsJson) : {};
    const maxTargets = spec.maxTargetsJson ? JSON.parse(spec.maxTargetsJson) : {};
    const custMinTargets = spec.customerMinTargetsJson ? JSON.parse(spec.customerMinTargetsJson) : {};
    const custMaxTargets = spec.customerMaxTargetsJson ? JSON.parse(spec.customerMaxTargetsJson) : {};
    return { minTargets, maxTargets, custMinTargets, custMaxTargets };
  };

  const filteredSpecs = specs.filter(s =>
    s.partName?.toLowerCase().includes(customSearch.toLowerCase()) ||
    s.partNo?.toLowerCase().includes(customSearch.toLowerCase()) ||
    s.gradeName?.toLowerCase().includes(customSearch.toLowerCase())
  );

  return (
    <div className="container-fluid m-0 p-3 h-100 d-flex flex-column">
      <div className="col-12 p-0">
        <div className="fm-panel p-0 table-responsive position-relative">
          <div className="d-flex justify-content-between align-items-center p-3 border-bottom border-secondary">
            <h6 className="mb-0" style={{ color: 'var(--bs-body-color)' }}>Custom Specifications</h6>
            <div className="d-flex align-items-center gap-2">
              <div className="input-group" style={{ width: '250px' }}>
                <span className="input-group-text bg-transparent border-secondary text-muted">
                  <i className="bi bi-search"></i>
                </span>
                <input
                  type="text"
                  className="form-control border-secondary bg-transparent"
                  placeholder="Search Specs..."
                  value={customSearch}
                  onChange={(e) => setCustomSearch(e.target.value)}
                  style={{ color: 'var(--bs-body-color)' }}
                />
              </div>
              <input
                type="file"
                accept=".xlsx"
                className="d-none"
                ref={fileInputRef}
                onChange={handleUpload}
              />
              <button
                className="btn btn-sm btn-outline-success"
                onClick={handleAddClick}
                disabled={busy}
              >
                <i className="bi bi-plus-lg me-2" />
                Add Specification
              </button>
              <button
                className="btn btn-sm btn-outline-success"
                onClick={handleDownloadTemplate}
                disabled={busy}
                title="Download a blank template to fill out"
              >
                <i className="bi bi-file-earmark-excel me-2" />
                Template
              </button>
              <button
                className="btn btn-sm btn-outline-primary"
                onClick={() => fileInputRef.current?.click()}
                disabled={busy}
              >
                <i className="bi bi-upload me-2" />
                Upload
              </button>
              <button className="btn btn-sm btn-outline-info" onClick={handleDownload} disabled={busy}>
                <i className="bi bi-download me-2" />
                Download
              </button>
              <button
                className="btn btn-sm btn-outline-danger"
                onClick={handleDeleteAll}
                disabled={busy || specs.length === 0}
                title="Delete all specifications"
              >
                <i className="bi bi-trash me-2" />
                Delete All
              </button>
            </div>
          </div>

          {msg && <div className="alert alert-info m-3 py-2">{msg}</div>}

          <table className="table table-hover align-middle mb-0 fm-table">
            <thead>
              <tr>
                <th style={{ width: '40px' }}></th>
                <th>Part No</th>
                <th>Part Name</th>
                <th>Grade</th>
                <th>Color</th>
                <th className="text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredSpecs.map((s) => {
                const isExpanded = expandedRows.has(s.id);
                const targets = isExpanded ? getChemicalTargets(s) : null;

                return (
                  <React.Fragment key={s.id}>
                    <tr onClick={() => toggleRow(s.id)} style={{ cursor: 'pointer' }} className={isExpanded ? 'table-active' : ''}>
                      <td className="text-center text-muted">
                        <i className={`bi bi-chevron-${isExpanded ? 'down' : 'right'}`}></i>
                      </td>
                      <td className="fw-semibold">{s.partNo}</td>
                      <td>{s.partName}</td>
                      <td>{s.gradeName}</td>
                      <td>{s.colorCode || '-'}</td>
                      <td className="text-end">
                        <button className="btn btn-sm btn-outline-primary me-2" onClick={(e) => handleEditClick(e, s)}>
                          Edit
                        </button>
                        <button className="btn btn-sm btn-outline-danger" onClick={(e) => { e.stopPropagation(); void deleteSpec(s.id); }}>
                          Delete
                        </button>
                      </td>
                    </tr>
                    {isExpanded && targets && (
                      <tr>
                        <td colSpan={6} className="p-0 border-0">
                          <div className="p-3 border-bottom" style={{ backgroundColor: 'var(--fm-panel, var(--theme-bg-card, #131B2B))', borderColor: 'var(--fm-border, rgba(255, 255, 255, 0.12))' }}>
                            <div className="d-flex align-items-center justify-content-between mb-2">
                              <h6 className="mb-0 fs-7 fw-bold d-flex align-items-center gap-1.5" style={{ color: 'var(--fm-text, #F1F5F9)' }}>
                                <i className="bi bi-calculator text-primary"></i>
                                <span>Chemical Targets (%)</span>
                              </h6>
                              <span className="badge bg-secondary bg-opacity-25" style={{ color: 'var(--fm-muted, #94A3B8)', fontSize: '0.7rem' }}>
                                Spec ID #{s.id}
                              </span>
                            </div>
                            <div className="table-responsive rounded-2 border" style={{ borderColor: 'var(--fm-border, rgba(255, 255, 255, 0.12))' }}>
                              <table className="table table-sm table-bordered mb-0 text-center align-middle" style={{ fontSize: '0.84rem', borderColor: 'var(--fm-border, rgba(255, 255, 255, 0.12))' }}>
                                <thead style={{ backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.04))' }}>
                                  <tr>
                                    <th style={{ color: 'var(--fm-text, #F1F5F9)', width: '90px' }}>Type</th>
                                    {STANDARD_CHEMICALS.map(chem => <th key={chem} style={{ color: 'var(--fm-accent, #00E5FF)' }}>{chem}</th>)}
                                  </tr>
                                </thead>
                                <tbody>
                                  <tr>
                                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>Our Min</td>
                                    {STANDARD_CHEMICALS.map(chem => (
                                      <td key={chem} className="fw-bold" style={{ color: targets.minTargets[chem] !== undefined ? 'var(--fm-text, #F1F5F9)' : 'var(--fm-muted, #94A3B8)' }}>
                                        {targets.minTargets[chem] !== undefined ? targets.minTargets[chem] : '—'}
                                      </td>
                                    ))}
                                  </tr>
                                  <tr>
                                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>Our Max</td>
                                    {STANDARD_CHEMICALS.map(chem => (
                                      <td key={chem} className="fw-bold" style={{ color: targets.maxTargets[chem] !== undefined ? 'var(--fm-text, #F1F5F9)' : 'var(--fm-muted, #94A3B8)' }}>
                                        {targets.maxTargets[chem] !== undefined ? targets.maxTargets[chem] : '—'}
                                      </td>
                                    ))}
                                  </tr>
                                  <tr>
                                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>Cust. Min</td>
                                    {STANDARD_CHEMICALS.map(chem => (
                                      <td key={chem} className="fw-bold" style={{ color: targets.custMinTargets[chem] !== undefined ? 'var(--fm-text, #F1F5F9)' : 'var(--fm-muted, #94A3B8)' }}>
                                        {targets.custMinTargets[chem] !== undefined ? targets.custMinTargets[chem] : '—'}
                                      </td>
                                    ))}
                                  </tr>
                                  <tr>
                                    <td className="text-start fw-bold ps-2.5" style={{ color: 'var(--fm-text, #F1F5F9)', backgroundColor: 'var(--theme-bg-header, rgba(255, 255, 255, 0.05))', whiteSpace: 'nowrap' }}>Cust. Max</td>
                                    {STANDARD_CHEMICALS.map(chem => (
                                      <td key={chem} className="fw-bold" style={{ color: targets.custMaxTargets[chem] !== undefined ? 'var(--fm-text, #F1F5F9)' : 'var(--fm-muted, #94A3B8)' }}>
                                        {targets.custMaxTargets[chem] !== undefined ? targets.custMaxTargets[chem] : '—'}
                                      </td>
                                    ))}
                                  </tr>
                                </tbody>
                              </table>
                            </div>
                          </div>
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })}
              {filteredSpecs.length === 0 && (
                <tr>
                  <td colSpan={6} className="text-center text-muted py-4">No custom specifications found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showEditor && (
        <SpecEditorModal
          spec={editingSpec}
          onClose={() => setShowEditor(false)}
          onSave={handleSaveSpec}
        />
      )}
    </div>
  );
}
