@extends('layouts.app')

@section('title', 'Tạo Báo Giá Mới')

@section('content')
<div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
    <div class="px-6 py-5 border-b border-slate-200 bg-slate-50/50">
        <h2 class="text-lg font-semibold text-slate-900">Tạo Yêu Cầu Báo Giá</h2>
        <p class="text-sm text-slate-500 mt-1">
            Thông tin sẽ được lưu vào cơ sở dữ liệu PostgreSQL và chuyển đến thiết bị thực thi .NET 8 qua mạng Tailscale.
        </p>
    </div>

    <form id="quote-form" action="{{ route('quotations.store') }}" method="POST" class="p-6 space-y-6" onsubmit="return validateQuoteForm(event)">
        @csrf

        <!-- Thông tin khách hàng -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div>
                <label for="customer_code" class="block text-sm font-medium text-slate-700 mb-1">Mã khách hàng</label>
                <input type="text" name="customer_code" id="customer_code" value="{{ old('customer_code', 'KH-001') }}"
                    class="w-full px-3 py-2 border border-slate-300 rounded-lg shadow-sm focus:ring-teal-500 focus:border-teal-500 text-sm"
                    placeholder="VD: KH-001">
            </div>

            <div class="md:col-span-2">
                <label for="customer_name" class="block text-sm font-medium text-slate-700 mb-1">
                    Tên khách hàng / Quý đối tác <span class="text-rose-500">*</span>
                </label>
                <input type="text" name="customer_name" id="customer_name" value="{{ old('customer_name', 'Công ty Cổ phần Thực phẩm Á Châu (Asia Foods)') }}" required
                    class="w-full px-3 py-2 border border-slate-300 rounded-lg shadow-sm focus:ring-teal-500 focus:border-teal-500 text-sm"
                    placeholder="Nhập tên đầy đủ của Quý công ty">
                @error('customer_name')
                    <p class="text-xs text-rose-600 mt-1">{{ $message }}</p>
                @enderror
            </div>
        </div>

        <!-- Bảng danh sách sản phẩm động (Giữ nguyên 100% dữ liệu đã nhập kể cả khi lỗi) -->
        @php
            // Lấy lại toàn bộ các dòng người dùng đã nhập (kể cả dòng 4, 5...) nếu validation fail
            $itemsToRender = old('items', $defaultItems);
        @endphp

        <div class="pt-4 border-t border-slate-200">
            <div class="flex items-center justify-between mb-3">
                <div>
                    <h3 class="text-sm font-semibold text-slate-900 uppercase tracking-wider">Danh mục sản phẩm & đơn giá</h3>
                    <p class="text-xs text-slate-500">Mặc định 3 sản phẩm mẫu, bạn có thể tự do thêm hoặc xóa bớt dòng tùy ý.</p>
                </div>
                <button type="button" id="btn-add-item" onclick="addItemRow()"
                    class="inline-flex items-center px-3 py-1.5 bg-teal-50 hover:bg-teal-100 text-teal-700 border border-teal-200 text-xs font-semibold rounded-lg shadow-sm transition-colors">
                    <svg class="w-4 h-4 mr-1 text-teal-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path></svg>
                    + Thêm dòng sản phẩm
                </button>
            </div>

            <div class="border border-slate-200 rounded-lg overflow-hidden">
                <table class="min-w-full divide-y divide-slate-200" id="items-table">
                    <thead class="bg-slate-50 text-xs font-semibold text-slate-600 uppercase">
                        <tr>
                            <th scope="col" class="px-4 py-3 text-center w-12">STT</th>
                            <th scope="col" class="px-4 py-3 text-left">Tên sản phẩm <span class="text-rose-500">*</span></th>
                            <th scope="col" class="px-4 py-3 text-left w-32">Quy cách</th>
                            <th scope="col" class="px-4 py-3 text-right w-48">Đơn giá chưa VAT (VNĐ) <span class="text-rose-500">*</span></th>
                            <th scope="col" class="px-4 py-3 text-center w-16">Xóa</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-200 bg-white text-sm" id="items-tbody">
                        @foreach($itemsToRender as $index => $item)
                        <tr class="item-row">
                            <td class="px-4 py-3 text-slate-500 text-center font-medium row-index">{{ $index + 1 }}</td>
                            <td class="px-4 py-3">
                                <input type="text" name="items[{{ $index }}][name]" value="{{ $item['name'] ?? '' }}" required
                                    class="w-full px-2.5 py-1.5 border {{ $errors->has("items.{$index}.name") ? 'border-rose-400 bg-rose-50/30' : 'border-slate-300' }} rounded-md focus:ring-teal-500 focus:border-teal-500 text-sm item-name-input"
                                    placeholder="Tên sản phẩm">
                                @error("items.{$index}.name")
                                    <p class="text-xs text-rose-600 mt-1">{{ $message }}</p>
                                @enderror
                            </td>
                            <td class="px-4 py-3 text-slate-500 text-sm">
                                25 kg/bao
                            </td>
                            <td class="px-4 py-3 text-right">
                                <input type="number" step="any" min="0" name="items[{{ $index }}][price]" value="{{ $item['price'] ?? '' }}" required
                                    class="w-full px-2.5 py-1.5 border {{ $errors->has("items.{$index}.price") ? 'border-rose-400 bg-rose-50/30' : 'border-slate-300' }} rounded-md text-right focus:ring-teal-500 focus:border-teal-500 text-sm item-price-input"
                                    placeholder="0">
                                @error("items.{$index}.price")
                                    <p class="text-xs text-rose-600 mt-1 text-right">{{ $message }}</p>
                                @enderror
                            </td>
                            <td class="px-4 py-3 text-center">
                                <button type="button" onclick="removeItemRow(this)" title="Xóa dòng này"
                                    class="text-slate-400 hover:text-rose-600 transition-colors p-1 rounded hover:bg-rose-50">
                                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path></svg>
                                </button>
                            </td>
                        </tr>
                        @endforeach
                    </tbody>
                </table>
            </div>
            <div class="flex items-center justify-between mt-2">
                <p class="text-xs text-slate-500 italic">
                    * Thuế VAT (8%) và đơn giá sau VAT sẽ được Executor tính toán tự động bằng code xác định, định dạng dấu phẩy theo chuẩn kế toán.
                </p>
                <button type="button" onclick="addItemRow()" class="text-xs text-teal-600 hover:text-teal-700 font-medium inline-flex items-center">
                    + Thêm dòng sản phẩm khác
                </button>
            </div>
        </div>

        <!-- Nút Submit -->
        <div class="pt-4 border-t border-slate-200 flex justify-end space-x-3">
            <button type="submit"
                class="inline-flex items-center px-5 py-2.5 bg-teal-600 hover:bg-teal-700 text-white font-medium text-sm rounded-lg shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-teal-500">
                <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3"></path></svg>
                Tạo Báo Giá và Gửi Executor
            </button>
        </div>
    </form>
