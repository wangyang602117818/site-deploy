// 切换顶部主菜单 Tab
function switchMainTab(tabName) {
    document.querySelectorAll('.main-tab-content').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.header-nav-menu .nav-link').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.sidebar-group').forEach(el => el.style.display = 'none');

    if (tabName === 'doc') {
        document.getElementById('tab-doc').style.display = 'block';
        document.getElementById('sidebar-doc-menu').style.display = 'block';
    } else if (tabName === 'license') {
        document.getElementById('tab-license').style.display = 'block';
        document.getElementById('sidebar-other-menu').style.display = 'block';
        checkLoginStatusForLicenseView();
    } else if (tabName === 'download') {
        document.getElementById('tab-download').style.display = 'block';
        document.getElementById('sidebar-other-menu').style.display = 'block';
    } else if (tabName === 'ticket') {
        document.getElementById('tab-ticket').style.display = 'block';
        document.getElementById('sidebar-other-menu').style.display = 'block';
    }

    // 高亮对应的顶部链接
    const activeNav = Array.from(document.querySelectorAll('.header-nav-menu .nav-link')).find(a => a.getAttribute('onclick').includes(tabName));
    if (activeNav) activeNav.classList.add('active');

    ensureSidebarActive();
}

function ensureSidebarActive() {
    const visibleGroup = Array.from(document.querySelectorAll('.sidebar-group')).find(el => el.style.display !== 'none');
    if (!visibleGroup) {
        return;
    }

    const hasActive = visibleGroup.querySelector('a.active');
    if (!hasActive) {
        const firstLink = visibleGroup.querySelector('a');
        if (firstLink) {
            firstLink.classList.add('active');
        }
    }
}

function bindSidebarActiveEvents() {
    document.querySelectorAll('.sidebar-nav a').forEach(link => {
        link.addEventListener('click', () => {
            document.querySelectorAll('.sidebar-nav a').forEach(x => x.classList.remove('active'));
            link.classList.add('active');
        });
    });
}

function switchLanguagePage(lang) {
    if (lang === 'en') {
        window.location.href = '/en';
        return;
    }

    window.location.href = '/';
}

// 弹窗控制
const loginModal = document.getElementById('login-modal');
const newTicketModal = document.getElementById('new-ticket-modal');
const ticketDetailModal = document.getElementById('ticket-detail-modal');
let currentTicketId = '';

document.getElementById('open-login-modal-btn').onclick = () => loginModal.classList.add('show');
function closeLoginModal() { loginModal.classList.remove('show'); }
function closeNewTicketModal() { newTicketModal.classList.remove('show'); }
function closeTicketDetailModal() { ticketDetailModal.classList.remove('show'); }

function setButtonLoading(button, loadingText, normalText, isLoading) {
    if (!button) {
        return;
    }

    button.disabled = isLoading;
    button.dataset.normalText = button.dataset.normalText || normalText;
    button.textContent = isLoading ? loadingText : (button.dataset.normalText || normalText);
}

// 发送验证码
async function sendVerifyCode() {
    const email = document.getElementById('user-email-input').value.trim();
    const sendCodeBtn = document.getElementById('send-code-btn');

    if (!email || !email.includes('@@')) {
        alert('请输入正确的邮箱地址！');
        return;
    }

    setButtonLoading(sendCodeBtn, '发送中...', '获取验证码', true);

    try {
        const resp = await fetch('/send-verification-code', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email })
        });

        const data = await resp.json();
        if (!resp.ok) {
            alert(data.message || '发送失败');
            return;
        }

        alert(data.message || '验证码已发送');
    } catch (error) {
        alert('网络异常，请稍后重试');
    } finally {
        setButtonLoading(sendCodeBtn, '发送中...', '获取验证码', false);
    }
}

// 验证登录
async function submitLogin() {
    const email = document.getElementById('user-email-input').value.trim();
    const code = document.getElementById('verify-code-input').value.trim();
    const loginBtn = document.getElementById('login-btn');

    if (!email || !code) {
        alert('请输入邮箱和验证码');
        return;
    }

    setButtonLoading(loginBtn, '登录中...', '验证并登录', true);

    try {
        const resp = await fetch('/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, code })
        });

        const data = await resp.json();
        if (!resp.ok) {
            alert(data.message || '登录失败');
            return;
        }

        closeLoginModal();
        updateUIAfterLogin(data.email);
        await checkLoginStatusForLicenseView();
        await loadTicketList();
        switchMainTab('license');
    } catch (error) {
        alert('网络异常，请稍后重试');
    } finally {
        setButtonLoading(loginBtn, '登录中...', '验证并登录', false);
    }
}

