(() => {
  const form = document.getElementById('login-form');
  const username = document.getElementById('username');
  const password = document.getElementById('password');
  const remember = document.getElementById('remember');
  const submit = document.getElementById('submit-button');
  const message = document.getElementById('form-message');
  const passwordToggle = document.querySelector('.password-toggle');

  passwordToggle.addEventListener('click', () => {
    const reveal = password.type === 'password';
    password.type = reveal ? 'text' : 'password';
    passwordToggle.setAttribute('aria-pressed', String(reveal));
    passwordToggle.setAttribute('aria-label', reveal ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
  });

  const setMessage = text => {
    message.textContent = text;
    message.classList.toggle('visible', Boolean(text));
  };

  const getRoleFromToken = token => {
    try {
      const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const decoded = new TextDecoder().decode(Uint8Array.from(
        atob(part.padEnd(Math.ceil(part.length / 4) * 4, '=')), character => character.charCodeAt(0)));
      const claims = JSON.parse(decoded);
      const roleClaim = Object.entries(claims).find(([key]) => key === 'role' || key.endsWith('/role'))?.[1];
      return Array.isArray(roleClaim) ? roleClaim[0] : roleClaim;
    } catch { return null; }
  };

  form.addEventListener('submit', async event => {
    event.preventDefault();
    setMessage('');
    username.setCustomValidity('');
    password.setCustomValidity('');
    if (username.value.trim().length < 3) username.setCustomValidity('Vui lòng nhập username có ít nhất 3 ký tự.');
    if (!password.value) password.setCustomValidity('Vui lòng nhập mật khẩu.');
    if (!form.reportValidity()) return;

    submit.disabled = true;
    submit.classList.add('loading');
    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify({ username: username.value.trim(), password: password.value })
      });
      const data = await response.json().catch(() => null);
      if (!response.ok) {
        const detail = typeof data === 'string' ? data : (data?.message || data?.title);
        if (response.status === 401) throw new Error('Username hoặc mật khẩu không chính xác.');
        throw new Error(detail || 'Không thể đăng nhập. Vui lòng thử lại sau.');
      }
      if (!data?.token) throw new Error('Máy chủ không trả về token đăng nhập hợp lệ.');
      const role = getRoleFromToken(data.token);
      if (!role) throw new Error('Token đăng nhập không có thông tin vai trò.');

      // Use tab-only storage by default; persist only when the user selects Remember me.
      sessionStorage.removeItem('eWarrantyToken');
      localStorage.removeItem('eWarrantyToken');
      (remember.checked ? localStorage : sessionStorage).setItem('eWarrantyToken', data.token);
      password.value = '';
      const normalizedRole = String(role).toLowerCase();
      if (normalizedRole === 'admin') {
        window.location.assign('/admin/dashboard');
      } else if (normalizedRole === 'user' || normalizedRole === 'customer') {
        window.location.assign('/user/dashboard');
      } else if (normalizedRole === 'staff') {
        window.location.assign('/staff/dashboard');
      } else if (normalizedRole === 'manager') {
        window.location.assign('/manager/dashboard');
      } else if (normalizedRole === 'technician') {
        window.location.assign('/technician/dashboard');
      } else {
        window.location.assign('/');
      }
    } catch (error) {
      setMessage(error instanceof TypeError
        ? 'Không thể kết nối đến máy chủ. Hãy kiểm tra kết nối và thử lại.'
        : (error.message || 'Đăng nhập thất bại. Vui lòng thử lại.'));
    } finally {
      submit.disabled = false;
      submit.classList.remove('loading');
    }
  });
})();
