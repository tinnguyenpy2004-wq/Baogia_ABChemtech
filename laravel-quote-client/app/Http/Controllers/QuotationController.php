<?php

namespace App\Http\Controllers;

use App\Jobs\SendQuoteToExecutor;
use App\Models\Quotation;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Storage;
use Illuminate\Support\Facades\Log;

class QuotationController extends Controller
{
    /**
     * Trang tạo báo giá mới (Blade).
     */
    public function create()
    {
        // 3 sản phẩm mẫu mặc định của An Bình Chemtech
        $defaultItems = [
            ['name' => 'Tetrasodium Pyrophosphate (TSPP)', 'price' => 51560],
            ['name' => 'Sodium Tripolyphosphate (STPP)', 'price' => 51560],
            ['name' => 'Sodium Hexametaphosphate (SHMP)', 'price' => 51560],
        ];

        return view('quotations.create', compact('defaultItems'));
    }

    /**
     * Tiếp nhận form submit từ Laravel.
     */
    public function store(Request $request)
    {
        $validated = $request->validate([
            'customer_code' => 'nullable|string|max:50',
            'customer_name' => 'required|string|max:255',
            'items' => 'required|array|min:1',
            'items.*.name' => 'required|string|max:255',
            'items.*.price' => 'required|numeric|min:0',
        ]);

        // 1. Lưu vào cơ sở dữ liệu với trạng thái 'pending'
        $quotation = Quotation::create([
            'customer_code' => $validated['customer_code'] ?? null,
            'customer_name' => $validated['customer_name'],
            'items' => $validated['items'],
            'status' => 'pending',
        ]);

        // 2. Dispatch Job gửi yêu cầu sang Executor
        SendQuoteToExecutor::dispatch($quotation);

        return redirect()->route('quotations.show', $quotation->id)
            ->with('success', 'Đã tiếp nhận yêu cầu báo giá và đưa vào hàng đợi xử lý.');
    }

    /**
     * Trang chi tiết báo giá (kèm auto polling).
     */
    public function show(Quotation $quotation)
    {
        return view('quotations.show', compact('quotation'));
    }

    /**
     * Endpoint API cho trang chi tiết Polling mỗi 2-3 giây.
     */
    public function checkStatus(Quotation $quotation)
    {
        // Nếu đã hoàn thành hoặc thất bại thì trả về ngay
        if (in_array($quotation->status, ['done', 'failed'])) {
            return response()->json([
                'status' => $quotation->status,
                'error' => $quotation->error,
                'download_url' => $quotation->status === 'done' ? route('quotations.download', $quotation->id) : null,
            ]);
        }

        // Nếu đang processing và đã có executor_quote_id -> tra cứu trạng thái Executor
        if ($quotation->executor_quote_id) {
            $executorUrl = rtrim(env('EXECUTOR_URL', 'http://127.0.0.1:5000'), '/');
            $executorToken = env('EXECUTOR_TOKEN', 'anbinh-chemtech-secret-token');

            try {
                $response = Http::withHeaders([
                    'X-API-Key' => $executorToken,
                    'Accept' => 'application/json',
                ])
                ->timeout(5)
                ->get("{$executorUrl}/quotes/{$quotation->executor_quote_id}");

                if ($response->successful()) {
                    $executorStatus = $response->json('status');

                    if ($executorStatus === 'done') {
                        // Tải file PPTX về lưu vào storage nội bộ
                        $fileResponse = Http::withHeaders([
                            'X-API-Key' => $executorToken,
                        ])
                        ->timeout(30)
                        ->get("{$executorUrl}/quotes/{$quotation->executor_quote_id}/file");

                        if ($fileResponse->successful()) {
                            $fileName = "quotes/{$quotation->executor_quote_id}.pptx";
                            Storage::disk('public')->put($fileName, $fileResponse->body());

                            $quotation->update([
                                'status' => 'done',
                                'file_path' => $fileName,
                                'error' => null,
                            ]);
                        } else {
                            $quotation->update([
                                'status' => 'failed',
                                'error' => 'Không thể tải file PowerPoint từ Executor: ' . $fileResponse->status(),
                            ]);
                        }
                    } elseif ($executorStatus === 'failed') {
                        $quotation->update([
                            'status' => 'failed',
                            'error' => $response->json('error') ?? 'Executor báo lỗi xử lý.',
                        ]);
                    }
                }
            } catch (\Throwable $e) {
                Log::warning("[Polling Status] Không thể kết nối Executor: " . $e->getMessage());
            }
        }

        // Tải lại model mới nhất
        $quotation->refresh();

        return response()->json([
            'status' => $quotation->status,
            'error' => $quotation->error,
            'download_url' => $quotation->status === 'done' ? route('quotations.download', $quotation->id) : null,
        ]);
    }

    /**
     * Tải file báo giá PPTX đã lưu.
     */
    public function download(Quotation $quotation)
    {
        if ($quotation->status !== 'done' || !$quotation->file_path || !Storage::disk('public')->exists($quotation->file_path)) {
            abort(404, 'File báo giá chưa sẵn sàng hoặc không tồn tại.');
        }

        $filePath = Storage::disk('public')->path($quotation->file_path);
        $fileName = "Bao_Gia_An_Binh_{$quotation->customer_code}_{$quotation->id}.pptx";

        return response()->download($filePath, $fileName);
    }
}
