const CUSTOMER = {
  id: 'KH-001',
  name: 'Công ty TNHH Hóa Chất Minh Phát',
  contactName: 'Nguyễn Minh Anh',
  email: 'anh.nguyen@example.local',
  phone: '0901 234 567',
  address: 'TP. Hồ Chí Minh'
};

const state = {
  quotes: [],
  modalOpen: false,
  editModalOpen: false,
  editingQuote: null,
  isLoading: false,
  isSubmitting: false
};

let pollingInterval = null;

const formatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND'
});

function formatMoney(value) {
  return `${formatter.format(value || 0)}₫`;
}

function getCustomerRoute() {
  const hash = window.location.hash.replace(/^#\/?/, '');
  if (!hash) return `/customers/${CUSTOMER.id}`;
  return `/${hash}`;
}

function isCreateQuoteRoute() {
  return getCustomerRoute() === `/customers/${CUSTOMER.id}/quotes/new`;
}

function openCreateQuoteModal() {
  window.location.hash = `/customers/${CUSTOMER.id}/quotes/new`;
  state.modalOpen = true;
  renderApp();
}

function closeCreateQuoteModal() {
  window.location.hash = `/customers/${CUSTOMER.id}`;
  state.modalOpen = false;
  renderApp();
}

// Xử lý mở Modal sửa với null-check an toàn và try-catch tránh crash UI
function handleEdit(quote) {
  try {
    if (!quote || !quote.id) {
      console.warn('Dữ liệu báo giá chưa hợp lệ hoặc đang tải:', quote);
      return;
    }

    state.editingQuote = {
      id: quote.id,
      quoteNumber: quote.quoteNumber ?? '',
      customerName: quote.customerName ?? '',
      productName: quote?.productName ?? '',
      total: quote.total ?? 0,
      status: quote.status ?? ''
    };
    state.editModalOpen = true;
    renderApp();
  } catch (error) {
    console.error('Lỗi khi mở modal sửa báo giá:', error);
  }
}

function closeEditModal() {
  state.editModalOpen = false;
  state.editingQuote = null;
  renderApp();
}

function generateUuid() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

function handleSaveEdit(event) {
  if (event) {
    event.preventDefault();
  }
  try {
    if (!state.editingQuote?.id) return;
    const form = document.getElementById('edit-quote-form');
    if (!form) return;
    const formData = new FormData(form);
    const updatedProductName = String(formData.get('productName') || '').trim();

    // Cập nhật tên sản phẩm trong state
    const index = state.quotes.findIndex((q) => q?.id === state.editingQuote.id);
    if (index !== -1) {
      state.quotes[index] = {
        ...state.quotes[index],
        productName: updatedProductName
      };
    }
    closeEditModal();
  } catch (error) {
    console.error('Lỗi khi cập nhật thông tin báo giá:', error);
  }
}

function CustomerInfoCard() {
  return `
    <aside class="panel profile-panel">
      <div class="eyebrow">Customer profile</div>
      <h2>${CUSTOMER.name}</h2>
      <div class="meta-list">
        <div><span>Mã KH</span><strong>${CUSTOMER.id}</strong></div>
        <div><span>Người liên hệ</span><strong>${CUSTOMER.contactName}</strong></div>
        <div><span>Email</span><strong>${CUSTOMER.email}</strong></div>
        <div><span>SĐT</span><strong>${CUSTOMER.phone}</strong></div>
        <div><span>Địa chỉ</span><strong>${CUSTOMER.address}</strong></div>
      </div>
    </aside>
  `;
}

function QuoteForm() {
  return `
    <form id="quote-form" class="quote-form" onsubmit="return false;">
      <div class="field-grid two-col">
        <label>
          <span>Sản phẩm</span>
          <input name="product" type="text" value="Dung môi công nghiệp A" required maxlength="120">
        </label>
        <label>
          <span>Số lượng</span>
          <input name="quantity" type="number" min="1" value="10" required>
        </label>
        <label>
          <span>Đơn giá</span>
          <input name="unitPrice" type="number" min="0.01" step="0.01" value="1250000" required>
        </label>
        <label>
          <span>Điều khoản thanh toán</span>
          <select name="paymentTerms">
            <option selected>Thanh toán 50% khi đặt hàng, 50% khi giao</option>
            <option>Thanh toán trong 30 ngày</option>
            <option>Thanh toán trước khi giao</option>
          </select>
        </label>
      </div>

      <label>
        <span>Điều kiện giao hàng</span>
        <input name="deliveryTerms" type="text" value="Giao hàng tại kho của khách hàng trong 3-5 ngày làm việc" maxlength="500" required>
      </label>

      <div id="form-message" class="form-message" aria-live="polite"></div>

      <div class="actions-row">
        <button type="button" class="secondary" data-action="close-modal">Hủy</button>
        <button type="button" class="primary" data-action="submit-quote" ${state.isSubmitting ? 'disabled' : ''}>
          ${state.isSubmitting ? 'Đang gửi...' : 'Gửi yêu cầu báo giá'}
        </button>
      </div>
    </form>
  `;
}

function EditQuoteModal() {
  if (!state.editModalOpen || !state.editingQuote) {
    return '';
  }

  const quote = state.editingQuote;

  return `
    <div class="modal-backdrop" data-action="close-edit-modal-backdrop">
      <div class="modal-sheet" role="dialog" aria-modal="true" aria-labelledby="edit-quote-title">
        <div class="sheet-header">
          <div>
            <div class="eyebrow">Chỉnh sửa báo giá</div>
            <h3 id="edit-quote-title">Sửa thông tin báo giá</h3>
          </div>
          <button type="button" class="icon-button" data-action="close-edit-modal" aria-label="Đóng">×</button>
        </div>
        <form id="edit-quote-form" class="quote-form" onsubmit="return false;">
          <div class="field-grid two-col">
            <label>
              <span>Số báo giá</span>
              <input type="text" value="${quote?.quoteNumber || ''}" disabled>
            </label>
            <label>
              <span>Khách hàng</span>
              <input type="text" value="${quote?.customerName || ''}" disabled>
            </label>
            <label>
              <span>Sản phẩm</span>
              <input name="productName" type="text" value="${quote?.productName || ''}" required maxlength="120">
            </label>
            <label>
              <span>Tổng tiền</span>
              <input type="text" value="${formatMoney(quote?.total || 0)}" disabled>
            </label>
          </div>

          <div class="actions-row">
            <button type="button" class="secondary" data-action="close-edit-modal">Hủy</button>
            <button type="button" class="primary" data-action="save-edit-quote">Lưu thay đổi</button>
          </div>
        </form>
      </div>
    </div>
  `;
}

function QuoteListTable(rows) {
  if (!rows || !rows.length) {
    return `
      <div class="empty-state">
        <p>Chưa có báo giá nào cho khách hàng này.</p>
      </div>
    `;
  }

  return `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Số báo giá</th>
            <th>Sản phẩm</th>
            <th>Tổng tiền</th>
            <th>Trạng thái</th>
            <th>Ngày tạo</th>
            <th>File</th>
            <th>Thao tác</th>
          </tr>
        </thead>
        <tbody>
          ${rows.map((quote) => {
    // VẤN ĐỀ 1 & 2: Dùng quote?.productName, fallback về chuỗi rỗng an toàn
    const product = quote?.productName || '';
    const status = quote?.status || 'PENDING';
    const statusClass = status.toUpperCase();
    const download = quote?.downloadUrl
      ? `<a href="${quote.downloadUrl}" target="_blank" rel="noreferrer">Tải file</a>`
      : (quote?.errorMessage || (statusClass === 'PROCESSING' ? 'Đang xử lý...' : 'Chưa sẵn sàng'));

    return `
              <tr>
                <td>${quote?.quoteNumber || '—'}</td>
                <td>${product}</td>
                <td>${formatMoney(quote?.total)}</td>
                <td><span class="badge ${statusClass}">${status}</span></td>
                <td>${quote?.createdAt ? new Date(quote.createdAt).toLocaleDateString('vi-VN') : '—'}</td>
                <td>${download}</td>
                <td>
                  <button type="button" class="secondary" style="padding: 5px 12px; font-size: 13px;" data-action="edit-quote" data-id="${quote?.id || ''}">
                    Sửa
                  </button>
                </td>
              </tr>
            `;
  }).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function CustomerDetailPage() {
  const customerQuotes = state.quotes.filter((q) => q?.customerName === CUSTOMER.name);

  return `
    <header class="topbar">
      <div>
        <div class="eyebrow light">An Bình Chemtech</div>
        <h1>Chi tiết khách hàng</h1>
      </div>
      <button type="button" class="primary" data-action="open-create-quote">Tạo báo giá mới</button>
    </header>

    <main class="layout">
      ${CustomerInfoCard()}

      <section class="panel content-panel">
        <div class="section-head">
          <div>
            <div class="eyebrow">Operational list</div>
            <h2>Danh sách báo giá</h2>
          </div>
          <button type="button" class="secondary" data-action="refresh-quotes">Làm mới</button>
        </div>

        <div id="quote-list-container">
          ${state.isLoading ? '<div class="loading-state">Đang tải dữ liệu...</div>' : QuoteListTable(customerQuotes)}
        </div>
      </section>
    </main>
  `;
}

function QuoteModal() {
  if (!state.modalOpen && !isCreateQuoteRoute()) {
    return '';
  }

  return `
    <div class="modal-backdrop" data-action="close-modal-backdrop">
      <div class="modal-sheet" role="dialog" aria-modal="true" aria-labelledby="create-quote-title">
        <div class="sheet-header">
          <div>
            <div class="eyebrow">Create quote</div>
            <h3 id="create-quote-title">Tạo báo giá mới</h3>
          </div>
          <button type="button" class="icon-button" data-action="close-modal" aria-label="Đóng">×</button>
        </div>
        ${QuoteForm()}
      </div>
    </div>
  `;
}

// VẤN ĐỀ 3: Cơ chế Polling tự động kiểm tra và cập nhật trạng thái PENDING / PROCESSING
function checkAndManagePolling() {
  const hasProcessingOrPending = state.quotes.some((q) => {
    const s = q?.status?.toUpperCase();
    return s === 'PENDING' || s === 'PROCESSING';
  });

  if (hasProcessingOrPending) {
    if (!pollingInterval) {
      console.log('[Polling] Bắt đầu polling mỗi 3 giây do có báo giá PENDING/PROCESSING...');
      pollingInterval = setInterval(async () => {
        await loadQuotes({ isPolling: true });
      }, 3000);
    }
  } else {
    if (pollingInterval) {
      console.log('[Polling] Dừng polling vì tất cả báo giá đã COMPLETED hoặc FAILED.');
      clearInterval(pollingInterval);
      pollingInterval = null;
    }
  }
}

function updateQuoteListDom() {
  const container = document.getElementById('quote-list-container');
  if (!container) return;
  const customerQuotes = state.quotes.filter((q) => q?.customerName === CUSTOMER.name);
  container.innerHTML = QuoteListTable(customerQuotes);
  container.querySelectorAll('[data-action="edit-quote"]').forEach((button) => {
    button.addEventListener('click', () => {
      const id = button.getAttribute('data-id');
      const quote = state.quotes.find((q) => q?.id === id);
      handleEdit(quote);
    });
  });
}

async function loadQuotes(options = {}) {
  const isPolling = options?.isPolling === true;

  // Khi đang polling ngầm thì không bật isLoading toàn trang để tránh nhấp nháy UI
  if (!isPolling) {
    state.isLoading = true;
    renderApp();
  }

  try {
    const response = await fetch('/api/quotes');
    if (!response.ok) throw new Error('Không thể lấy danh sách báo giá');
    const data = await response.json();
    state.quotes = Array.isArray(data) ? data : [];
  } catch (error) {
    console.error('Lỗi khi tải danh sách báo giá:', error);
    if (!isPolling) {
      state.quotes = [];
    }
  } finally {
    if (!isPolling) {
      state.isLoading = false;
      renderApp();
    } else {
      // Khi polling ngầm: chỉ cập nhật lại danh sách báo giá trong bảng,
      // tuyệt đối không renderApp() đè lên toàn trang để tránh phá hủy Modal form đang mở
      updateQuoteListDom();
    }
    checkAndManagePolling();
  }
}

async function handleCreateQuote(event) {
  if (event) {
    event.preventDefault();
  }

  if (state.isSubmitting) return;

  const form = document.getElementById('quote-form');
  if (!form) return;

  const formData = new FormData(form);
  const product = String(formData.get('product') || '').trim();
  const quantity = Number(formData.get('quantity'));
  const unitPrice = Number(formData.get('unitPrice'));
  const paymentTerms = String(formData.get('paymentTerms') || '').trim();
  const deliveryTerms = String(formData.get('deliveryTerms') || '').trim();
  const messageBox = document.getElementById('form-message');
  const submitButton = form.querySelector('[data-action="submit-quote"]');

  if (!product || !quantity || quantity <= 0 || !unitPrice || unitPrice <= 0 || !paymentTerms || !deliveryTerms) {
    if (messageBox) {
      messageBox.textContent = 'Vui lòng nhập đầy đủ thông tin hợp lệ.';
      messageBox.classList.add('error');
    }
    return;
  }

  if (messageBox) {
    messageBox.textContent = '';
    messageBox.classList.remove('error');
  }

  const payload = {
    customerId: CUSTOMER.id,
    customerName: CUSTOMER.name,
    idempotencyKey: generateUuid(),
    paymentTerms,
    deliveryTerms,
    items: [{ product, quantity, unitPrice }]
  };

  state.isSubmitting = true;
  // Cập nhật trạng thái nút gửi trực tiếp trên DOM hiện tại mà không gọi renderApp()
  if (submitButton) {
    submitButton.disabled = true;
    submitButton.textContent = 'Đang gửi...';
  }

  try {
    const response = await fetch('/api/quotes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      let errText = 'Gửi báo giá thất bại';
      try {
        const errJson = await response.json();
        if (errJson?.title) errText = errJson.title;
      } catch (_) {}
      throw new Error(errText);
    }

    closeCreateQuoteModal();
    await loadQuotes();
  } catch (error) {
    console.error('Lỗi khi tạo báo giá:', error);
    if (messageBox) {
      messageBox.textContent = error.message || 'Không thể gửi yêu cầu báo giá. Vui lòng thử lại.';
      messageBox.classList.add('error');
    }
    if (submitButton) {
      submitButton.disabled = false;
      submitButton.textContent = 'Gửi yêu cầu báo giá';
    }
  } finally {
    state.isSubmitting = false;
  }
}

function renderApp() {
  const root = document.getElementById('app');
  if (!root) return;

  root.innerHTML = `
    ${CustomerDetailPage()}
    ${QuoteModal()}
    ${EditQuoteModal()}
  `;

  const form = document.getElementById('quote-form');
  if (form) {
    form.addEventListener('submit', (e) => {
      e.preventDefault();
      handleCreateQuote(e);
    });
  }

  const editForm = document.getElementById('edit-quote-form');
  if (editForm) {
    editForm.addEventListener('submit', (e) => {
      e.preventDefault();
      handleSaveEdit(e);
    });
  }

  document.querySelectorAll('.modal-sheet').forEach((sheet) => {
    sheet.addEventListener('click', (e) => {
      e.stopPropagation();
    });
  });

  document.querySelectorAll('[data-action]').forEach((element) => {
    const action = element.getAttribute('data-action');
    if (!action) return;

    element.addEventListener('click', (e) => {
      if (action === 'open-create-quote') openCreateQuoteModal();
      if (action === 'close-modal') closeCreateQuoteModal();
      if (action === 'close-modal-backdrop') {
        if (e.target === e.currentTarget) {
          closeCreateQuoteModal();
        }
      }
      if (action === 'close-edit-modal') closeEditModal();
      if (action === 'close-edit-modal-backdrop') {
        if (e.target === e.currentTarget) {
          closeEditModal();
        }
      }
      if (action === 'refresh-quotes') loadQuotes();
      if (action === 'submit-quote') handleCreateQuote(e);
      if (action === 'save-edit-quote') handleSaveEdit(e);
      if (action === 'edit-quote') {
        const id = element.getAttribute('data-id');
        const quote = state.quotes.find((q) => q?.id === id);
        handleEdit(quote);
      }
    });
  });
}

function syncRouteFromHash() {
  const route = getCustomerRoute();
  const target = `/customers/${CUSTOMER.id}`;
  const createRoute = `/customers/${CUSTOMER.id}/quotes/new`;

  state.modalOpen = route === createRoute;

  if (route !== target && route !== createRoute) {
    window.location.hash = target;
  }

  renderApp();
}

window.addEventListener('hashchange', syncRouteFromHash);

window.addEventListener('DOMContentLoaded', async () => {
  const route = getCustomerRoute();
  if (!route.startsWith(`/customers/${CUSTOMER.id}`)) {
    window.location.hash = `/customers/${CUSTOMER.id}`;
    return;
  }

  state.modalOpen = route === `/customers/${CUSTOMER.id}/quotes/new`;
  await loadQuotes();
});
