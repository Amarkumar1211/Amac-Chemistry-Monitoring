import { useState, useEffect, useMemo } from 'react';
import { historyApi } from '../services/api';
import type { ChemicalConsumptionPeriodDto } from '../types';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, PieChart, Pie, Cell, Legend } from 'recharts';

const COLORS = ['#00E5FF', '#FFB020', '#10B981', '#F43F5E', '#8B5CF6', '#3B82F6', '#F59E0B', '#14B8A6'];

const formatWeight = (value: number, fractionDigits = 1) => {
  if (value >= 1000) {
    return { val: (value / 1000).toLocaleString(undefined, { maximumFractionDigits: fractionDigits }), unit: 'Kg' };
  }
  return { val: value.toLocaleString(undefined, { maximumFractionDigits: fractionDigits }), unit: 'g' };
};

const formatWeightString = (value: number, fractionDigits = 1) => {
  const fw = formatWeight(value, fractionDigits);
  return `${fw.val} ${fw.unit}`;
};

const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload && payload.length) {
    return (
      <div className="custom-tooltip shadow-lg p-3 rounded-4" style={{ backgroundColor: 'var(--fm-panel)', border: '1px solid var(--fm-border)' }}>
        <p className="mb-1 fw-bold" style={{ color: 'var(--fm-text)' }}>{label}</p>
        <p className="mb-0 fw-semibold" style={{ color: payload[0].color || 'var(--fm-accent)' }}>
          {payload[0].name}: {formatWeightString(Number(payload[0].value))}
        </p>
      </div>
    );
  }
  return null;
};

