(() => {
  const token = sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const login = () => { sessionStorage.removeItem('eWarrantyToken'); localStorage.removeItem('eWarrantyToken'); window.location.replace('/login'); };
  const readClaims = value => {
    try {
      const part = value.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const bytes = atob(part.padEnd(Math.ceil(part.length / 4) * 4, '='));
      const payload = JSON.parse(new TextDecoder().decode(Uint8Array.from(bytes, c => c.charCodeAt(0))));
      const find = suffix => Object.entries(payload).find(([key]) => key === suffix || key.endsWith('/' + suffix))?.[1];
      const role = find('role');
      return { role: Array.isArray(role) ? role[0] : role, name: find('unique_name') || find('name'), exp: Number(payload.exp) };
    } catch { return null; }
  };
  const claims = token ? readClaims(token) : null;
  if (!claims || claims.exp <= Date.now() / 1000) { login(); return; }
  const role = String(claims.role).toLowerCase();
  if (role !== 'staff') { window.location.replace(role === 'admin' ? '/admin/dashboard' : role === 'manager' ? '/manager/dashboard' : role === 'technician' ? '/technician/dashboard' : '/user/dashboard'); return; }
  document.getElementById('staff-name').textContent = claims.name || 'nhân viên';
  document.getElementById('staff-logout').addEventListener('click', login);
  window.setInterval(() => { if (claims.exp <= Date.now() / 1000) login(); }, 15000);
})();