</div>

<!-- POPUP MODAL THÔNG BÁO LỖI TRỰC QUAN -->
<div id="error-modal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm hidden transition-opacity">
    <div class="bg-white rounded-xl shadow-xl border border-slate-200 max-w-md w-full mx-4 overflow-hidden transform transition-all scale-95 duration-200" id="modal-box">
        <div class="p-6">
            <div class="w-12 h-12 rounded-full bg-rose-100 text-rose-600 flex items-center justify-center mb-4">
                <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>
            </div>
            <h3 class="text-base font-bold text-slate-900 mb-1" id="modal-title">Lỗi dữ liệu báo giá</h3>
            <p class="text-sm text-slate-600 leading-relaxed" id="modal-message">
                Nội dung thông báo lỗi
            </p>
        </div>
        <div class="bg-slate-50 px-6 py-3 border-t border-slate-100 flex justify-end">
            <button type="button" onclick="closeErrorModal()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-900 text-white text-xs font-semibold rounded-lg shadow-sm transition-colors">
                Đã hiểu, để tôi sửa lại
            </button>
        </div>
    </div>
</div>
@endsection

@push('scripts')
<script>
    // Kiểm tra tính hợp lệ trước khi submit form - Bật POPUP ngay lập tức nếu lỗi
    function validateQuoteForm(event) {
        const rows = document.querySelectorAll('#items-tbody .item-row');

        for (let i = 0; i < rows.length; i++) {
            const row = rows[i];
            const nameInput = row.querySelector('.item-name-input');
            const priceInput = row.querySelector('.item-price-input');
            const stt = i + 1;

            const nameVal = nameInput ? nameInput.value.trim() : '';
            const priceVal = priceInput ? parseFloat(priceInput.value) : NaN;

            if (!nameVal) {
                event.preventDefault();
                showErrorModal(`Sản phẩm dòng số ${stt} chưa có tên! Vui lòng nhập tên sản phẩm.`, nameInput);
                return false;
            }

            if (isNaN(priceVal)) {
                event.preventDefault();
                showErrorModal(`Đơn giá sản phẩm dòng số ${stt} (${nameVal}) không được để trống!`, priceInput);
                return false;
            }

            if (priceVal < 0) {
                event.preventDefault();
                showErrorModal(`Đơn giá sản phẩm dòng số ${stt} (${nameVal}) đang là ${priceInput.value}đ. Đơn giá KHÔNG ĐƯỢC LÀ SỐ ÂM (phải >= 0)!`, priceInput);
                return false;
            }
        }

        return true;
    }

    // Hiển thị Popup Modal
    let activeInputToFocus = null;
    function showErrorModal(message, inputElement) {
        activeInputToFocus = inputElement;
        const modal = document.getElementById('error-modal');
        const modalMsg = document.getElementById('modal-message');
        const modalBox = document.getElementById('modal-box');

        modalMsg.innerText = message;
        modal.classList.remove('hidden');

        // Animation nhẹ nhàng
        setTimeout(() => {
            modalBox.classList.remove('scale-95');
            modalBox.classList.add('scale-100');
        }, 10);

        // Làm nổi bật ô input bị lỗi
        if (inputElement) {
            inputElement.classList.add('ring-2', 'ring-rose-500', 'border-rose-500');
        }
    }

    // Đóng Popup Modal và focus vào ô input bị sai
    function closeErrorModal() {
        const modal = document.getElementById('error-modal');
        const modalBox = document.getElementById('modal-box');

        modalBox.classList.remove('scale-100');
        modalBox.classList.add('scale-95');

        setTimeout(() => {
            modal.classList.add('hidden');
            if (activeInputToFocus) {
                activeInputToFocus.focus();
                activeInputToFocus.select();
            }
        }, 150);
    }

    // Hàm thêm dòng sản phẩm mới
    function addItemRow() {
        const tbody = document.getElementById('items-tbody');
        const currentIndex = tbody.querySelectorAll('.item-row').length;
        const newRowNumber = currentIndex + 1;

        const tr = document.createElement('tr');
        tr.className = 'item-row bg-teal-50/30 transition-colors';
        tr.innerHTML = `
            <td class="px-4 py-3 text-slate-500 text-center font-medium row-index">${newRowNumber}</td>
            <td class="px-4 py-3">
                <input type="text" name="items[${currentIndex}][name]" required
                    class="w-full px-2.5 py-1.5 border border-slate-300 rounded-md focus:ring-teal-500 focus:border-teal-500 text-sm item-name-input"
                    placeholder="Tên sản phẩm mới">
            </td>
            <td class="px-4 py-3 text-slate-500 text-sm">
                25 kg/bao
            </td>
            <td class="px-4 py-3 text-right">
                <input type="number" step="any" min="0" name="items[${currentIndex}][price]" required
                    class="w-full px-2.5 py-1.5 border border-slate-300 rounded-md text-right focus:ring-teal-500 focus:border-teal-500 text-sm item-price-input"
                    placeholder="0">
            </td>
            <td class="px-4 py-3 text-center">
                <button type="button" onclick="removeItemRow(this)" title="Xóa dòng này"
                    class="text-slate-400 hover:text-rose-600 transition-colors p-1 rounded hover:bg-rose-50">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path></svg>
                </button>
            </td>
        `;

        tbody.appendChild(tr);

        // Tự động focus vào ô nhập tên sản phẩm mới
        const newInput = tr.querySelector('.item-name-input');
        if (newInput) newInput.focus();
    }

    // Hàm xóa dòng sản phẩm
    function removeItemRow(button) {
        const tbody = document.getElementById('items-tbody');
        const rows = tbody.querySelectorAll('.item-row');

        if (rows.length <= 1) {
            showErrorModal('Báo giá phải có ít nhất 1 sản phẩm!');
            return;
        }

        const tr = button.closest('.item-row');
        tr.remove();

        // Cập nhật lại số thứ tự (STT) và index name cho tất cả các dòng
        reindexRows();
    }

    // Đánh lại số thứ tự và thuộc tính name của input
    function reindexRows() {
        const rows = document.querySelectorAll('#items-tbody .item-row');
        rows.forEach((row, idx) => {
            const idxCell = row.querySelector('.row-index');
            if (idxCell) idxCell.innerText = idx + 1;

            const nameInput = row.querySelector('input[name*="[name]"]');
            if (nameInput) nameInput.name = `items[${idx}][name]`;

            const priceInput = row.querySelector('input[name*="[price]"]');
            if (priceInput) priceInput.name = `items[${idx}][price]`;
        });
    }
</script>
@endpush