export default function ConsumptionReport() {
  const [data, setData] = useState<ChemicalConsumptionPeriodDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const [fromUtc, setFromUtc] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d.toISOString().split('T')[0];
  });
  const [toUtc, setToUtc] = useState(() => new Date().toISOString().split('T')[0]);
  const [groupBy, setGroupBy] = useState('Day');
  const [selectedChemical, setSelectedChemical] = useState<string | null>(null);

  const fetchReport = async () => {
    setLoading(true);
    setError('');
    try {
      const fromQuery = `${fromUtc}T00:00:00Z`;
      const toQuery = `${toUtc}T23:59:59Z`;
      const res = await historyApi.consumption(fromQuery, toQuery, groupBy);
      setData(res);
      setSelectedChemical(null);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch consumption report');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const grandTotals = useMemo(() => {
    const totals: Record<string, number> = {};
    data.forEach(p => {
      Object.entries(p.consumptions).forEach(([chem, val]) => {
        totals[chem] = (totals[chem] || 0) + val;
      });
    });
    return totals;
  }, [data]);

  const chemicalKeys = Object.keys(grandTotals).sort();

  const exportToPDF = () => {
    if (data.length === 0) return;

    const doc = new jsPDF('landscape');
    
    // Add title
    doc.setFontSize(18);
    doc.text('Chemical Consumption Report', 14, 22);
    
    doc.setFontSize(11);
    doc.text(`Generated on: ${new Date().toLocaleString()}`, 14, 30);
    doc.text(`Period: ${fromUtc} to ${toUtc} (Grouped by ${groupBy})`, 14, 36);

    const headers = [['Period', ...chemicalKeys]];
    
    const rows = data.map(p => {
      return [
        p.period,
        ...chemicalKeys.map(k => formatWeightString(p.consumptions[k] || 0))
      ];
    });

    autoTable(doc, {
      head: headers,
      body: rows,
      startY: 42,
      styles: { fontSize: 9 },
      headStyles: { fillColor: [41, 128, 185] },
    });

    doc.save(`Consumption_${groupBy}_${fromUtc}_to_${toUtc}.pdf`);
  };

  const barData = useMemo(() => {
    if (!selectedChemical) return [];
    return data.map(p => ({
      period: p.period,
      value: p.consumptions[selectedChemical] || 0
    }));
  }, [data, selectedChemical]);

  const pieData = useMemo(() => {
    return chemicalKeys.map(k => ({
      name: k,
      value: grandTotals[k]
    })).filter(x => x.value > 0);
  }, [chemicalKeys, grandTotals]);

  const getChemColor = (chem: string) => {
    const idx = chemicalKeys.indexOf(chem);
    return COLORS[idx % COLORS.length] || COLORS[0];
  };

  return (
    <div className="h-100 d-flex flex-column" style={{ overflowY: 'auto' }}>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h4 className="fw-bold mb-1" style={{ color: 'var(--fm-text)' }}>Chemical Consumption Report</h4>
          <p className="text-muted mb-0 small">Analyze and export chemical usage across batches</p>
        </div>
      </div>

      <div className="fm-panel mb-4">
        <div className="row g-3 align-items-end">
          <div className="col-12 col-md-auto">
            <label className="form-label small mb-1 fw-semibold text-uppercase" style={{ letterSpacing: '0.05em' }}>From Date</label>
            <input type="date" className="form-control" value={fromUtc} onChange={e => setFromUtc(e.target.value)} />
          </div>
          <div className="col-12 col-md-auto">
            <label className="form-label small mb-1 fw-semibold text-uppercase" style={{ letterSpacing: '0.05em' }}>To Date</label>
            <input type="date" className="form-control" value={toUtc} onChange={e => setToUtc(e.target.value)} />
          </div>
          <div className="col-12 col-md-auto">
            <label className="form-label small mb-1 fw-semibold text-uppercase" style={{ letterSpacing: '0.05em' }}>Group By</label>
            <select className="form-select" value={groupBy} onChange={e => setGroupBy(e.target.value)}>
              <option value="Day">Day</option>
              <option value="Week">Week</option>
              <option value="Month">Month</option>
            </select>
          </div>
          <div className="col-12 col-md-auto d-flex gap-2 ms-md-auto">
            <button className="btn btn-fm px-4" onClick={fetchReport} disabled={loading}>
              {loading ? <span className="spinner-border spinner-border-sm me-2" /> : <i className="bi bi-arrow-clockwise me-2"></i>}
              Generate
            </button>
            <button className="btn btn-outline-danger px-4" onClick={exportToPDF} disabled={loading || data.length === 0}>
              <i className="bi bi-file-earmark-pdf me-2"></i> Export PDF
            </button>
          </div>
        </div>
      </div>

      {error && <div className="alert alert-danger shadow-sm border-0 rounded-4 py-3"><i className="bi bi-exclamation-triangle-fill me-2"></i>{error}</div>}

      {!loading && data.length === 0 && !error && (
        <div className="text-center py-5 fm-panel d-flex flex-column align-items-center justify-content-center opacity-75">
          <i className="bi bi-inbox fs-1 mb-3 text-secondary"></i>
          <h5 className="fw-semibold">No data found</h5>
          <p className="text-muted mb-0">Try adjusting your date range to see consumption history.</p>
        </div>
      )}

      {data.length > 0 && (
        <>
          <div className="mb-4">
            <h6 className="fw-bold mb-3 text-uppercase text-muted" style={{ letterSpacing: '0.05em', fontSize: '0.85rem' }}>Grand Totals & Selection</h6>
            <div className="row g-3">
              {chemicalKeys.map((chem) => {
                const isSelected = selectedChemical === chem;
                const color = getChemColor(chem);
                return (
                  <div key={chem} className="col-6 col-md-4 col-xl-2">
                    <div 
                      onClick={() => setSelectedChemical(isSelected ? null : chem)}
                      className={`h-100 rounded-4 p-3 position-relative cursor-pointer transition-all`}
                      style={{ 
                        background: isSelected ? `linear-gradient(135deg, ${color}15, ${color}05)` : 'var(--fm-panel)',
                        border: `1px solid ${isSelected ? color : 'var(--fm-border)'}`,
                        boxShadow: isSelected ? `0 8px 24px -8px ${color}60` : 'var(--theme-card-shadow)',
                        transform: isSelected ? 'translateY(-2px)' : 'none',
                        cursor: 'pointer'
                      }}
                    >
                      {isSelected && (
                        <div className="position-absolute top-0 end-0 m-2">
                          <i className="bi bi-check-circle-fill" style={{ color: color }}></i>
                        </div>
                      )}
                      <div className="d-flex flex-column h-100 justify-content-between">
                        <div className="d-flex align-items-center gap-2 mb-2">
                          <div className="rounded-circle" style={{ width: '12px', height: '12px', backgroundColor: color }}></div>
                          <span className="fw-bold fs-6" style={{ color: 'var(--fm-text)' }}>{chem}</span>
                        </div>
                        <div className="fs-3 fw-black font-monospace" style={{ color: 'var(--fm-text)', lineHeight: 1.2 }}>
                          {formatWeight(grandTotals[chem]).val}
                          <span className="fs-6 fw-bold ms-1" style={{ color: 'var(--fm-muted)' }}>{formatWeight(grandTotals[chem]).unit}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {selectedChemical && (
            <div className="row g-4 pb-4">
              <div className="col-lg-8">
                <div className="fm-panel h-100 d-flex flex-column">
                  <div className="d-flex justify-content-between align-items-center mb-4">
                    <h6 className="fw-bold mb-0">
                      <span style={{ color: getChemColor(selectedChemical) }}>{selectedChemical}</span> Consumption Trend
                    </h6>
                    <span className="badge rounded-pill bg-secondary bg-opacity-10 text-secondary border border-secondary border-opacity-25 px-3 py-2">
                      Grouped by {groupBy}
                    </span>
                  </div>
                  <div className="flex-grow-1 w-100" style={{ minHeight: '350px' }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={barData} margin={{ top: 20, right: 10, left: -10, bottom: 0 }}>
                        <defs>
                          <linearGradient id="barGradient" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor={getChemColor(selectedChemical)} stopOpacity={0.9} />
                            <stop offset="100%" stopColor={getChemColor(selectedChemical)} stopOpacity={0.3} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="var(--fm-border)" vertical={false} />
                        <XAxis 
                          dataKey="period" 
                          stroke="var(--fm-muted)" 
                          fontSize={12} 
                          tickMargin={12}
                          tickLine={false}
                          axisLine={false}
                        />
                        <YAxis 
                          stroke="var(--fm-muted)" 
                          fontSize={12} 
                          tickFormatter={(val) => formatWeightString(val)}
                          tickLine={false}
                          axisLine={false}
                          tickMargin={12}
                        />
                        <Tooltip content={<CustomTooltip />} cursor={{ fill: 'var(--theme-text-primary)', opacity: 0.05 }} />
                        <Bar 
                          dataKey="value" 
                          name={selectedChemical}
                          fill="url(#barGradient)" 
                          radius={[6, 6, 0, 0]} 
                          maxBarSize={60}
                          animationDuration={1000}
                        />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              </div>
              
              <div className="col-lg-4">
                <div className="fm-panel h-100 d-flex flex-column">
                  <h6 className="fw-bold mb-4">Overall Distribution</h6>
                  <div className="flex-grow-1 w-100 position-relative d-flex align-items-center justify-content-center" style={{ minHeight: '350px' }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={pieData}
                          cx="50%"
                          cy="45%"
                          innerRadius={80}
                          outerRadius={110}
                          paddingAngle={8}
                          dataKey="value"
                          stroke="none"
                          cornerRadius={6}
                          animationDuration={1000}
                        >
                          {pieData.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={COLORS[chemicalKeys.indexOf(entry.name) % COLORS.length]} />
                          ))}
                        </Pie>
                        <Tooltip content={<CustomTooltip />} />
                        <Legend 
                          verticalAlign="bottom" 
                          height={36}
                          content={(props) => {
                            const { payload } = props;
                            return (
                              <ul className="list-unstyled d-flex flex-wrap justify-content-center gap-3 mb-0 mt-3">
                                {payload?.map((entry: any, index: number) => (
                                  <li key={`item-${index}`} className="d-flex align-items-center gap-2" style={{ color: 'var(--fm-text)', fontSize: '0.85rem' }}>
                                    <span className="rounded-circle" style={{ width: '10px', height: '10px', backgroundColor: entry.color }}></span>
                                    <span className="fw-semibold">{entry.value}</span>
                                  </li>
                                ))}
                              </ul>
                            );
                          }}
                        />
                      </PieChart>
                    </ResponsiveContainer>
                    <div className="position-absolute text-center" style={{ top: '45%', left: '50%', transform: 'translate(-50%, -50%)', pointerEvents: 'none' }}>
                      <p className="mb-0 small fw-bold text-muted text-uppercase" style={{ letterSpacing: '0.1em' }}>Total</p>
                      <h4 className="fw-black mb-0" style={{ color: 'var(--fm-text)' }}>
                        {(() => {
                          const fw = formatWeight(Object.values(grandTotals).reduce((a, b) => a + b, 0), 1);
                          return (
                            <>
                              {fw.val}<span className="fs-6 text-muted ms-1">{fw.unit}</span>
                            </>
                          );
                        })()}
                      </h4>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
