import { useEffect, useState } from 'react';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { historyApi } from '../services/api';
import type { HistoryItem } from '../types';

const statusBadge = (s: number) => {
  if (s === 1) return 'text-bg-success';
  if (s === 2) return 'text-bg-info';
  return 'text-bg-secondary';
};

export default function History() {
  const [items, setItems] = useState<HistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [search, setSearch] = useState('');
  const [dateFilter, setDateFilter] = useState('all');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const fetchHistory = () => {
    setLoading(true);
    const params: Record<string, string> = {};
    if (search) params.batchNumber = search;

    if (dateFilter === 'custom') {
      if (startDate) {
        params.fromUtc = new Date(startDate).toISOString();
      }
      if (endDate) {
        const end = new Date(endDate);
        end.setHours(23, 59, 59, 999);
        params.toUtc = end.toISOString();
      }
    } else if (dateFilter !== 'all') {
      const now = new Date();
      let fromDate = new Date();
      if (dateFilter === 'week') fromDate.setDate(now.getDate() - 7);
      if (dateFilter === 'month') fromDate.setMonth(now.getMonth() - 1);
      if (dateFilter === 'year') fromDate.setFullYear(now.getFullYear() - 1);
      params.fromUtc = fromDate.toISOString();
      params.toUtc = now.toISOString();
    }

    historyApi
      .batches(params)
      .then(setItems)
      .catch((err) => setError((err as Error).message))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchHistory();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dateFilter, startDate, endDate]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    fetchHistory();
  };

  const highlightSearch = (text: string, query: string) => {
    if (!query.trim()) return text;
    const regex = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
    const parts = text.split(regex);
    return parts.map((part, i) =>
      regex.test(part) ? (
        <span key={i} className="bg-warning text-dark px-1 rounded">
          {part}
        </span>
      ) : (
        part
      )
    );
  };

  const formatUtcDate = (dateStr?: string) => {
    if (!dateStr) return '';
    const withZ = dateStr.endsWith('Z') ? dateStr : `${dateStr}Z`;
    return new Date(withZ).toLocaleString();
  };

  const exportPDF = () => {
    if (items.length === 0) return;

    const doc = new jsPDF('landscape');
    
    // Add title
    doc.setFontSize(18);
    doc.text('Batch History Report', 14, 22);
    
    doc.setFontSize(11);
    doc.text(`Generated on: ${new Date().toLocaleString()}`, 14, 30);

    const headers = [[
      'Batch no', 'Furnace Number', 'Inspector', 'Chemicals (Additions)', 'Actual Values', 'Start Time', 'End Time', 'Ind', 'Status'
    ]];

    const coreChems = ['Si', 'Fe', 'Cu', 'Mn', 'Mg', 'Zn', 'Ti', 'Sr', 'Pb', 'Sn', 'Ni', 'Cr'];

    const formatChemList = (itemsStr: string[]) => {
      const chunks = [];
      for (let i = 0; i < itemsStr.length; i += 4) {
        chunks.push(itemsStr.slice(i, i + 4).join(', '));
      }
      return chunks.join('\n');
    };
    
    const rows = items.map(it => {
      let parsedObj: any = null;
      let isJson = false;
      if (it.remarks && it.remarks.startsWith('{')) {
        try { parsedObj = JSON.parse(it.remarks); isJson = true; } catch {}
      }

      const additionsArr: string[] = [];
      coreChems.forEach(chem => {
          const lowerC = chem.toLowerCase();
          let val: any = 0;
          if (lowerC === 'si') val = it.si ?? 0;
          else if (lowerC === 'ti') val = it.ti ?? 0;
          else if (lowerC === 'mg') val = it.mg ?? 0;
          else if (lowerC === 'sr') val = it.sr ?? 0;
          else if (isJson && Array.isArray(parsedObj?.params)) {
            const p = parsedObj.params.find((x: any) => x.name.toLowerCase() === lowerC);
            if (p) val = p.value || 0;
          }
          if (Number(val) > 0) {
            additionsArr.push(`${chem}: ${val}`);
          }
      });
      if (additionsArr.length === 0) {
        additionsArr.push('-');
      }

      const actualsArr: string[] = [];
      coreChems.forEach(chem => {
          let val: any = 0;
          if (parsedObj?.actualValues && parsedObj.actualValues[chem] !== undefined) {
            val = parsedObj.actualValues[chem];
          } else if (isJson && Array.isArray(parsedObj?.calculationDetails)) {
            const c = parsedObj.calculationDetails.find((x: any) => x.chemical.toLowerCase() === chem.toLowerCase());
            if (c) val = c.actualValue ?? 0;
          }
          if (Number(val) > 0) {
            actualsArr.push(`${chem}: ${val}`);
          }
      });
      if (actualsArr.length === 0) {
        actualsArr.push('-');
      }

      return [
        it.batchNumber,
        it.furnaceNumber,
        it.operatorName,
        formatChemList(additionsArr),
        formatChemList(actualsArr),
        formatUtcDate(it.startedAtUtc),
        it.endedAtUtc ? formatUtcDate(it.endedAtUtc) : '',
        ['GREEN', 'YELLOW', 'RED'][it.indicator - 1] || '',
        it.status === 1 ? 'RUNNING' : it.status === 2 ? 'CLOSED' : 'CLEARED'
      ];
    });

    autoTable(doc, {
      head: headers,
      body: rows,
      startY: 36,
      styles: { fontSize: 8, cellPadding: 2, valign: 'middle' },
      headStyles: { fillColor: [41, 128, 185], textColor: 255, fontStyle: 'bold' },
      alternateRowStyles: { fillColor: [245, 247, 250] }
    });

    doc.save(`history_export_${new Date().toISOString().split('T')[0]}.pdf`);
  };

  return (
    <div className="d-flex flex-column h-100">
      <header className="mb-4 flex-shrink-0">
        <h4 className="text-white mb-0">Batch History</h4>
        <small className="fm-muted">Completed and cleared batches</small>
      </header>

      <div className="d-flex flex-wrap gap-3 mb-4 flex-shrink-0 align-items-end">
        <form onSubmit={handleSearchSubmit} className="d-flex gap-2">
          <input
            type="text"
            className="form-control"
            placeholder="Search Batch Code..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ maxWidth: '250px', background: 'var(--theme-bg-card)', color: 'var(--theme-text-primary)', border: '1px solid var(--theme-border)' }}
          />
          <button type="submit" className="btn btn-outline-info">Search</button>
        </form>
        <div className="d-flex gap-2 align-items-center ms-auto">
          <select 
            className="form-select" 
            value={dateFilter} 
            onChange={(e) => {
              setDateFilter(e.target.value);
              if (e.target.value !== 'custom') {
                setStartDate('');
                setEndDate('');
              }
            }}
            style={{ width: 'auto', background: 'var(--theme-bg-card)', color: 'var(--theme-text-primary)', border: '1px solid var(--theme-border)' }}
          >
            <option value="all">All Time</option>
            <option value="week">Past Week</option>
            <option value="month">Past Month</option>
            <option value="year">Past Year</option>
            <option value="custom">Custom Range...</option>
          </select>
          {dateFilter === 'custom' && (
            <div className="d-flex gap-2 align-items-center">
              <input 
                type="date" 
                className="form-control" 
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                title="Start Date"
                style={{ background: 'var(--theme-bg-card)', color: 'var(--theme-text-primary)', border: '1px solid var(--theme-border)' }}
              />
              <span className="text-secondary">to</span>
              <input 
                type="date" 
                className="form-control" 
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                title="End Date"
                style={{ background: 'var(--theme-bg-card)', color: 'var(--theme-text-primary)', border: '1px solid var(--theme-border)' }}
              />
            </div>
          )}
          <button className="btn btn-danger" onClick={exportPDF} disabled={items.length === 0}>
            <i className="bi bi-file-earmark-pdf me-1" /> Export PDF
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {loading && (
        <div className="d-flex justify-content-center py-5">
          <div className="spinner-border text-info" role="status" />
        </div>
      )}

      {!loading && items.length === 0 && (
        <div className="fm-panel fm-muted text-center py-5">No history found.</div>
      )}

      {!loading && items.length > 0 && (
        <div className="internal-scroll pb-2">
          <div className="fm-panel p-0 table-responsive h-100">
            <table className="table table-dark align-middle mb-0">
            <thead>
              <tr>
                <th>Batch</th>
                <th>Chemicals & Parameters</th>
                <th>Actual Values (Spectro)</th>
                <th>Furnace</th>
                <th>Operator</th>
                <th>Start</th>
                <th>End</th>
                <th>Indicator</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {items.map((it) => {
                let parsedObj: any = null;
                let isJson = false;
                if (it.remarks && it.remarks.startsWith('{')) {
                  try {
                    parsedObj = JSON.parse(it.remarks);
                    isJson = true;
                  } catch {
                    isJson = false;
                  }
                }
                
                return (
                  <tr key={it.id}>
                    <td className="fw-semibold text-nowrap">
                      {highlightSearch(it.batchNumber, search)}
                    </td>
                    <td style={{ minWidth: '350px' }}>
                      {(() => {
                        const chemsWithVal: { chem: string; val: any }[] = [];
                        const checkedChems = new Set<string>();

                        ['Si', 'Fe', 'Cu', 'Mn', 'Mg', 'Zn', 'Ti', 'Sr', 'Pb', 'Sn', 'Ni', 'Cr'].forEach(chem => {
                          const lowerC = chem.toLowerCase();
                          checkedChems.add(lowerC);
                          let val: any = 0;
                          if (lowerC === 'si') val = it.si ?? 0;
                          else if (lowerC === 'ti') val = it.ti ?? 0;
                          else if (lowerC === 'mg') val = it.mg ?? 0;
                          else if (lowerC === 'sr') val = it.sr ?? 0;
                          else if (isJson && Array.isArray(parsedObj?.params)) {
                            const p = parsedObj.params.find((x: any) => x.name.toLowerCase() === lowerC);
                            if (p) val = p.value || 0;
                          }
                          if (Number(val) > 0) {
                            chemsWithVal.push({ chem, val });
                          }
                        });

                        if (isJson && Array.isArray(parsedObj?.params)) {
                          parsedObj.params.forEach((p: any) => {
                            if (p && p.name && !checkedChems.has(p.name.toLowerCase()) && Number(p.value) > 0) {
                              chemsWithVal.push({ chem: p.name, val: p.value });
                            }
                          });
                        }

                        if (chemsWithVal.length === 0) {
                          return <span className="fm-muted">—</span>;
                        }

                        return (
                          <div className="d-flex flex-wrap gap-2 mb-1">
                            {chemsWithVal.map(({ chem, val }) => (
                              <span key={chem} className="badge border" style={{ background: 'var(--theme-bg-header)', color: 'var(--theme-accent)', borderColor: 'var(--theme-border)' }} title={chem}>
                                {chem}: {val}
                              </span>
                            ))}
                          </div>
                        );
                      })()}
                    </td>
                    <td style={{ minWidth: '350px' }}>
                      <div className="d-flex flex-wrap gap-2 mb-1">
                        {['Si', 'Fe', 'Cu', 'Mn', 'Mg', 'Zn', 'Ti', 'Sr', 'Pb', 'Sn', 'Ni', 'Cr'].map(chem => {
                          let val: any = 0;
                          if (parsedObj?.actualValues && parsedObj.actualValues[chem] !== undefined) {
                            val = parsedObj.actualValues[chem];
                          } else if (isJson && Array.isArray(parsedObj?.calculationDetails)) {
                            const c = parsedObj.calculationDetails.find((x: any) => x.chemical.toLowerCase() === chem.toLowerCase());
                            if (c) val = c.actualValue ?? 0;
                          }
                          return (
                            <span key={chem} className="badge border" style={{ background: 'rgba(59, 130, 246, 0.1)', color: '#60a5fa', borderColor: 'rgba(59, 130, 246, 0.3)' }} title={`${chem} Actual`}>
                              {chem}: {val}
                            </span>
                          );
                        })}
                      </div>
                    </td>
                    <td className="text-nowrap">
                      {it.furnaceNumber} <small className="fm-muted d-block">{it.furnaceName}</small>
                    </td>
                    <td>{it.operatorName}</td>
                    <td>{formatUtcDate(it.startedAtUtc)}</td>
                    <td>{it.endedAtUtc ? formatUtcDate(it.endedAtUtc) : '—'}</td>
                    <td>
                      <span
                        className="badge"
                        style={{ background: ['#16a34a', '#eab308', '#dc2626'][it.indicator - 1] }}
                      >
                        {['GREEN', 'YELLOW', 'RED'][it.indicator - 1]}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${statusBadge(it.status)}`}>
                        {it.status === 1 ? 'RUNNING' : it.status === 2 ? 'CLOSED' : 'CLEARED'}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          </div>
        </div>
      )}
    </div>
  );
}
