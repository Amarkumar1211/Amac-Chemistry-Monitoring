import { FormEvent, useEffect, useState } from 'react';
import { furnaceApi, usersApi, historyApi } from '../services/api';
import type { FurnaceDto, UserManagementDto, UserRole } from '../types';
import MasterSpecsManager from '../components/MasterSpecsManager';
import ConsumptionReport from '../components/ConsumptionReport';

const roleOptions: UserRole[] = ['Admin', 'Operator', 'Viewer'];

export default function AdminPanel() {
  // Users
  const [users, setUsers] = useState<UserManagementDto[]>([]);
  const [furnaces, setFurnaces] = useState<FurnaceDto[]>([]);
  const [tab, setTab] = useState<'users' | 'furnaces' | 'masterSpecs' | 'consumption'>('users');

  // User form state
  const [uname, setUname] = useState('');
  const [dispName, setDispName] = useState('');
  const [uemail, setUemail] = useState('');
  const [upassword, setUpassword] = useState('');
  const [urole, setUrole] = useState<UserRole>('Operator');
  const [uMsg, setUMsg] = useState('');
  const [uBusy, setUBusy] = useState(false);

  // Furnace form state
  const [editFId, setEditFId] = useState<number | null>(null);
  const [fnum, setFnum] = useState('');
  const [fname, setFname] = useState('');
  const [floc, setFloc] = useState('');
  const [fActive, setFActive] = useState(true);
  const [fMsg, setFMsg] = useState('');
  const [fBusy, setFBusy] = useState(false);

  useEffect(() => {
    void usersApi.getAll().then(setUsers).catch((e) => setUMsg((e as Error).message));
    void furnaceApi.getAll().then(setFurnaces).catch((e) => setFMsg((e as Error).message));
  }, []);

  const createUser = async (e: React.FormEvent) => {
    e.preventDefault();
    setUBusy(true);
    try {
      const roleMap: Record<string, number> = {
        Admin: 1,
        Operator: 2,
        Viewer: 3,
      };

      await usersApi.create({
        userName: uname.trim(),
        displayName: dispName.trim(),
        email: uemail.trim() || undefined,
        password: upassword,
        role: roleMap[urole] || 3,
      });
      setUMsg('User created.');
      setUname('');
      setDispName('');
      setUemail('');
      setUpassword('');
      setUsers(await usersApi.getAll());
    } catch (err) {
      setUMsg((err as Error).message || 'Failed to create user');
    } finally {
      setUBusy(false);
    }
  };

  const deactivateUser = async (id: number) => {
    if (!window.confirm('Deactivate this user?')) return;
    try {
      await usersApi.deactivate(id);
      const updated = await usersApi.getAll();
      setUsers(updated);
    } catch (err) {
      setUMsg((err as Error).message);
    }
  };

  const createFurnace = async (e: FormEvent) => {
    e.preventDefault();
    setFBusy(true);
    setFMsg('');
    try {
      await furnaceApi.save({ id: editFId || 0, number: fnum, name: fname, location: floc || undefined, isActive: fActive });
      setEditFId(null);
      setFnum('');
      setFname('');
      setFloc('');
      setFActive(true);
      setFurnaces(await furnaceApi.getAll());
      setFMsg(editFId ? 'Furnace updated.' : 'Furnace created.');
    } catch (err) {
      setFMsg((err as Error).message);
    } finally {
      setFBusy(false);
    }
  };

  const startEditFurnace = (f: FurnaceDto) => {
    setEditFId(f.id);
    setFnum(f.number);
    setFname(f.name);
    setFloc(f.location || '');
    setFActive(f.isActive);
    setFMsg('');
  };

  const cancelEditFurnace = () => {
    setEditFId(null);
    setFnum('');
    setFname('');
    setFloc('');
    setFActive(true);
    setFMsg('');
  };

  const activateFurnace = async (f: FurnaceDto) => {
    try {
      // status: 1 = Active, 2 = Inactive (matches backend FurnaceStatus enum)
      await furnaceApi.save({ id: f.id, number: f.number, name: f.name, isActive: true, status: 1, location: f.location });
      setFurnaces(await furnaceApi.getAll());
      setFMsg(`Furnace ${f.number} activated.`);
    } catch (err) {
      setFMsg((err as Error).message);
    }
  };

  const deactivateFurnace = async (id: number) => {
    if (!window.confirm('Deactivate this furnace?')) return;
    try {
      await furnaceApi.delete(id);
      setFurnaces(await furnaceApi.getAll());
    } catch (err) {
      setFMsg((err as Error).message);
    }
  };

  const hardDeleteFurnace = async (id: number) => {
    if (!window.confirm('Permanently delete this furnace? This cannot be undone.')) return;
    try {
      await furnaceApi.hardDelete(id);
      setFurnaces(await furnaceApi.getAll());
    } catch (err) {
      setFMsg((err as Error).message);
    }
  };

  const handleResetHistory = async () => {
    if (!window.confirm('Are you sure you want to completely reset all History and Consumption Data to 0? This cannot be undone.')) return;
    try {
      await historyApi.reset();
      window.alert('History and consumption data has been successfully reset to 0.');
      // Refresh the page or trigger a re-fetch if necessary, but since they might be on consumption tab, we can just reload
      window.location.reload();
    } catch (err) {
      window.alert((err as Error).message || 'Failed to reset data');
    }
  };

  return (
    <div className="d-flex flex-column h-100">
      <header className="mb-4 d-flex align-items-center justify-content-between flex-shrink-0">
        <div>
          <h4 className="text-white mb-0">Admin Panel</h4>
          <small className="fm-muted">Manage users and furnaces</small>
        </div>
        <div className="d-flex gap-3 align-items-center">
          <div className="fm-hover-dropdown">
            <button className="btn btn-fm dropdown-toggle">
              {tab === 'users' ? 'Users' : tab === 'furnaces' ? 'Furnaces' : tab === 'masterSpecs' ? 'Master Specs' : 'Consumption'}
            </button>
            <div className="dropdown-menu">
              <button
                className={`dropdown-item ${tab === 'users' ? 'active' : ''}`}
                onClick={() => setTab('users')}
              >
                Users
              </button>
              <button
                className={`dropdown-item ${tab === 'furnaces' ? 'active' : ''}`}
                onClick={() => setTab('furnaces')}
              >
                Furnaces
              </button>
              <button
                className={`dropdown-item ${tab === 'masterSpecs' ? 'active' : ''}`}
                onClick={() => setTab('masterSpecs')}
              >
                Master Specs
              </button>
              <button
                className={`dropdown-item ${tab === 'consumption' ? 'active' : ''}`}
                onClick={() => setTab('consumption')}
              >
                Consumption
              </button>
            </div>
          </div>
          <button
            className="btn btn-outline-danger"
            onClick={handleResetHistory}
            title="Reset History and Consumption to 0"
          >
            Reset Data
          </button>
        </div>
      </header>

      {tab === 'users' && (
        <div className="internal-scroll pb-2">
          <div className="row g-4 m-0">
            <div className="col-lg-4 p-1">
              <div className="fm-panel">
              <h6 className="text-white mb-3">Create User</h6>
              {uMsg && <div className="alert alert-info py-2">{uMsg}</div>}
              <form onSubmit={createUser}>
                <div className="mb-2">
                  <label className="form-label">Username *</label>
                  <input className="form-control" value={uname} onChange={(e) => setUname(e.target.value)} required />
                </div>
                <div className="mb-2">
                  <label className="form-label">Display Name *</label>
                  <input className="form-control" value={dispName} onChange={(e) => setDispName(e.target.value)} required />
                </div>
                <div className="mb-2">
                  <label className="form-label">Email</label>
                  <input className="form-control" type="email" value={uemail} onChange={(e) => setUemail(e.target.value)} />
                </div>
                <div className="mb-2">
                  <label className="form-label">Password *</label>
                  <input className="form-control" type="password" value={upassword} onChange={(e) => setUpassword(e.target.value)} required />
                </div>
                <div className="mb-3">
                  <label className="form-label">Role</label>
                  <select className="form-select" value={urole} onChange={(e) => setUrole(e.target.value as UserRole)}>
                    {roleOptions.map((r) => (
                      <option key={r} value={r}>
                        {r}
                      </option>
                    ))}
                  </select>
                </div>
                <button className="btn btn-fm w-100" disabled={uBusy}>
                  {uBusy ? 'Creating…' : 'Create User'}
                </button>
              </form>
            </div>
          </div>

          <div className="col-lg-8 p-1">
            <div className="fm-panel p-0 table-responsive">
              <table className="table table-dark align-middle mb-0">
                <thead>
                  <tr>
                    <th>Username</th>
                    <th>Name</th>
                    <th>Role</th>
                    <th>Status</th>
                    <th>Last Login</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {users.map((u) => (
                    <tr key={u.id}>
                      <td>{u.userName}</td>
                      <td>{u.displayName}</td>
                      <td>{u.roleName}</td>
                      <td>
                        <span className={`badge ${u.isActive ? 'text-bg-success' : 'text-bg-secondary'}`}>
                          {u.isActive ? 'ACTIVE' : 'INACTIVE'}
                        </span>
                      </td>
                      <td>{u.lastLoginAtUtc ? new Date(u.lastLoginAtUtc).toLocaleString() : '—'}</td>
                      <td>
                        {u.isActive && !u.isSystem && (
                          <button className="btn btn-sm btn-outline-danger" onClick={() => void deactivateUser(u.id)}>
                            Deactivate
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          </div>
        </div>
      )}

      {tab === 'furnaces' && (
        <div className="internal-scroll pb-2">
          <div className="row g-4 m-0">
            <div className="col-lg-4 p-1">
              <div className="fm-panel">
              <h6 className="text-white mb-3">{editFId ? 'Edit Furnace' : 'Create Furnace'}</h6>
              {fMsg && <div className="alert alert-info py-2">{fMsg}</div>}
              <form onSubmit={createFurnace}>
                <div className="mb-2">
                  <label className="form-label">Number *</label>
                  <input className="form-control" value={fnum} onChange={(e) => setFnum(e.target.value)} placeholder="F21" required />
                </div>
                <div className="mb-2">
                  <label className="form-label">Name *</label>
                  <input className="form-control" value={fname} onChange={(e) => setFname(e.target.value)} required />
                </div>
                <div className="mb-2">
                  <label className="form-label">Location</label>
                  <input className="form-control" value={floc} onChange={(e) => setFloc(e.target.value)} />
                </div>
                <div className="form-check mb-3">
                  <input
                    className="form-check-input"
                    type="checkbox"
                    id="factive"
                    checked={fActive}
                    onChange={(e) => setFActive(e.target.checked)}
                  />
                  <label className="form-check-label" htmlFor="factive">
                    Active
                  </label>
                </div>
                <div className="d-flex gap-2">
                  <button className="btn btn-fm w-100" disabled={fBusy}>
                    {fBusy ? 'Saving…' : editFId ? 'Update' : 'Create'}
                  </button>
                  {editFId && (
                    <button type="button" className="btn btn-outline-secondary w-100" onClick={cancelEditFurnace}>
                      Cancel
                    </button>
                  )}
                </div>
              </form>
            </div>

          </div>

          <div className="col-lg-8 p-1">
            <div className="fm-panel p-0 table-responsive">
              <table className="table table-dark align-middle mb-0">
                <thead>
                  <tr>
                    <th>Number</th>
                    <th>Name</th>
                    <th>Location</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {furnaces.map((f) => (
                    <tr key={f.id}>
                      <td className="fw-semibold">{f.number}</td>
                      <td>{f.name}</td>
                      <td>{f.location || '—'}</td>
                      <td>
                        <span className={`badge ${f.isActive ? 'text-bg-success' : 'text-bg-secondary'}`}>
                          {f.isActive ? 'ACTIVE' : 'INACTIVE'}
                        </span>
                      </td>
                      <td>
                        <div className="d-flex gap-2 justify-content-end">
                          <button className="btn btn-sm btn-outline-info" onClick={() => startEditFurnace(f)}>
                            Edit
                          </button>
                          {f.isActive ? (
                            <button className="btn btn-sm btn-outline-warning" onClick={() => void deactivateFurnace(f.id)}>
                              Deactivate
                            </button>
                          ) : (
                            <button className="btn btn-sm btn-outline-success" onClick={() => void activateFurnace(f)}>
                              Activate
                            </button>
                          )}
                          <button className="btn btn-sm btn-outline-danger" onClick={() => void hardDeleteFurnace(f.id)}>
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          </div>
        </div>
      )}

      {tab === 'masterSpecs' && (
        <div className="internal-scroll pb-2">
          <MasterSpecsManager />
        </div>
      )}

      {tab === 'consumption' && (
        <div className="internal-scroll pb-2">
          <ConsumptionReport />
        </div>
      )}
    </div>
  );
}
