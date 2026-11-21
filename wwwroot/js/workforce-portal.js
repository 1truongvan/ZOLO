(() => {
  const role = document.body.dataset.role;
  const token = sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const login = () => { sessionStorage.removeItem('eWarrantyToken'); localStorage.removeItem('eWarrantyToken'); location.replace('/login'); };
  const decode = value => { try { const part=value.split('.')[1].replace(/-/g,'+').replace(/_/g,'/'); return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(part.padEnd(Math.ceil(part.length/4)*4,'=')),c=>c.charCodeAt(0)))); } catch { return null; } };
  const claims = token && decode(token);
  const roleClaim = claims && Object.entries(claims).find(([key]) => key === 'role' || key.endsWith('/role'))?.[1];
  const actualRole = String(Array.isArray(roleClaim) ? roleClaim[0] : roleClaim || '').toLowerCase();
  if (!claims || (claims.exp && claims.exp * 1000 <= Date.now())) { login(); return; }
  if (actualRole !== role) {
    const destinations = { admin:'/admin/dashboard', user:'/user/dashboard', customer:'/user/dashboard', staff:'/staff/dashboard', manager:'/manager/dashboard', technician:'/technician/dashboard' };
    location.replace(destinations[actualRole] || '/'); return;
  }

  const manager = role === 'manager';
  const menus = manager
    ? [['dashboard','◫','Dashboard'],['requests','▣','Quản lý yêu cầu bảo hành'],['assign','♧','Phân công kỹ thuật viên'],['technicians','♙','Quản lý kỹ thuật viên'],['customers','♙','Quản lý khách hàng'],['products','⬡','Quản lý sản phẩm'],['progress','◷','Giám sát tiến độ'],['review','✓','Kiểm tra kết quả sửa chữa'],['reports','▥','Báo cáo thống kê'],['profile','♙','Hồ sơ cá nhân']]
    : [['dashboard','◫','Dashboard'],['assigned','▣','Công việc được giao'],['working','⚒','Đang sửa chữa'],['inspect','⌕','Kiểm tra sản phẩm'],['update','✎','Cập nhật sửa chữa'],['parts','⬡','Linh kiện thay thế'],['approval','✓','Chờ xác nhận'],['history','◷','Lịch sử công việc'],['profile','♙','Hồ sơ cá nhân']];
  const $ = selector => document.querySelector(selector);
  const nav = $('#portal-nav');
  const content = $('#portal-content');
  const pageName = $('#page-name');
  const toast = text => { const el=$('#toast'); el.textContent=text; el.classList.add('show'); setTimeout(()=>el.classList.remove('show'),2300); };
  const esc = value => String(value ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const name = claims.name || claims.unique_name || claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || (manager?'Manager':'Technician');
  $('#account-name').textContent = name;
  let activeMenus = menus.filter(item => item[0] === 'profile');
  let portalRights = new Map();
  const canModule = (key, operation, detail) => { const p=portalRights.get(key);const allowed=!!p?.[`can${operation[0].toUpperCase()}${operation.slice(1)}`];return allowed&&(!detail||!!p.details?.some(item=>item.permissionKey===detail&&item.isGranted)); };
  const renderNavigation = active => { nav.innerHTML = activeMenus.map(([id,icon,label])=>`<button class="nav-link${id===active?' active':''}" data-page="${id}"><span class="nav-icon">${icon}</span>${label}</button>`).join('') + '<button class="nav-link" id="nav-logout"><span class="nav-icon">⇥</span>Đăng xuất</button>'; };
  const empty = (title='Chưa có dữ liệu', detail='Backend hiện chưa có API hoặc dữ liệu cho chức năng này.') => `<div class="empty-state"><div><span class="empty-icon">…</span><b>${title}</b>${detail}</div></div>`;
  const panel = (title, body, subtitle='') => `<article class="panel"><div class="panel-heading"><h2>${title}</h2><small>${subtitle}</small></div>${body}</article>`;
  const unavailable = '<div class="workflow-note"><b>Chức năng chưa kết nối backend</b>Source hiện chưa có Entity hoặc API cho nghiệp vụ phiếu bảo hành, phân công, sửa chữa, linh kiện và tiến độ. Màn hình này không hiển thị số liệu giả và các thao tác chưa được bật.</div>';
  const stat = (icon,label) => `<article class="stat-card"><span class="stat-icon">${icon}</span><div><small>${label}</small><strong class="na">Chưa có API</strong></div></article>`;
  const managerDashboard = () => `${unavailable}<section class="welcome"><span class="eyebrow">MANAGER PORTAL</span><h1>Chào mừng, ${esc(name)}!</h1><p>Quản lý hiệu quả quy trình bảo hành khi dữ liệu nghiệp vụ được tích hợp.</p></section><section class="stat-grid">${stat('▣','Tổng số yêu cầu')}${stat('◷','Yêu cầu chờ duyệt')}${stat('⚒','Đang sửa chữa')}${stat('✓','Yêu cầu hoàn thành')}${stat('♙','Số kỹ thuật viên')}</section><div class="columns">${panel('Thống kê yêu cầu bảo hành','<div class="chart-empty">Chưa có API thống kê để dựng biểu đồ.</div>','Chưa có dữ liệu')}${panel('Trạng thái kỹ thuật viên',empty('Chưa có dữ liệu nhân viên'))}</div>${panel('Danh sách yêu cầu mới',empty('Chưa có yêu cầu bảo hành','Chưa có Entity/API yêu cầu bảo hành; chưa thể tiếp nhận hoặc phân công.'))}`;
  const detailFields = ['Mã phiếu','Thông tin khách hàng','Thông tin sản phẩm','Serial Number','Mô tả lỗi','Hình ảnh lỗi','Kết quả kiểm tra','Nguyên nhân lỗi','Nội dung sửa chữa','Linh kiện thay thế','Kết quả xử lý'];
  const renderDetail = () => panel('Chi tiết phiếu sửa chữa',`<div class="detail-grid">${detailFields.map(field=>`<div class="detail-field"><small>${field}</small><b>Chưa có dữ liệu phiếu sửa chữa</b></div>`).join('')}</div><div class="disabled-actions"><button disabled>Cập nhật tiến độ · chưa có API</button><button disabled>Gửi kết quả cho Manager · chưa có API</button></div>`);
  const technicianDashboard = () => `${unavailable}<section class="welcome"><span class="eyebrow">TECHNICIAN PORTAL</span><h1>Xin chào, ${esc(name)}!</h1><p>Danh sách công việc sẽ hiển thị tại đây sau khi hệ thống có API phân công.</p></section><section class="stat-grid">${stat('▣','Công việc mới được giao')}${stat('⚒','Công việc đang xử lý')}${stat('◷','Công việc chờ xác nhận')}${stat('✓','Công việc đã hoàn thành')}</section>${panel('Danh sách công việc được phân công',empty('Chưa có công việc được giao','Chưa có dữ liệu hoặc API phân công công việc.'))}${renderDetail()}`;
  const pageContent = id => {
    if (id==='dashboard') return manager?managerDashboard():technicianDashboard();
    const selected=activeMenus.find(item=>item[0]===id);
    if (id==='profile') return `<h1 class="module-title">Hồ sơ cá nhân</h1><p class="module-subtitle">Thông tin đọc từ JWT hiện tại.</p>${panel('Tài khoản',`<div class="detail-grid"><div class="detail-field"><small>Tên</small><b>${esc(name)}</b></div><div class="detail-field"><small>Vai trò</small><b>${manager?'Manager':'Technician'}</b></div><div class="detail-field"><small>Email</small><b>${esc(claims.email || 'Không có trong token')}</b></div></div>`)}`;
    if (id==='review' || id==='approval') return unavailable + renderDetail();
    return `${unavailable}<h1 class="module-title">${esc(selected?.[2] || 'Chức năng')}</h1><p class="module-subtitle">Màn hình đã tạo cho phân hệ ${esc(selected?.[2] || '')}; cần API và dữ liệu backend trước khi bật nghiệp vụ.</p>${panel(esc(selected?.[2] || 'Dữ liệu'),empty('Chưa thể tải dữ liệu','Không tìm thấy API tương ứng trong dự án hiện tại.'))}`;
  };
  const showPage = id => {
    const selected=activeMenus.find(item=>item[0]===id) || activeMenus[0];
    if(!selected){content.innerHTML=empty('Chưa được cấp quyền xem chức năng nào');pageName.textContent='Không có quyền';return}
    document.querySelectorAll('.nav-link[data-page]').forEach(link=>link.classList.toggle('active',link.dataset.page===selected[0]));
    pageName.textContent=selected[2]; content.innerHTML=pageContent(selected[0]);
  };
  nav.addEventListener('click',event=>{const link=event.target.closest('[data-page]');if(link)showPage(link.dataset.page);if(event.target.closest('#nav-logout'))login();});
  $('#logout').addEventListener('click',login); $('#notifications').addEventListener('click',()=>toast('Chưa có API thông báo.'));
  $('#mobile-menu').addEventListener('click',()=>$('#portal').classList.toggle('menu-open'));
  $('#portal-search').addEventListener('keydown',event=>{if(event.key==='Enter')toast('Tìm kiếm chưa khả dụng vì chưa có API nghiệp vụ.');});
  const requestApi=async(url,options={})=>{const response=await fetch(url,{...options,headers:{Authorization:`Bearer ${token}`,Accept:'application/json',...(options.body?{'Content-Type':'application/json'}:{}),...options.headers}});const body=await response.json().catch(()=>null);if(!response.ok)throw Error(body?.message||`API trả về HTTP ${response.status}`);return body;};
  async function loadWorkflow(){
    try{
      if(manager){
        const summary=await requestApi('/api/warranty-requests/summary');
        const strong=[...content.querySelectorAll('.stat-card strong')];[summary.total,summary.pending,summary.inProgress,summary.completed,summary.technicians].forEach((value,index)=>{if(strong[index])strong[index].textContent=Number(value||0).toLocaleString('vi-VN')});
        content.querySelector('.workflow-note')?.remove();
        const chart=content.querySelector('.chart-empty');if(chart){const values=[['Đã gửi',summary.pending],['Đang sửa',summary.inProgress],['Chờ duyệt kết quả',summary.readyForReview],['Hoàn thành',summary.completed]],max=Math.max(1,...values.map(item=>item[1]));chart.innerHTML=`<div style="width:85%;display:grid;gap:8px">${values.map(([label,value])=>`<div style="display:grid;grid-template-columns:125px 1fr 35px;align-items:center;gap:8px;text-align:left"><span>${label}</span><span style="height:9px;background:#eef0ff;border-radius:8px;overflow:hidden"><i style="display:block;height:100%;width:${Math.max(value?4:0,value/max*100)}%;background:var(--accent)"></i></span><b>${value}</b></div>`).join('')}</div>`;}
        const panel=content.querySelector('.panel:last-child');if(panel){panel.innerHTML=`<div class="panel-heading"><h2>Danh sách yêu cầu mới</h2><small>${summary.latest.length} gần nhất</small></div>${summary.latest.length?`<div class="table-wrap"><table><thead><tr><th>Mã</th><th>Khách hàng</th><th>Sản phẩm</th><th>Ngày gửi</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${summary.latest.map(item=>{const action=item.status==='Submitted'?'review':item.status==='Approved'?'assign':item.status==='ReadyForReview'?'complete':'detail';const permitted=action==='review'?canModule('warranti','edit','approve'):action==='assign'?canModule('warranti','edit','manage-technicians'):action==='complete'?canModule('warranti','edit','close'):canModule('warranti','view');return `<tr><td>${esc(item.requestCode)}</td><td>${esc(item.customer)}</td><td>${esc(item.product||'—')}</td><td>${new Date(item.createdAt).toLocaleDateString('vi-VN')}</td><td>${esc(item.status)}</td><td>${permitted?`<button class="job-action" data-manager-action="${action}" data-id="${item.id}">Xử lý</button>`:'—'}</td></tr>`}).join('')}</tbody></table></div>`:empty('Chưa có yêu cầu bảo hành')}</div>`;
          panel.querySelectorAll('[data-manager-action]').forEach(button=>button.addEventListener('click',async()=>{try{const id=button.dataset.id,action=button.dataset.managerAction;if(action==='review'){const approved=confirm('Duyệt yêu cầu này? Chọn Hủy để từ chối.');await requestApi(`/api/warranty-requests/${id}/review`,{method:'POST',body:JSON.stringify({approved,note:''})});}else if(action==='assign'){const techs=await requestApi('/api/warranty-requests/technicians');if(!techs.length){toast('Chưa có Technician đang hoạt động.');return}const choice=prompt(techs.map(item=>`${item.id}: ${item.fullName||item.username}`).join('\n')+'\nNhập ID kỹ thuật viên:');if(!choice)return;await requestApi(`/api/warranty-requests/${id}/assign`,{method:'POST',body:JSON.stringify({technicianId:Number(choice)})});}else if(action==='complete'){await requestApi(`/api/warranty-requests/${id}/complete`,{method:'POST',body:JSON.stringify({completed:confirm('Xác nhận đã hoàn tất? Chọn Hủy để yêu cầu xử lý thêm.'),note:''})});}else{const item=await requestApi(`/api/warranty-requests/${id}`);alert(`${item.requestCode}\n${item.description}\n${item.status}`);}toast('Đã cập nhật yêu cầu.');loadWorkflow();}catch(error){toast(error.message)}}));}
      }else{
        const jobs=await requestApi('/api/warranty-requests/assigned');
        const counts={Submitted:0,InProgress:0,ReadyForReview:0,Completed:0};jobs.forEach(job=>{if(counts[job.status]!==undefined)counts[job.status]++});
        const strong=[...content.querySelectorAll('.stat-card strong')];[counts.Submitted,counts.InProgress,counts.ReadyForReview,counts.Completed].forEach((value,index)=>{if(strong[index])strong[index].textContent=String(value)});
        content.querySelector('.workflow-note')?.remove();
        const listPanel=content.querySelector('.panel');if(listPanel)listPanel.innerHTML=`<div class="panel-heading"><h2>Danh sách công việc được phân công</h2><small>${jobs.length} công việc</small></div>${jobs.length?`<div class="table-wrap"><table><thead><tr><th>Mã phiếu</th><th>Sản phẩm</th><th>Mô tả</th><th>Ngày nhận</th><th>Trạng thái</th><th></th></tr></thead><tbody>${jobs.map(job=>`<tr><td>${esc(job.requestCode)}</td><td>${esc(job.product?.productName||'—')}</td><td>${esc(job.description)}</td><td>${new Date(job.createdAt).toLocaleDateString('vi-VN')}</td><td>${esc(job.status)}</td><td><button class="job-action" data-job-id="${job.id}">Chi tiết</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có công việc được giao')}</div>`;
        const detailPanel=content.querySelector('.panel:last-child');
        const showJob=job=>{if(!detailPanel)return;const canRepair=canModule('warranti','edit');detailPanel.innerHTML=`<div class="panel-heading"><h2>Chi tiết phiếu sửa chữa · ${esc(job.requestCode)}</h2><small>${esc(job.status)}</small></div><div class="detail-grid"><div class="detail-field"><small>Khách hàng</small><b>${esc(job.customer?.fullName||job.customer?.username)}</b></div><div class="detail-field"><small>Sản phẩm / Serial</small><b>${esc(job.product?.productName||'—')} · ${esc(job.product?.serialNumber||'—')}</b></div><div class="detail-field"><small>Mô tả lỗi</small><b>${esc(job.description)}</b></div><div class="detail-field"><small>Ghi chú Manager</small><b>${esc(job.managerNotes||'—')}</b></div></div><form class="repair-form" data-repair-id="${job.id}"><label>Kết quả kiểm tra<textarea name="inspectionResult" required>${esc(job.inspectionResult||'')}</textarea></label><label>Nguyên nhân lỗi<textarea name="faultCause">${esc(job.faultCause||'')}</textarea></label><label>Nội dung sửa chữa<textarea name="repairNotes">${esc(job.repairNotes||'')}</textarea></label><label>Kết quả xử lý<textarea name="resolution" required>${esc(job.resolution||'')}</textarea></label>${canRepair?`<div class="disabled-actions"><button ${job.status!=='InProgress'?'disabled':''} type="submit">L?u c?p nh?t</button><button ${job.status!=='InProgress'?'disabled':''} type="button" id="submit-repair-result">G?i k?t qu? cho Manager</button></div>`:''}</form>`;const form=detailPanel.querySelector('.repair-form');form.addEventListener('submit',async event=>{event.preventDefault();const data=new FormData(form);try{await requestApi(`/api/warranty-requests/${job.id}/repair`,{method:'PUT',body:JSON.stringify(Object.fromEntries(data))});toast('Đã lưu cập nhật sửa chữa.');loadWorkflow()}catch(error){toast(error.message)}});detailPanel.querySelector('#submit-repair-result')?.addEventListener('click',async()=>{try{if(form.reportValidity()){const data=new FormData(form);await requestApi(`/api/warranty-requests/${job.id}/repair`,{method:'PUT',body:JSON.stringify(Object.fromEntries(data))});await requestApi(`/api/warranty-requests/${job.id}/submit-result`,{method:'POST'});toast('Đã gửi kết quả cho Manager.');loadWorkflow()}}catch(error){toast(error.message)}})};
        listPanel?.querySelectorAll('[data-job-id]').forEach(button=>button.addEventListener('click',()=>{const job=jobs.find(item=>String(item.id)===button.dataset.jobId);if(job)showJob(job)}));if(jobs.length)showJob(jobs[0]);
      }
    }catch(error){toast(`Không thể tải dữ liệu nghiệp vụ: ${error.message}`)}
  }
  const portalModules={dashboard:'dashboard',requests:'warranti',assign:'warranti',technicians:'warranti',customers:'customer',products:'product',progress:'warranti',review:'warranti',reports:'dashboard',assigned:'warranti',working:'warranti',inspect:'warranti',update:'warranti',parts:'accessary',approval:'warranti',history:'warranti'};
  async function applyPortalPermissions(){
    try{
      const matrix=await requestApi(`/api/admin/permissions/${role==='customer'?'User':role==='manager'?'Manager':'Technician'}`);
      const permissions=new Map(matrix.modules.map(item=>[item.moduleKey,item.permission||{}]));
      portalRights=permissions;
      activeMenus=menus.filter(([id])=>{if(id==='profile')return true;const permission=permissions.get(portalModules[id]);const advanced=['technicians','assign'].includes(id)?'manage-technicians':id==='review'?'approve':null;return !!permission?.menuVisible&&!!permission?.canView&&(!advanced||!!permission.details?.some(item=>item.permissionKey===advanced&&item.isGranted))});
      if(!activeMenus.some(item=>item[0]==='dashboard'))activeMenus=activeMenus.filter(item=>item[0]!=='dashboard');
      renderNavigation(activeMenus[0]?.[0]);
      showPage(activeMenus.some(item=>item[0]==='dashboard')?'dashboard':activeMenus[0]?.[0]);
      if(activeMenus.some(item=>item[0]==='dashboard')&&permissions.get('warranti')?.canView)loadWorkflow();
    }catch(error){activeMenus=menus.filter(item=>item[0]==='profile');renderNavigation('profile');content.innerHTML=empty('Không tải được quyền truy cập',error.message);pageName.textContent='Không có quyền'}
  }
  renderNavigation('');
  applyPortalPermissions();
})();
