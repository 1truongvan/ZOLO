(() => {
  const token = sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const loginUrl = '/login';
  const parseClaims = value => {
    try {
      const part = value.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const base64 = part.padEnd(Math.ceil(part.length / 4) * 4, '=');
      const binary = atob(base64);
      const payload = JSON.parse(new TextDecoder().decode(Uint8Array.from(binary, ch => ch.charCodeAt(0))));
      const claim = suffix => Object.entries(payload).find(([key]) => key === suffix || key.endsWith('/' + suffix))?.[1];
      const roleClaim = claim('role');
      return {
        name: claim('unique_name') || claim('name') || 'Khách hàng',
        id: claim('nameid') || claim('sub') || claim('nameidentifier') || '',
        role: Array.isArray(roleClaim) ? roleClaim[0] : roleClaim,
        expiry: Number(payload.exp)
      };
    } catch { return null; }
  };

  const claims = token ? parseClaims(token) : null;
  const role = String(claims?.role || '').toLowerCase();
  if (!claims || claims.expiry <= Math.floor(Date.now() / 1000)) {
    sessionStorage.removeItem('eWarrantyToken');
    localStorage.removeItem('eWarrantyToken');
    window.location.replace(loginUrl);
    return;
  }
  if (role === 'admin') { window.location.replace('/admin/dashboard'); return; }
  if (role === 'staff') { window.location.replace('/staff/dashboard'); return; }
  if (role === 'manager') { window.location.replace('/manager/dashboard'); return; }
  if (role === 'technician') { window.location.replace('/technician/dashboard'); return; }
  if (role !== 'user' && role !== 'customer') { window.location.replace('/'); return; }

  // Demo-only records. There is no User↔Product relationship or warranty-request API in the backend yet.
  const sampleProducts = [
    { name: 'Laptop ASUS Vivobook 15', serial: 'ASV15-2024001', expiry: '20/12/2026', status: 'Còn bảo hành', kind: 'laptop' },
    { name: 'iPhone 16', serial: 'IP16-2024010', expiry: '15/01/2027', status: 'Còn bảo hành', kind: 'phone' },
    { name: 'Tai nghe Sony WH-1000XM5', serial: 'SN-2024005', expiry: '30/03/2027', status: 'Còn bảo hành', kind: 'headphones' },
    { name: 'Máy tính bảng Galaxy Tab', serial: 'GT-2023008', expiry: '10/05/2025', status: 'Hết bảo hành', kind: 'tablet' }
  ];
  const sampleRequests = [
    { id: 'WC-2025-0012', product: 'iPhone 16', sent: '18/11/2025 14:30', status: 'Đang kiểm tra', type: 'checking' },
    { id: 'WC-2025-0009', product: 'Laptop ASUS Vivobook 15', sent: '10/11/2025 09:15', status: 'Đã tiếp nhận', type: 'received' }
  ];
  const routes = {
    dashboard: { title: 'Tổng quan', crumb: 'Tổng quan' },
    products: { title: 'Sản phẩm của tôi', crumb: 'Sản phẩm của tôi' },
    'warranty-register': { title: 'Đăng ký bảo hành', crumb: 'Đăng ký bảo hành' },
    'warranty-request': { title: 'Gửi yêu cầu bảo hành', crumb: 'Gửi yêu cầu bảo hành' },
    requests: { title: 'Theo dõi yêu cầu', crumb: 'Theo dõi yêu cầu' },
    history: { title: 'Lịch sử bảo hành', crumb: 'Lịch sử bảo hành' },
    profile: { title: 'Hồ sơ cá nhân', crumb: 'Hồ sơ cá nhân' }
  };
  const pathPage = window.location.pathname.split('/').filter(Boolean).pop();
  const page = routes[pathPage] ? pathPage : 'dashboard';
  const pageInfo = routes[page];
  const byId = id => document.getElementById(id);
  const safe = value => String(value).replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
  const initials = [...String(claims.name).trim()][0]?.toLocaleUpperCase('vi-VN') || 'U';
  byId('user-avatar').textContent = initials;
  byId('account-name').textContent = claims.name;

  const icons = {
    laptop: '<svg viewBox="0 0 100 82" aria-hidden="true"><rect x="18" y="9" width="64" height="46" rx="4"/><path d="M10 64h80l-7 8H17zM26 17h48v31H26z"/><path d="M40 64h20"/></svg>',
    phone: '<svg viewBox="0 0 100 82" aria-hidden="true"><rect x="34" y="4" width="34" height="70" rx="7"/><path d="M39 12h24v49H39zM47 67h8"/><circle cx="51" cy="9" r="1"/></svg>',
    headphones: '<svg viewBox="0 0 100 82" aria-hidden="true"><path d="M20 44V35a30 30 0 0 1 60 0v9"/><rect x="16" y="39" width="16" height="29" rx="7"/><rect x="68" y="39" width="16" height="29" rx="7"/><path d="M77 67c-3 7-11 10-21 10h-8"/><rect x="39" y="72" width="13" height="6" rx="3"/></svg>',
    tablet: '<svg viewBox="0 0 100 82" aria-hidden="true"><rect x="25" y="5" width="50" height="71" rx="6"/><path d="M31 12h38v55H31z"/><circle cx="50" cy="72" r="1"/></svg>'
  };
  const demoNotice = '<div class="demo-notice"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 11v5m0-8h.01"/></svg><span><b>Chế độ minh họa:</b> thông tin sản phẩm và yêu cầu bên dưới là dữ liệu mẫu ở giao diện, không lấy từ tài khoản thật và không được lưu vào database. Backend hiện chưa có liên kết khách hàng–sản phẩm hoặc API bảo hành.</span></div>';
  const heading = title => `<div class="user-page-head"><div><div class="breadcrumbs"><a href="/user/dashboard">Khu vực của tôi</a><span>›</span><b>${safe(title)}</b></div><h1 class="page-title">${safe(title)}</h1></div></div>`;
  const productCard = (product, index) => `<article class="product-card" data-search="${safe(product.name + ' ' + product.serial)}"><div class="product-visual">${icons[product.kind]}</div><div class="product-copy"><h3>${safe(product.name)}</h3><div class="product-meta"><span>SN: ${safe(product.serial)}</span><span><svg viewBox="0 0 24 24"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 10h18"/></svg>Hết hạn: ${safe(product.expiry)}</span><span class="warranty-pill ${index === 3 ? 'expired' : ''}">${safe(product.status)}</span></div></div><div class="product-actions"><button type="button" data-demo-action="Chi tiết sản phẩm">Xem chi tiết →</button><a href="/user/warranty-request">Gửi yêu cầu</a></div></article>`;
  const requestRows = sampleRequests.map(item => `<tr class="history-row" data-search="${safe(item.id + ' ' + item.product)}"><td>${safe(item.id)}<small class="sample-label">Mẫu</small></td><td>${safe(item.product)}</td><td>${safe(item.sent)}</td><td><span class="request-pill ${item.type}">${safe(item.status)}</span></td><td><button class="text-action" type="button" data-demo-action="Chi tiết yêu cầu">Xem chi tiết →</button></td></tr>`).join('');

  const dashboard = () => `${demoNotice}${heading(pageInfo.crumb)}<section class="welcome-banner"><div class="welcome-copy"><span class="welcome-tag">KHU VỰC KHÁCH HÀNG E-WARRANTY</span><h1>Xin chào, <span>${safe(claims.name)}</span> <span aria-hidden="true">👋</span></h1><p><b>Chào mừng bạn quay trở lại với <strong>E-Warranty!</strong></b></p><p>Quản lý bảo hành sản phẩm của bạn một cách dễ dàng và nhanh chóng.</p></div><div class="welcome-art" aria-hidden="true"><svg viewBox="0 0 450 180"><circle cx="290" cy="86" r="79" fill="#fff" opacity=".22"/><path d="M65 125h156l-15 20H80z" fill="#244c78"/><rect x="82" y="61" width="121" height="64" rx="7" fill="#122841"/><rect x="90" y="68" width="105" height="49" rx="3" fill="#4578a9"/><rect x="266" y="76" width="47" height="78" rx="9" fill="#142943"/><rect x="271" y="83" width="37" height="62" rx="4" fill="#6c9ac4"/><path d="M366 65a35 35 0 0 1 70 0v25h-12V65a23 23 0 0 0-46 0v25h-12z" fill="#1e3a59"/><rect x="361" y="84" width="19" height="43" rx="9" fill="#274968"/><rect x="422" y="84" width="19" height="43" rx="9" fill="#274968"/><path d="m250 30 35 13v28c0 23-13 37-35 49-22-12-35-26-35-49V43z" fill="#12b9bd" stroke="#fff" stroke-width="5"/><path d="m235 70 11 11 21-24" fill="none" stroke="#fff" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/><path d="m199 36-9-14m34-3-1-17m45 18 9-13" stroke="#2686eb" stroke-width="4" stroke-linecap="round"/></svg></div></section><section class="stat-grid"><a class="stat-card" href="/user/products"><span class="stat-icon"><svg viewBox="0 0 24 24"><path d="m12 3 9 5-9 5-9-5zM3 8v9l9 5 9-5V8m-9 5v9"/></svg></span><span><small class="stat-label">Sản phẩm của tôi <i>Dữ liệu mẫu</i></small><b class="stat-number">4</b></span><span class="stat-link">→</span></a><a class="stat-card" href="/user/products"><span class="stat-icon green"><svg viewBox="0 0 24 24"><path d="m12 3 8 3v6c0 5-3 8-8 10-5-2-8-5-8-10V6z"/><path d="m8 12 3 3 5-6"/></svg></span><span><small class="stat-label">Còn bảo hành <i>Dữ liệu mẫu</i></small><b class="stat-number">3</b></span><span class="stat-link">→</span></a><a class="stat-card" href="/user/requests"><span class="stat-icon orange"><svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg></span><span><small class="stat-label">Yêu cầu đang xử lý <i>Dữ liệu mẫu</i></small><b class="stat-number">1</b></span><span class="stat-link">→</span></a></section><section class="section-panel"><div class="panel-title-row"><h2 class="panel-title"><svg viewBox="0 0 24 24"><path d="m12 3 9 5-9 5-9-5zM3 8v9l9 5 9-5V8m-9 5v9"/></svg>Sản phẩm của tôi <small class="sample-label">Dữ liệu minh họa</small></h2><a class="primary-link" href="/user/warranty-request"><span>➤</span> Gửi yêu cầu bảo hành</a></div><div class="product-grid">${sampleProducts.slice(0, 3).map(productCard).join('')}</div></section><section class="section-panel"><div class="panel-title-row"><h2 class="panel-title"><svg viewBox="0 0 24 24"><path d="M3 12a9 9 0 1 0 3-6.7L3 8m0-5v5h5m4-1v5l4 2"/></svg>Lịch sử yêu cầu <small class="sample-label">Dữ liệu minh họa</small></h2><a class="secondary-link" href="/user/requests">Xem tất cả →</a></div>${requestTable()}</section><footer class="user-footer">E-Warranty · Khu vực khách hàng <span>·</span> Nội dung minh họa được tách riêng khỏi dữ liệu backend.</footer>`;
  const requestTable = () => `<div class="history-table-wrap"><table><thead><tr><th>Mã yêu cầu</th><th>Sản phẩm</th><th>Ngày gửi</th><th>Trạng thái</th><th></th></tr></thead><tbody>${requestRows}</tbody></table></div>`;
  const productsPage = () => `${demoNotice}${heading('Sản phẩm của tôi')}<section class="section-panel"><div class="panel-title-row"><h2 class="panel-title">Danh sách sản phẩm <small class="sample-label">Dữ liệu minh họa · ${sampleProducts.length} sản phẩm</small></h2><a class="primary-link" href="/user/warranty-register">+ Đăng ký sản phẩm</a></div><div class="product-grid">${sampleProducts.map(productCard).join('')}</div></section>`;
  const requestsPage = title => `${demoNotice}${heading(title)}<section class="section-panel"><div class="panel-title-row"><h2 class="panel-title">Yêu cầu bảo hành <small class="sample-label">Dữ liệu minh họa</small></h2><a class="primary-link" href="/user/warranty-request">+ Gửi yêu cầu</a></div>${requestTable()}</section>`;
  const registerPage = () => `${demoNotice}${heading('Đăng ký bảo hành')}<section class="subpage-grid"><form class="content-card demo-form"><h2>Thông tin sản phẩm</h2><div class="field-grid"><div class="field"><label for="reg-code">Mã sản phẩm</label><input id="reg-code" placeholder="Nhập mã trên sản phẩm" required></div><div class="field"><label for="reg-serial">Số serial</label><input id="reg-serial" placeholder="Nhập số serial" required></div><div class="field"><label for="reg-date">Ngày mua</label><input id="reg-date" type="date" required></div><div class="field"><label for="reg-store">Nơi mua</label><input id="reg-store" placeholder="Tên cửa hàng"></div><div class="field field-full"><label for="reg-proof">Thông tin hóa đơn</label><input id="reg-proof" type="text" placeholder="Mã hóa đơn (nếu có)"></div></div><div class="form-actions"><button type="button" disabled>Đăng ký bảo hành</button><span>Chưa có API đăng ký; biểu mẫu chưa gửi hoặc lưu dữ liệu.</span></div></form><aside class="content-card"><h2>Lưu ý</h2><p class="side-copy">Chuẩn bị mã sản phẩm, số serial và thông tin mua hàng. Khi backend có API đăng ký, biểu mẫu này mới có thể gửi yêu cầu thực tế.</p></aside></section>`;
  const warrantyRequestPage = () => `${demoNotice}${heading('Gửi yêu cầu bảo hành')}<section class="subpage-grid"><form class="content-card"><h2>Thông tin yêu cầu</h2><div class="field-grid"><div class="field field-full"><label for="req-product">Sản phẩm</label><select id="req-product"><option>Chọn sản phẩm</option>${sampleProducts.map(p => `<option>${safe(p.name)} · ${safe(p.serial)}</option>`).join('')}</select></div><div class="field"><label for="req-type">Loại yêu cầu</label><select id="req-type"><option>Kiểm tra sản phẩm</option><option>Sửa chữa</option><option>Khác</option></select></div><div class="field"><label for="req-contact">Số điện thoại liên hệ</label><input id="req-contact" placeholder="Nhập số điện thoại"></div><div class="field field-full"><label for="req-description">Mô tả tình trạng</label><textarea id="req-description" placeholder="Mô tả vấn đề bạn gặp phải"></textarea></div></div><div class="form-actions"><button type="button" disabled>Gửi yêu cầu</button><span>Chưa có API tiếp nhận yêu cầu bảo hành.</span></div></form><aside class="content-card"><h2>Hỗ trợ</h2><p class="side-copy">Khi chức năng được tích hợp, bạn có thể theo dõi tiến độ xử lý tại mục “Theo dõi yêu cầu”.</p></aside></section>`;
  const profilePage = () => `${demoNotice}${heading('Hồ sơ cá nhân')}<section class="content-card profile-grid"><div><span class="profile-large-avatar">${safe(initials)}</span><p class="side-copy">Thông tin xác thực đọc từ JWT.</p></div><div class="profile-details"><div class="profile-item"><small>Tên đăng nhập</small><b>${safe(claims.name)}</b></div><div class="profile-item"><small>Vai trò</small><b>${safe(claims.role)}</b></div><div class="profile-item"><small>User ID</small><b>${safe(claims.id || 'Không có trong token')}</b></div><div class="profile-item"><small>Email</small><b>Chưa có API hồ sơ người dùng</b></div><div class="profile-item"><small>Số điện thoại</small><b>Chưa có API hồ sơ người dùng</b></div></div></section>`;

  const content = byId('user-main');
  content.innerHTML = page === 'dashboard' ? dashboard()
    : page === 'products' ? productsPage()
      : page === 'warranty-register' ? registerPage()
        : page === 'warranty-request' ? warrantyRequestPage()
          : page === 'requests' ? requestsPage('Theo dõi yêu cầu bảo hành')
            : page === 'history' ? requestsPage('Lịch sử bảo hành') : profilePage();
  document.querySelectorAll('.user-nav-link').forEach(link => {
    const active = link.dataset.page === page;
    link.classList.toggle('active', active);
    if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current');
  });

  let toastTimer;
  const showToast = text => {
    const toast = byId('toast'); toast.textContent = text; toast.classList.add('show');
    window.clearTimeout(toastTimer); toastTimer = window.setTimeout(() => toast.classList.remove('show'), 2800);
  };
  const logout = () => {
    sessionStorage.removeItem('eWarrantyToken'); localStorage.removeItem('eWarrantyToken');
    window.location.replace(loginUrl);
  };
  byId('sidebar-logout').addEventListener('click', logout);
  byId('menu-logout').addEventListener('click', logout);
  byId('notification-button').addEventListener('click', () => showToast('Chức năng thông báo chưa được kết nối với backend.'));
  document.querySelectorAll('[data-demo-action]').forEach(button => button.addEventListener('click', () => showToast(`${button.dataset.demoAction}: đây là dữ liệu minh họa, chưa có chức năng backend.`)));
  byId('mobile-menu').addEventListener('click', () => document.body.classList.add('sidebar-open'));
  byId('mobile-shade').addEventListener('click', () => document.body.classList.remove('sidebar-open'));
  document.querySelectorAll('.user-nav-link').forEach(link => link.addEventListener('click', () => document.body.classList.remove('sidebar-open')));
  const accountButton = byId('account-button'); const accountMenu = byId('account-menu');
  accountButton.addEventListener('click', () => {
    const open = accountButton.getAttribute('aria-expanded') === 'true';
    accountButton.setAttribute('aria-expanded', String(!open)); accountMenu.hidden = open;
  });
  document.addEventListener('click', event => {
    if (!event.target.closest('.account-wrap')) { accountButton.setAttribute('aria-expanded', 'false'); accountMenu.hidden = true; }
  });
  byId('global-search').addEventListener('input', event => {
    const term = event.target.value.trim().toLocaleLowerCase('vi-VN');
    document.querySelectorAll('[data-search]').forEach(card => {
      card.hidden = !card.dataset.search.toLocaleLowerCase('vi-VN').includes(term);
    });
  });
  const userModuleByPage={dashboard:'dashboard',products:'product','warranty-register':'product','warranty-request':'warranti',requests:'warranti',history:'warranti'};
  fetch('/api/admin/permissions/User',{headers:{Authorization:`Bearer ${token}`,Accept:'application/json'},cache:'no-store'}).then(async response=>{
    if(response.status===401){window.location.replace(loginUrl);return null}
    if(!response.ok)throw new Error(`HTTP ${response.status}`);
    return response.json();
  }).then(data=>{
    if(!data)return;
    const permissions=new Map(data.modules.map(module=>[module.moduleKey,module.permission||{}]));
    let currentAllowed=page==='profile';
    document.querySelectorAll('.user-nav-link').forEach(link=>{
      const key=userModuleByPage[link.dataset.page];
      const permission=key&&permissions.get(key);
      const allowed=link.dataset.page==='profile'||!!permission?.menuVisible&&!!permission?.canView;
      link.hidden=!allowed;
      if(link.dataset.page===page)currentAllowed=allowed;
    });
    if(!currentAllowed)byId('user-main').innerHTML='<section class="section-panel"><h1>Chưa được cấp quyền</h1><p>Quyền truy cập chức năng này chưa được quản trị viên cấp.</p></section>';
  }).catch(()=>{document.querySelectorAll('.user-nav-link').forEach(link=>{if(link.dataset.page!=='profile')link.hidden=true});byId('user-main').innerHTML='<section class="section-panel"><h1>Không tải được quyền truy cập</h1><p>Hãy tải lại trang hoặc đăng nhập lại.</p></section>'});
  const expiryTimer = window.setInterval(() => {
    if (claims.expiry <= Math.floor(Date.now() / 1000)) {
      window.clearInterval(expiryTimer); logout();
    }
  }, 15000);
})();
