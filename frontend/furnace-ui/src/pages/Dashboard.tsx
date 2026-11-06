import { useEffect, useState } from 'react';
import { dashboardApi, furnaceApi, batchApi } from '../services/api';
import { useSignalR } from '../context/SignalRContext';
import type { DashboardDto, FurnaceDto, FurnaceUpdateDto, FurnaceClearedDto, BatchDto, IndicatorColor } from '../types';

interface FurnaceDisplay {
  furnace: FurnaceDto;
  batch: BatchDto | null;
}

const colorClass: Record<IndicatorColor | 0, string> = {
  1: 'indicator-green',
  2: 'indicator-yellow',
  3: 'indicator-red',
  0: 'indicator-none',
};

export default function Dashboard() {
  const { subscribeUpdate, subscribeClear } = useSignalR();
  const [stats, setStats] = useState<DashboardDto | null>(null);
  const [furnaces, setFurnaces] = useState<FurnaceDisplay[]>([]);
  const [error, setError] = useState('');

  const load = async () => {
    try {
      const [s, fs] = await Promise.all([dashboardApi.get(), furnaceApi.getAll()]);
      setStats(s);
      const displays = await Promise.all(
        fs.map(async (f: FurnaceDto) => {
          let batch: BatchDto | null = null;
          try {
            batch = await batchApi.getActive(f.id);
          } catch {
            batch = null;
          }
          return { furnace: f, batch };
        }),
      );
      setFurnaces(displays);
    } catch (err) {
      setError((err as Error).message || 'Failed to load dashboard');
    }
  };

  useEffect(() => {
    void load();
  }, []);

  useEffect(() => {
    const unsubUpdate = subscribeUpdate((u: FurnaceUpdateDto) => {
      setFurnaces((prev) =>
        prev.map((fd) =>
          fd.furnace.number === u.furnaceNumber.toUpperCase() && fd.batch
            ? {
                ...fd,
                batch: {
                  ...fd.batch,
                  si: u.si,
                  ti: u.ti,
                  mg: u.mg,
                  indicator: u.indicator,
                  instruction: u.instruction,
                  remarks: u.remarks,
                },
              }
            : fd,
        ),
      );
    });
    const unsubClear = subscribeClear((c: FurnaceClearedDto) => {
      setFurnaces((prev) =>
        prev.map((fd) =>
          fd.furnace.number === c.furnaceNumber.toUpperCase() ? { ...fd, batch: null } : fd,
        ),
      );
    });
    return () => {
      unsubUpdate();
      unsubClear();
    };
  }, [subscribeUpdate, subscribeClear]);

  return (
    <div className="d-flex flex-column h-100">
      <header className="d-flex align-items-center justify-content-between mb-4 flex-shrink-0">
        <div>
          <h4 className="text-white mb-0">Plant Dashboard</h4>
          <small className="fm-muted">Real-time furnace overview</small>
        </div>
      </header>

      {error && <div className="alert alert-danger">{error}</div>}

      {stats && (
        <div className="row g-3 mb-4">
          <div className="col-6 col-lg-3">
            <div className="fm-panel d-flex align-items-center">
              <i className="bi bi-fire fs-3 me-3" style={{ color: 'var(--fm-warn)' }} />
              <div>
                <div className="fs-4 fw-bold">{stats.activeBatches}</div>
                <small className="fm-muted">Active Batches</small>
              </div>
            </div>
          </div>
          <div className="col-6 col-lg-3">
            <div className="fm-panel d-flex align-items-center">
              <i className="bi bi-cpu fs-3 me-3" style={{ color: 'var(--fm-accent)' }} />
              <div>
                <div className="fs-4 fw-bold">{stats.connectedDisplays}</div>
                <small className="fm-muted">Connected Displays</small>
              </div>
            </div>
          </div>
          <div className="col-6 col-lg-3">
            <div className="fm-panel d-flex align-items-center">
              <i className="bi bi-person-badge fs-3 me-3" style={{ color: '#60a5fa' }} />
              <div>
                <div className="fs-4 fw-bold">{stats.onlineOperators}</div>
                <small className="fm-muted">Online Operators</small>
              </div>
            </div>
          </div>
          <div className="col-6 col-lg-3">
            <div className="fm-panel d-flex align-items-center">
              <i className="bi bi-thermometer-half fs-3 me-3" style={{ color: 'var(--fm-danger)' }} />
              <div>
                <div className="fs-4 fw-bold">
                  <span className="text-danger">{stats.redCount}</span>/
                  <span style={{ color: 'var(--fm-warn)' }}>{stats.yellowCount}</span>/
                  <span style={{ color: 'var(--fm-success)' }}>{stats.greenCount}</span>
                </div>
                <small className="fm-muted">Red / Yellow / Green</small>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="internal-scroll pb-2">
        <div className="row g-3 m-0">
          {furnaces.map((fd) => (
            <div className="col-6 col-md-4 col-xl-3 p-1" key={fd.furnace.id}>
            <div className={`fm-panel ${fd.batch ? colorClass[fd.batch.indicator] : 'indicator-none'}`}>
              <div className="d-flex justify-content-between align-items-center">
                <div className="fw-bold fs-5">{fd.furnace.number}</div>
                <span
                  className="badge"
                  style={{
                    background: fd.batch
                      ? ['#16a34a', '#eab308', '#dc2626'][fd.batch.indicator - 1]
                      : '#475569',
                  }}
                >
                  {fd.batch ? ['GREEN', 'YELLOW', 'RED'][fd.batch.indicator - 1] : 'IDLE'}
                </span>
              </div>
              <div className="fm-muted small">{fd.furnace.name}</div>
              {fd.batch ? (
                <div className="mt-2 small">
                  <div>
                    <strong>Batch:</strong>{' '}
                    <span className="fw-semibold">{fd.batch.batchNumber}</span>
                  </div>
                  <div>
                    <strong>Si</strong> {fd.batch.si} · <strong>Ti</strong> {fd.batch.ti} ·{' '}
                    <strong>Mg</strong> {fd.batch.mg}
                  </div>
                  <div className="text-truncate" title={fd.batch.instruction}>
                    {fd.batch.instruction || '—'}
                  </div>
                </div>
              ) : (
                <div className="mt-2 small fm-muted">No Active Batch</div>
              )}
            </div>
          </div>
        ))}
        </div>
      </div>
    </div>
  );
}