// 退出登录
async function logoutUser() {
    await fetch('/logout');
    location.reload();
}

function updateUIAfterLogin(email) {
    document.getElementById('user-auth-area').innerHTML =
        `<div style="display:flex; gap:8px; align-items:center;">
                    <button class="auth-trigger-btn logged" onclick="switchMainTab('license')">👤 ${email}</button>
                    <button class="logout-action-btn" onclick="logoutUser()">退出登录</button>
                </div>`;
}

async function checkLoginStatusForLicenseView() {
    const authResp = await fetch('/auth/status');
    if (!authResp.ok) {
        document.getElementById('license-unpurchased-view').style.display = 'block';
        document.getElementById('user-purchased-dashboard').style.display = 'none';
        return;
    }

    const auth = await authResp.json();
    if (!auth.isAuthenticated) {
        document.getElementById('license-unpurchased-view').style.display = 'block';
        document.getElementById('user-purchased-dashboard').style.display = 'none';
        return;
    }

    updateUIAfterLogin(auth.email);
    const listResp = await fetch('/license/list');
    const list = listResp.ok ? await listResp.json() : [];
    const tbody = document.getElementById('purchased-license-tbody');

    if (!list.length) {
        document.getElementById('license-unpurchased-view').style.display = 'block';
        document.getElementById('user-purchased-dashboard').style.display = 'none';
        return;
    }

    document.getElementById('license-unpurchased-view').style.display = 'none';
    document.getElementById('user-purchased-dashboard').style.display = 'block';
    document.getElementById('dashboard-user-email').textContent = auth.email;

    tbody.innerHTML = list.map(x => `
                <tr>
                    <td><strong>Order #${x.orderNumber}</strong></td>
                    <td>${x.customerEmail}</td>
                    <td>${new Date(x.createdAt).toLocaleString()}</td>
                    <td><button class="dl-lic-btn" onclick="downloadLicense('${x.orderId}')">下载 License</button></td>
                </tr>
            `).join('');
}

function downloadLicense(orderId) {
    if (!orderId) {
        alert('订单号无效');
        return;
    }

    window.location.href = `/api/Download/downloadLicense/${encodeURIComponent(orderId)}`;
}

async function loadTicketList() {
    const resp = await fetch('/ticket/list');
    if (!resp.ok) {
        document.getElementById('ticket-tbody').innerHTML = `<tr><td colspan="5">请先登录后查看问题列表</td></tr>`;
        return;
    }

    const list = await resp.json();
    const tbody = document.getElementById('ticket-tbody');

    if (!list.length) {
        tbody.innerHTML = `<tr><td colspan="5">暂无问题，点击“提交新问题”创建</td></tr>`;
        return;
    }

    tbody.innerHTML = list.map(x => `
                <tr>
                    <td>#${x.id}</td>
                    <td>${x.email}</td>
                    <td>${x.status}</td>
                    <td>${new Date(x.updatedAt).toLocaleString()}</td>
                    <td><button class="dl-lic-btn" onclick="viewTicketDetail('${x.id}')">查看详情</button></td>
                </tr>
            `).join('');
}

async function openNewTicketModal() {
    const authResp = await fetch('/auth/status');
    if (!authResp.ok) {
        alert('请先登录后再提交问题');
        return;
    }

    const auth = await authResp.json();
    if (!auth.isAuthenticated) {
        alert('请先登录后再提交问题');
        return;
    }

    document.getElementById('new-ticket-content').value = '';
    newTicketModal.classList.add('show');
}

async function submitNewTicket() {
    const content = document.getElementById('new-ticket-content').value.trim();
    const submitBtn = document.getElementById('new-ticket-submit-btn');

    if (!content) {
        alert('请输入问题描述');
        return;
    }

    setButtonLoading(submitBtn, '提交中...', '提交问题', true);

    try {
        const resp = await fetch('/ticket/create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content })
        });

        const data = await resp.json();
        if (!resp.ok) {
            alert(data.message || '创建失败');
            return;
        }

        closeNewTicketModal();
        await loadTicketList();
        await viewTicketDetail(data.ticketId);
    } catch (error) {
        alert('网络异常，请稍后重试');
    } finally {
        setButtonLoading(submitBtn, '提交中...', '提交问题', false);
    }
}

