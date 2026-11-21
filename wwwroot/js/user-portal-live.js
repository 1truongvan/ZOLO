(() => {
  const token = sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const login = () => { sessionStorage.removeItem('eWarrantyToken'); localStorage.removeItem('eWarrantyToken'); location.replace('/login'); };
  const decode = value => { try { const raw = value.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'); return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(raw.padEnd(Math.ceil(raw.length / 4) * 4, '=')), c => c.charCodeAt(0)))); } catch { return null; } };
  const claims = token && decode(token);
  const getClaim = key => claims && Object.entries(claims).find(([k]) => k === key || k.endsWith('/' + key))?.[1];
  const role = String(getClaim('role') || '').toLowerCase();
  if (!claims || Number(claims.exp) <= Date.now() / 1000) return login();
  if (role === 'admin') return location.replace('/admin/dashboard');
  if (role === 'manager') return location.replace('/manager/dashboard');
  if (role === 'technician') return location.replace('/technician/dashboard');
  if (!['user', 'customer'].includes(role)) return location.replace('/');

  const el = id => document.getElementById(id);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const fmtDate = value => value ? new Date(value).toLocaleDateString('vi-VN') : '—';
  const stateText = value => ({ Submitted: 'Đã gửi', Approved: 'Đã duyệt', Rejected: 'Từ chối', InProgress: 'Đang xử lý', ReadyForReview: 'Chờ xác nhận', Completed: 'Hoàn thành' }[value] || value || '—');
  const stateClass = value => ({ Submitted: 'checking', Approved: 'received', Rejected: 'expired', InProgress: 'checking', ReadyForReview: 'received', Completed: 'received' }[value] || 'checking');
  let products = [], requests = [], account = {}, permissions = new Map(), currentPage = location.pathname.split('/').filter(Boolean).pop() || 'dashboard';
  const api = async (url, options = {}) => {
    const response = await fetch(url, { ...options, headers: { Authorization: `Bearer ${token}`, Accept: 'application/json', ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
    if (response.status === 401) return login();
    const payload = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) throw new Error(payload?.message || payload || `Yêu cầu lỗi (${response.status})`);
    return payload;
  };
  const toast = message => { const box = el('toast'); box.textContent = message; box.classList.add('show'); setTimeout(() => box.classList.remove('show'), 3000); };
  const heading = title => `<div class="user-page-head"><div><div class="breadcrumbs"><a href="/user/dashboard">Khu vực của tôi</a><span>›</span><b>${esc(title)}</b></div><h1 class="page-title">${esc(title)}</h1></div></div>`;
  const pageTitle = { dashboard: 'Tổng quan', products: 'Sản phẩm của tôi', 'warranty-register': 'Đăng ký bảo hành', 'warranty-request': 'Gửi yêu cầu bảo hành', requests: 'Theo dõi yêu cầu', history: 'Lịch sử bảo hành', profile: 'Hồ sơ cá nhân' };
  const productRows = list => list.length ? list.map(p => `<article class="product-card" data-search="${esc(`${p.productName} ${p.serialNumber} ${p.productCode}`)}"><div class="product-visual">▣</div><div class="product-copy"><h3>${esc(p.productName)}</h3><div class="product-meta"><span>Mã: ${esc(p.productCode)}</span><span>SN: ${esc(p.serialNumber)}</span><span>Hết hạn: ${fmtDate(p.warrantyExpiresAt)}</span><span class="warranty-pill ${p.isUnderWarranty ? '' : 'expired'}">${p.isUnderWarranty ? 'Còn bảo hành' : 'Hết bảo hành'}</span></div></div><div class="product-actions"><a href="/user/warranty-request">Gửi yêu cầu</a></div></article>`).join('') : '<p class="side-copy">Chưa có sản phẩm được đăng ký trong tài khoản.</p>';
  const requestRows = list => list.length ? `<div class="history-table-wrap"><table><thead><tr><th>Mã yêu cầu</th><th>Sản phẩm</th><th>Ngày gửi</th><th>Trạng thái</th></tr></thead><tbody>${list.map(r => `<tr data-search="${esc(`${r.requestCode} ${r.product?.productName || ''}`)}"><td>${esc(r.requestCode)}</td><td>${esc(r.product?.productName || '—')}</td><td>${fmtDate(r.createdAt)}</td><td><span class="request-pill ${stateClass(r.status)}">${esc(stateText(r.status))}</span></td></tr>`).join('')}</tbody></table></div>` : '<p class="side-copy">Chưa có yêu cầu bảo hành.</p>';
  const productsBlock = (limit = products.length) => `<section class="section-panel"><div class="panel-title-row"><h2 class="panel-title">Sản phẩm của tôi <small>${products.length} sản phẩm</small></h2><a class="primary-link" href="/user/warranty-register">+ Đăng ký sản phẩm</a></div><div class="product-grid">${productRows(products.slice(0, limit))}</div></section>`;
  const requestsBlock = (title = 'Lịch sử yêu cầu') => `<section class="section-panel"><div class="panel-title-row"><h2 class="panel-title">${title}</h2><a class="secondary-link" href="/user/requests">Xem tất cả →</a></div>${requestRows(requests)}</section>`;
  const stat = (name, value, cls) => `<div class="stat-card"><span class="stat-icon ${cls}">◉</span><span><small class="stat-label">${name}</small><b class="stat-number">${value}</b></span></div>`;
  const pageMarkup = () => {
    const title = pageTitle[currentPage] || pageTitle.dashboard;
    if (currentPage === 'products') return `${heading(title)}${productsBlock()}`;
    if (currentPage === 'requests' || currentPage === 'history') return `${heading(title)}${requestsBlock(title)}`;
    if (currentPage === 'warranty-register') return `${heading(title)}<section class="subpage-grid"><form class="content-card" id="register-form"><h2>Thông tin sản phẩm</h2><div class="field-grid"><div class="field"><label for="reg-code">Mã sản phẩm</label><input id="reg-code" required maxlength="60"></div><div class="field"><label for="reg-serial">Số serial</label><input id="reg-serial" required maxlength="100"></div></div><div class="form-actions"><button type="submit">Đăng ký sản phẩm</button><span>Nhập mã sản phẩm và serial đã được nhà cung cấp cấp.</span></div></form><aside class="content-card"><h2>Lưu ý</h2><p class="side-copy">Sản phẩm chỉ được liên kết nếu mã và serial khớp dữ liệu hệ thống và chưa được đăng ký bởi tài khoản khác.</p></aside></section>`;
    if (currentPage === 'warranty-request') return `${heading(title)}<section class="subpage-grid"><form class="content-card" id="request-form"><h2>Thông tin yêu cầu</h2><div class="field-grid"><div class="field field-full"><label for="req-product">Sản phẩm</label><select id="req-product" required><option value="">Chọn sản phẩm</option>${products.map(p => `<option value="${p.id}">${esc(p.productName)} · ${esc(p.serialNumber)}</option>`).join('')}</select></div><div class="field field-full"><label for="req-description">Mô tả tình trạng</label><textarea id="req-description" required minlength="5" maxlength="2000"></textarea></div></div><div class="form-actions"><button type="submit" ${products.length ? '' : 'disabled'}>Gửi yêu cầu</button><span>${products.length ? 'Yêu cầu sẽ được lưu và chuyển tới bộ phận bảo hành.' : 'Bạn cần đăng ký sản phẩm trước khi gửi yêu cầu.'}</span></div></form></section>`;
    if (currentPage === 'profile') return `${heading(title)}<section class="content-card profile-grid"><div><span class="profile-large-avatar">${esc((account.fullName || account.username || 'U').slice(0, 1).toUpperCase())}</span></div><div class="profile-details">${[['Tên đăng nhập', account.username], ['Họ tên', account.fullName], ['Email', account.email], ['Số điện thoại', account.phoneNumber], ['Vai trò', account.role], ['Ngày tạo', fmtDate(account.createdAt)]].map(([k, v]) => `<div class="profile-item"><small>${k}</small><b>${esc(v || '—')}</b></div>`).join('')}</div></section>`;
    const covered = products.filter(p => p.isUnderWarranty).length;
    const open = requests.filter(r => !['Completed', 'Rejected'].includes(r.status)).length;
    return `${heading(title)}<section class="welcome-banner"><div class="welcome-copy"><span class="welcome-tag">KHU VỰC KHÁCH HÀNG E-WARRANTY</span><h1>Xin chào, <span>${esc(account.fullName || account.username || 'Khách hàng')}</span> 👋</h1><p>Quản lý bảo hành sản phẩm của bạn một cách dễ dàng và nhanh chóng.</p></div></section><section class="stat-grid">${stat('Sản phẩm của tôi', products.length, '')}${stat('Còn bảo hành', covered, 'green')}${stat('Yêu cầu đang xử lý', open, 'orange')}</section>${productsBlock(3)}${requestsBlock()}`;
  };
  const render = () => { el('user-main').innerHTML = pageMarkup(); };
  const bind = () => {
    document.querySelectorAll('.user-nav-link').forEach(link => {
      const key = ({ dashboard: 'dashboard', products: 'product', 'warranty-register': 'product', 'warranty-request': 'warranti', requests: 'warranti', history: 'warranti' })[link.dataset.page];
      const permission = permissions.get(key);
      link.hidden = link.dataset.page !== 'profile' && !(permission?.menuVisible && permission?.canView);
      link.classList.toggle('active', link.dataset.page === currentPage);
    });
    el('account-name').textContent = account.fullName || account.username || 'Khách hàng';
    el('user-avatar').textContent = (account.fullName || account.username || 'U').slice(0, 1).toUpperCase();
    render();
  };
  document.addEventListener('submit', async event => {
    if (event.target.id === 'register-form') {
      event.preventDefault(); const button = event.target.querySelector('button[type=submit]'); button.disabled = true;
      try { await api('/api/products/register', { method: 'POST', body: JSON.stringify({ productCode: el('reg-code').value.trim(), serialNumber: el('reg-serial').value.trim() }) }); toast('Đăng ký sản phẩm thành công.'); products = await api('/api/products/mine'); currentPage = 'products'; history.replaceState({}, '', '/user/products'); bind(); }
      catch (error) { toast(error.message); button.disabled = false; }
    }
    if (event.target.id === 'request-form') {
      event.preventDefault(); const button = event.target.querySelector('button[type=submit]'); button.disabled = true;
      try { await api('/api/warranty-requests', { method: 'POST', body: JSON.stringify({ productId: Number(el('req-product').value), description: el('req-description').value.trim() }) }); toast('Đã gửi yêu cầu bảo hành.'); requests = await api('/api/warranty-requests/mine'); currentPage = 'requests'; history.replaceState({}, '', '/user/requests'); bind(); }
      catch (error) { toast(error.message); button.disabled = false; }
    }
  });
  const logout = () => { sessionStorage.removeItem('eWarrantyToken'); localStorage.removeItem('eWarrantyToken'); location.replace('/login'); };
  el('sidebar-logout').addEventListener('click', logout); el('menu-logout').addEventListener('click', logout);
  el('mobile-menu').addEventListener('click', () => document.body.classList.add('sidebar-open'));
  el('mobile-shade').addEventListener('click', () => document.body.classList.remove('sidebar-open'));
  el('account-button').addEventListener('click', () => { const menu = el('account-menu'); menu.hidden = !menu.hidden; });
  el('global-search').addEventListener('input', event => { const query = event.target.value.toLocaleLowerCase('vi-VN'); document.querySelectorAll('[data-search]').forEach(node => node.hidden = !node.dataset.search.toLocaleLowerCase('vi-VN').includes(query)); });
  (async () => {
    try {
      const [profile, permissionData] = await Promise.all([api('/api/account/me'), api('/api/admin/permissions/User')]);
      account = profile; permissions = new Map((permissionData.modules || []).map(item => [item.moduleKey, item.permission || {}]));
      if (permissions.get('product')?.canView) products = await api('/api/products/mine');
      if (permissions.get('warranti')?.canView) requests = await api('/api/warranty-requests/mine');
      bind();
    } catch (error) { if (error.message) { el('user-main').innerHTML = `<section class="section-panel"><h1>Không tải được dữ liệu</h1><p>${esc(error.message)}</p><button onclick="location.reload()">Thử lại</button></section>`; } }
  })();
})();
