(() => {
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  const token = () => sessionStorage.getItem('eWarrantyToken') || localStorage.getItem('eWarrantyToken');
  const claims = () => {
    try {
      const part = token()?.split('.')[1];
      if (!part) return {};
      const raw = part.replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(raw.padEnd(Math.ceil(raw.length / 4) * 4, '=')), c => c.charCodeAt(0))));
    } catch { return {}; }
  };
  const userId = () => {
    const jwt = claims();
    const key = Object.keys(jwt).find(k => k === 'nameid' || k === 'sub' || k.endsWith('/nameidentifier'));
    return Number(key ? jwt[key] : 0);
  };
  const timeText = value => value ? new Date(value).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' }) : '';

  async function open({ requestId, requestCode }) {
    if (!window.signalR) {
      alert('Không tải được thư viện SignalR. Hãy tải lại trang.');
      return;
    }

    document.querySelector('#warranty-chat-dialog')?.remove();
    const dialog = document.createElement('dialog');
    dialog.id = 'warranty-chat-dialog';
    dialog.className = 'warranty-chat-dialog';
    dialog.innerHTML = `
      <header class="warranty-chat-header">
        <div><small>TRAO ĐỔI THEO PHIẾU</small><h2>${esc(requestCode || `#${requestId}`)}</h2></div>
        <button type="button" class="warranty-chat-close" aria-label="Đóng">×</button>
      </header>
      <div class="warranty-chat-status" aria-live="polite">Đang kết nối…</div>
      <div class="warranty-chat-messages" role="log" aria-live="polite" aria-relevant="additions"></div>
      <form class="warranty-chat-form">
        <textarea name="content" rows="2" maxlength="2000" placeholder="Nhập tin nhắn…" required></textarea>
        <button type="submit">Gửi</button>
      </form>`;
    document.body.append(dialog);
    dialog.showModal();

    const status = dialog.querySelector('.warranty-chat-status');
    const list = dialog.querySelector('.warranty-chat-messages');
    const form = dialog.querySelector('.warranty-chat-form');
    const seen = new Set();
    let joined = false;
    let closed = false;

    const renderMessage = message => {
      const id = String(message.id ?? `${message.senderUserId}-${message.sentAt}-${message.content}`);
      if (seen.has(id)) return;
      seen.add(id);
      list.querySelector('.warranty-chat-empty')?.remove();
      const own = Number(message.senderUserId) === userId();
      const article = document.createElement('article');
      article.className = `warranty-chat-message ${own ? 'is-own' : ''}`;
      article.innerHTML = `<div class="warranty-chat-bubble"><b>${esc(message.senderName || 'Người dùng')}</b><p>${esc(message.content)}</p><time>${esc(timeText(message.sentAt))}</time></div>`;
      list.append(article);
      list.scrollTop = list.scrollHeight;
    };

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/warranty-chat', { accessTokenFactory: () => token() || '' })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveMessage', renderMessage);
    connection.onreconnecting(() => { status.textContent = 'Mất kết nối, đang kết nối lại…'; });
    connection.onreconnected(async () => {
      try {
        await connection.invoke('JoinRequest', Number(requestId));
        joined = true;
        status.textContent = 'Đã kết nối';
      } catch (error) { status.textContent = error.message || 'Không kết nối được phiếu chat.'; }
    });
    connection.onclose(() => { if (!closed) status.textContent = 'Mất kết nối chat.'; });

    try {
      await connection.start();
      await connection.invoke('JoinRequest', Number(requestId));
      joined = true;
      status.textContent = 'Đã kết nối';
      const history = await connection.invoke('GetHistory', Number(requestId));
      history.forEach(renderMessage);
      if (!history.length) list.innerHTML = '<p class="warranty-chat-empty">Chưa có tin nhắn. Hãy bắt đầu trao đổi.</p>';
    } catch (error) {
      status.textContent = error.message || 'Không kết nối được chat.';
    }

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const field = form.elements.content;
      const content = field.value.trim();
      const button = form.querySelector('button[type="submit"]');
      if (!content || !connection.state || connection.state !== signalR.HubConnectionState.Connected) return;
      button.disabled = true;
      try {
        await connection.invoke('SendMessage', Number(requestId), content);
        field.value = '';
        field.focus();
      } catch (error) {
        status.textContent = error.message || 'Gửi tin nhắn thất bại.';
      } finally { button.disabled = false; }
    });

    const close = async () => {
      if (closed) return;
      closed = true;
      try { if (joined) await connection.invoke('LeaveRequest', Number(requestId)); } catch { }
      try { await connection.stop(); } catch { }
      dialog.remove();
    };
    dialog.querySelector('.warranty-chat-close').addEventListener('click', close);
    dialog.addEventListener('cancel', event => { event.preventDefault(); close(); });
  }

  document.addEventListener('click', event => {
    const button = event.target.closest('[data-open-warranty-chat]');
    if (!button) return;
    open({ requestId: Number(button.dataset.requestId), requestCode: button.dataset.requestCode || '' });
  });

  window.WarrantyChat = { open };
})();