async function viewTicketDetail(id) {
    const resp = await fetch(`/ticket/${id}`);
    const data = await resp.json();
    if (!resp.ok) {
        alert(data.message || '获取问题详情失败');
        return;
    }

    currentTicketId = id;
    document.getElementById('ticket-detail-id').textContent = `#${id}`;
    document.getElementById('ticket-detail-email').textContent = data.email;
    document.getElementById('ticket-detail-status').textContent = data.status;
    document.getElementById('ticket-reply-content').value = '';

    const messageList = document.getElementById('ticket-message-list');
    if (!data.messages || !data.messages.length) {
        messageList.innerHTML = '<div class="ticket-message-empty">暂无沟通记录</div>';
    } else {
        messageList.innerHTML = data.messages.map(x => `
                    <div class="ticket-message-item ${x.senderType === 'User' ? 'user' : 'admin'}">
                        <div class="ticket-message-meta">${x.senderType} · ${new Date(x.createAt).toLocaleString()}</div>
                        <div class="ticket-message-content">${x.content}</div>
                    </div>
                `).join('');
    }

    ticketDetailModal.classList.add('show');
}

async function submitTicketReply() {
    const content = document.getElementById('ticket-reply-content').value.trim();
    const replyBtn = document.getElementById('ticket-reply-btn');

    if (!currentTicketId) {
        return;
    }

    if (!content) {
        alert('请输入回复内容');
        return;
    }

    setButtonLoading(replyBtn, '提交中...', '提交回复', true);

    try {
        const resp = await fetch(`/ticket/${currentTicketId}/reply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content })
        });

        const data = await resp.json();
        if (!resp.ok) {
            alert(data.message || '提交失败');
            return;
        }

        await viewTicketDetail(currentTicketId);
        await loadTicketList();
    } catch (error) {
        alert('网络异常，请稍后重试');
    } finally {
        setButtonLoading(replyBtn, '提交中...', '提交回复', false);
    }
}
// 初始化全局一次，mode 上线改成 live
DodoPaymentsCheckout.DodoPayments.Initialize({
    mode: "test",
    displayType: "overlay",
    onEvent: (evt) => {
        console.log("Dodo事件：", evt);
        if (evt.event_type === "checkout.opened") {
            console.log("支付弹窗打开");
        }
        if (evt.event_type === "checkout.closed") {
            console.log("弹窗关闭");
        }
        if (evt.event_type === "checkout.error") {
            alert("支付弹窗出错：" + evt.data?.message);
        }
    }
});

let payBtn = null;
DodoPaymentsCheckout.DodoPayments.Initialize({
    mode: "test", // 上线改成 live
    displayType: "overlay",
    onEvent: function (event) {
        console.log('Dodo event:', event);
        switch (event.event_type) {
            case "checkout.opened":
                console.log('弹窗打开了');
                break;
            case "checkout.closed":
                console.log('弹窗关闭，恢复按钮');
                if (payBtn) {
                    payBtn.classList.remove('is-disabled');
                    payBtn = null; // 清空
                }
                break;
            case "checkout.error":
                console.error('支付弹窗错误：', event.data);
                if (payBtn) {
                    payBtn.classList.remove('is-disabled');
                    payBtn = null;
                }
                break;
        }
    }
});

document.querySelector('.dodo-pay-btn').addEventListener('click', async function () {
    if (this.classList.contains('is-disabled')) {
        return;
    }

    this.classList.add('is-disabled');
    payBtn = this;

    try {
        const res = await fetch("api/webhook/dodo/create-session", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            }
        });
        const json = await res.json();
        if (!json.checkoutUrl) {
            alert("创建支付会话失败");
            this.classList.remove('is-disabled');
            payBtn = null;
            return;
        }
        // 打开弹窗
        DodoPaymentsCheckout.DodoPayments.Checkout.open({
            checkoutUrl: json.checkoutUrl
        });
    } catch (err) {
        console.error(err);
        alert("网络异常，无法拉起支付");
        this.classList.remove('is-disabled');
        payBtn = null;
    }
})

window.onload = async () => {
    bindSidebarActiveEvents();
    ensureSidebarActive();
    await checkLoginStatusForLicenseView();
    await loadTicketList();
};