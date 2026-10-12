@extends('layouts.app')

@section('title', "Báo Giá #{$quotation->id}")

@section('content')
<div class="space-y-6">
    <!-- Header thông tin báo giá -->
    <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
            <div class="flex items-center space-x-3">
                <h2 class="text-xl font-bold text-slate-900">Báo Giá #{{ str_pad($quotation->id, 4, '0', STR_PAD_LEFT) }}</h2>
                <span id="status-badge" class="px-2.5 py-1 text-xs font-semibold rounded-full
                    @if($quotation->status === 'done') bg-emerald-100 text-emerald-800
                    @elseif($quotation->status === 'failed') bg-rose-100 text-rose-800
                    @elseif($quotation->status === 'processing') bg-amber-100 text-amber-800 animate-pulse
                    @else bg-slate-100 text-slate-800
                    @endif">
                    {{ strtoupper($quotation->status) }}
                </span>
            </div>
            <p class="text-sm text-slate-500 mt-1">Khách hàng: <strong class="text-slate-800">{{ $quotation->customer_name }}</strong> (Mã: {{ $quotation->customer_code ?? 'N/A' }})</p>
        </div>

        <!-- Khu vực nút hành động (Tải xuống hoặc Trạng thái) -->
        <div id="action-area">
            @if($quotation->status === 'done')
                <a href="{{ route('quotations.download', $quotation->id) }}" id="download-btn"
                   class="inline-flex items-center px-4 py-2.5 bg-teal-600 hover:bg-teal-700 text-white text-sm font-medium rounded-lg shadow-sm transition-colors">
                    <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path></svg>
                    Tải File Báo Giá PowerPoint (.pptx)
                </a>
            @elseif($quotation->status === 'failed')
                <div class="text-xs text-rose-600 font-medium bg-rose-50 border border-rose-200 px-3 py-2 rounded-lg">
                    Xử lý thất bại
                </div>
            @else
                <div class="inline-flex items-center text-xs font-medium text-amber-700 bg-amber-50 border border-amber-200 px-3 py-2 rounded-lg">
                    <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-amber-600" fill="none" viewBox="0 0 24 24">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    Đang xử lý tại Executor .NET...
                </div>
            @endif
        </div>
    </div>

    <!-- Thông báo lỗi nếu thất bại -->
    <div id="error-box" class="{{ $quotation->status === 'failed' ? '' : 'hidden' }} bg-rose-50 border border-rose-200 rounded-xl p-4 text-sm text-rose-800">
        <div class="flex items-start">
            <svg class="w-5 h-5 text-rose-600 mr-2 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>
            <div>
                <strong class="font-semibold">Lỗi xử lý báo giá:</strong>
                <p id="error-message" class="mt-1 font-mono text-xs">{{ $quotation->error }}</p>
            </div>
        </div>
    </div>

    <!-- Chi tiết kỹ thuật & Tiến trình Polling -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <!-- Cột thông tin hệ thống -->
        <div class="bg-white rounded-xl shadow-sm border border-slate-200 p-5 space-y-4">
            <h3 class="text-xs font-semibold uppercase text-slate-500 tracking-wider">Thông tin liên kết</h3>
            <div class="space-y-3 text-sm">
                <div>
                    <span class="text-xs text-slate-400 block">Mã định danh Executor</span>
                    <span id="executor-id" class="font-mono text-xs text-slate-700 font-semibold">{{ $quotation->executor_quote_id ?? 'Đang chờ cấp ID...' }}</span>
                </div>
                <div>
                    <span class="text-xs text-slate-400 block">Thời gian tạo</span>
                    <span class="text-slate-700">{{ $quotation->created_at->format('d/m/Y H:i:s') }}</span>
                </div>
                <div>
                    <span class="text-xs text-slate-400 block">Mạng kết nối</span>
                    <span class="inline-flex items-center text-xs text-teal-700 bg-teal-50 px-2 py-0.5 rounded font-medium">Tailscale Mesh VPN</span>
                </div>
            </div>
        </div>

        <!-- Cột danh sách sản phẩm trong yêu cầu -->
        <div class="md:col-span-2 bg-white rounded-xl shadow-sm border border-slate-200 p-5">
            <h3 class="text-xs font-semibold uppercase text-slate-500 tracking-wider mb-3">Sản phẩm yêu cầu báo giá</h3>
            <div class="border border-slate-200 rounded-lg overflow-hidden">
                <table class="min-w-full divide-y divide-slate-200 text-sm">
                    <thead class="bg-slate-50 text-xs font-medium text-slate-500">
                        <tr>
                            <th class="px-4 py-2.5 text-left">#</th>
                            <th class="px-4 py-2.5 text-left">Tên hàng</th>
                            <th class="px-4 py-2.5 text-right">Đơn giá chưa VAT</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-200 bg-white">
                        @foreach($quotation->items as $i => $item)
                        <tr>
                            <td class="px-4 py-2 text-slate-400">{{ $i + 1 }}</td>
                            <td class="px-4 py-2 text-slate-800 font-medium">{{ $item['name'] }}</td>
                            <td class="px-4 py-2 text-right text-slate-700 font-mono">{{ number_format($item['price'], 0, ',', '.') }} đ</td>
                        </tr>
                        @endforeach
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>
@endsection

@push('scripts')
<script>
    // Cơ chế Polling tự động kiểm tra trạng thái mỗi 2.5 giây
    const quotationId = {{ $quotation->id }};
    let currentStatus = "{{ $quotation->status }}";

    function pollStatus() {
        if (currentStatus === 'done' || currentStatus === 'failed') {
            return; // Đã kết thúc, dừng polling
        }

        fetch(`/quotations/${quotationId}/check-status`)
            .then(res => res.json())
            .then(data => {
                currentStatus = data.status;

                // Cập nhật Badge
                const badge = document.getElementById('status-badge');
                badge.innerText = currentStatus.toUpperCase();

                if (currentStatus === 'done') {
                    badge.className = 'px-2.5 py-1 text-xs font-semibold rounded-full bg-emerald-100 text-emerald-800';

                    // Cập nhật nút Download
                    const actionArea = document.getElementById('action-area');
                    actionArea.innerHTML = `
                        <a href="${data.download_url}" id="download-btn"
                           class="inline-flex items-center px-4 py-2.5 bg-teal-600 hover:bg-teal-700 text-white text-sm font-medium rounded-lg shadow-sm transition-colors">
                            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path></svg>
                            Tải File Báo Giá PowerPoint (.pptx)
                        </a>
                    `;
                } else if (currentStatus === 'failed') {
                    badge.className = 'px-2.5 py-1 text-xs font-semibold rounded-full bg-rose-100 text-rose-800';

                    const actionArea = document.getElementById('action-area');
                    actionArea.innerHTML = `
                        <div class="text-xs text-rose-600 font-medium bg-rose-50 border border-rose-200 px-3 py-2 rounded-lg">
                            Xử lý thất bại
                        </div>
                    `;

                    const errorBox = document.getElementById('error-box');
                    const errorMsg = document.getElementById('error-message');
                    errorMsg.innerText = data.error || 'Executor báo lỗi xử lý.';
                    errorBox.classList.remove('hidden');
                } else {
                    // Tiếp tục polling sau 2.5 giây
                    setTimeout(pollStatus, 2500);
                }
            })
            .catch(err => {
                console.error("Lỗi khi kiểm tra trạng thái:", err);
                setTimeout(pollStatus, 3000);
            });
    }

    // Bắt đầu polling nếu chưa xong
    if (currentStatus !== 'done' && currentStatus !== 'failed') {
        setTimeout(pollStatus, 2000);
    }
</script>
@endpush
