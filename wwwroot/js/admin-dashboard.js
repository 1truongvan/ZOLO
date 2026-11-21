(() => {
  const token = sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const loginUrl = '/login';
  const homeUrl = '/';
  const fromBase64Url = value => {
    const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
    const binary = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='));
    return new TextDecoder().decode(Uint8Array.from(binary, char => char.charCodeAt(0)));
  };
  const readClaims = value => {
    try {
      const payload = JSON.parse(fromBase64Url(value.split('.')[1]));
      const findClaim = suffix => Object.entries(payload).find(([key]) => key === suffix || key.endsWith('/' + suffix))?.[1];
      const roleValue = findClaim('role');
      return {
        payload,
        role: Array.isArray(roleValue) ? roleValue[0] : roleValue,
        name: findClaim('unique_name') || findClaim('name'),
        userId: findClaim('nameid') || findClaim('sub') || findClaim('nameidentifier'),
        expiry: Number(payload.exp)
      };
    } catch { return null; }
  };
  const claims = token ? readClaims(token) : null;
  const validSession = claims && claims.expiry > Math.floor(Date.now() / 1000);
  if (!validSession) {
    sessionStorage.removeItem('eWarrantyToken');
    localStorage.removeItem('eWarrantyToken');
    window.location.replace(loginUrl);
    return;
  }
  const normalizedRole = String(claims.role).toLowerCase();
  if (normalizedRole !== 'admin') {
    window.location.replace(normalizedRole === 'user' || normalizedRole === 'customer'
      ? '/user/dashboard' : normalizedRole === 'staff' ? '/staff/dashboard'
        : normalizedRole === 'manager' ? '/manager/dashboard'
          : normalizedRole === 'technician' ? '/technician/dashboard' : homeUrl);
    return;
  }

  const byId = id => document.getElementById(id);
  const selectedModule = new URLSearchParams(location.search).get('module');
  if (selectedModule) document.body.classList.add('admin-module-mode');
  const roleLabel = String(claims.role || 'Admin');
  const username = String(claims.name || 'Admin');
  const initial = [...username.trim()][0]?.toLocaleUpperCase('vi-VN') || 'A';
  byId('user-name').textContent = username;
  byId('user-role').textContent = roleLabel;
  byId('user-avatar').textContent = initial;
  byId('popover-name').textContent = username;
  byId('popover-role').textContent = roleLabel;
  byId('dialog-avatar').textContent = initial;
  byId('profile-username').textContent = username;
  byId('profile-user-role').textContent = roleLabel;
  byId('profile-id').textContent = claims.userId || 'Không có trong token';
  byId('profile-expiry').textContent = new Date(claims.expiry * 1000).toLocaleString('vi-VN');

  const logout = () => {
    sessionStorage.removeItem('eWarrantyToken');
    localStorage.removeItem('eWarrantyToken');
    window.location.replace(loginUrl);
  };
  const expireTimer = window.setInterval(() => {
    if (claims.expiry <= Math.floor(Date.now() / 1000)) {
      window.clearInterval(expireTimer);
      logout();
    }
  }, 15000);

  const popover = byId('account-popover');
  const trigger = byId('account-trigger');
  trigger.addEventListener('click', () => {
    const open = trigger.getAttribute('aria-expanded') === 'true';
    trigger.setAttribute('aria-expanded', String(!open));
    popover.hidden = open;
  });
  document.addEventListener('click', event => {
    if (!event.target.closest('.account-menu-wrap')) {
      trigger.setAttribute('aria-expanded', 'false');
      popover.hidden = true;
    }
  });
  byId('logout-button').addEventListener('click', logout);
  byId('profile-button').addEventListener('click', () => {
    popover.hidden = true;
    trigger.setAttribute('aria-expanded', 'false');
    byId('profile-dialog').showModal();
  });

  byId('sidebar-toggle').addEventListener('click', () => document.body.classList.toggle('sidebar-collapsed'));
  const closeMobileSidebar = () => document.body.classList.remove('mobile-sidebar-open');
  byId('mobile-menu').addEventListener('click', () => document.body.classList.toggle('mobile-sidebar-open'));
  byId('mobile-shade').addEventListener('click', closeMobileSidebar);
  document.querySelectorAll('.side-link.active').forEach(link => link.addEventListener('click', closeMobileSidebar));
  document.querySelectorAll('.group-heading').forEach(button => button.addEventListener('click', () => {
    const expanded = button.getAttribute('aria-expanded') === 'true';
    button.setAttribute('aria-expanded', String(!expanded));
  }));

  let toastTimer;
  document.querySelectorAll('.side-link.unavailable').forEach(link => link.addEventListener('click', event => {
    event.preventDefault();
    const toast = byId('toast');
    toast.textContent = link.dataset.feature || 'Chức năng chưa được triển khai.';
    toast.classList.add('show');
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(() => toast.classList.remove('show'), 3000);
  }));

  const formatCount = value => new Intl.NumberFormat('vi-VN').format(value);
  const setStatus = text => { byId('refresh-status').textContent = text; };
  const loadSummary = async () => {
    const refresh = byId('refresh-button');
    const error = byId('load-error');
    error.hidden = true;
    refresh.disabled = true;
    refresh.classList.add('is-loading');
    setStatus('Đang tải số liệu…');
    try {
      const response = await fetch('/api/admin/dashboard/summary', {
        headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
        cache: 'no-store'
      });
      if (response.status === 401) { logout(); return; }
      if (response.status === 403) { window.location.replace(homeUrl); return; }
      if (!response.ok) throw new Error(response.status === 500
        ? 'API không tải được dữ liệu. Hãy kiểm tra kết nối SQL Server và thử lại.'
        : `Không thể tải Dashboard (HTTP ${response.status}).`);
      const data = await response.json();
      const unavailable = data.unavailableMetrics || {};
      document.querySelectorAll('[data-warranty-status]').forEach(element => { element.textContent = formatCount(data.warrantyStatuses?.[element.dataset.warrantyStatus] ?? 0); });
      document.querySelectorAll('[data-metric]').forEach(element => {
        const key = element.dataset.metric;
        const count = data.counts?.[key];
        if (Number.isSafeInteger(count) && count >= 0) {
          element.textContent = formatCount(count);
          element.classList.remove('unavailable-value');
          element.title = '';
        } else {
          element.textContent = 'Chưa có dữ liệu';
          element.classList.add('unavailable-value');
          element.title = unavailable[key] || 'Backend chưa cung cấp dữ liệu cho mục này.';
        }
      });
      setStatus(`Cập nhật lúc ${new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}`);
    } catch (exception) {
      error.textContent = exception instanceof TypeError
        ? 'Không kết nối được Backend. Kiểm tra máy chủ rồi tải lại.'
        : exception.message;
      error.hidden = false;
      setStatus('Không tải được dữ liệu');
    } finally {
      refresh.disabled = false;
      refresh.classList.remove('is-loading');
    }
  };
  byId('refresh-button').addEventListener('click', loadSummary);
  if (!selectedModule) loadSummary();
})();
