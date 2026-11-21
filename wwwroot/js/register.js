(() => {
  const form = document.getElementById('register-form');
  const username = document.getElementById('username');
  const email = document.getElementById('email');
  const password = document.getElementById('password');
  const confirm = document.getElementById('confirm-password');
  const button = document.getElementById('submit-button');
  const message = document.getElementById('form-message');
  const showMessage = (text, success = false) => {
    message.textContent = text;
    message.classList.toggle('visible', Boolean(text));
    message.classList.toggle('success', success);
  };

  form.addEventListener('submit', async event => {
    event.preventDefault();
    showMessage('');
    username.setCustomValidity(username.value.trim().length < 3 ? 'Tên đăng nhập cần ít nhất 3 ký tự.' : '');
    email.setCustomValidity(email.validity.valid ? '' : 'Vui lòng nhập email hợp lệ.');
    password.setCustomValidity(password.value.length < 6 ? 'Mật khẩu cần ít nhất 6 ký tự.' : '');
    confirm.setCustomValidity(confirm.value !== password.value ? 'Mật khẩu xác nhận không khớp.' : '');
    if (!form.reportValidity()) return;
    button.disabled = true;
    button.classList.add('loading');
    try {
      const response = await fetch('/api/auth/register', {
        method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ username: username.value.trim(), email: email.value.trim(), password: password.value })
      });
      const data = await response.json().catch(() => null);
      if (!response.ok) {
        const detail = typeof data === 'string' ? data : (data?.message || data?.title);
        throw new Error(detail || 'Không thể tạo tài khoản. Vui lòng thử lại.');
      }
      form.reset();
      showMessage('Đăng ký thành công. Tài khoản khách hàng của bạn đã được tạo. Chuyển đến trang đăng nhập…', true);
      window.setTimeout(() => window.location.assign('/login'), 1600);
    } catch (error) {
      showMessage(error instanceof TypeError ? 'Không thể kết nối đến máy chủ. Vui lòng thử lại sau.' : (error.message || 'Đăng ký thất bại.'));
    } finally {
      button.disabled = false;
      button.classList.remove('loading');
    }
  });
})();
