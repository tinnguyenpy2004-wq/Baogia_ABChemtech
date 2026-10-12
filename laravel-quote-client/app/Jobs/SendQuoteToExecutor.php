<?php

namespace App\Jobs;

use App\Models\Quotation;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Throwable;

class SendQuoteToExecutor implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public Quotation $quotation;

    /**
     * Số lần retry tối đa khi gặp sự cố mạng hoặc timeout.
     */
    public int $tries = 3;

    /**
     * Timeout tối đa cho job (giây).
     */
    public int $timeout = 30;

    /**
     * Thời gian chờ giữa các lần retry (giây).
     */
    public int $backoff = 3;

    public function __construct(Quotation $quotation)
    {
        $this->quotation = $quotation;
    }

    /**
     * Thực thi gửi yêu cầu báo giá tới .NET Executor qua mạng Tailscale.
     */
    public function handle(): void
    {
        $executorUrl = rtrim(env('EXECUTOR_URL', 'http://127.0.0.1:5000'), '/');
        $executorToken = env('EXECUTOR_TOKEN', 'anbinh-chemtech-secret-token');

        // Chuẩn bị payload gửi sang Executor API
        $payload = [
            'customer' => $this->quotation->customer_name,
            'customer_code' => $this->quotation->customer_code,
            'quoteDate' => now()->format('Y-m-d'),
            'quoteNumber' => [
                'sequence' => str_pad((string) $this->quotation->id, 4, '0', STR_PAD_LEFT),
                'yearMonth' => now()->format('ym'),
                'revision' => '01',
            ],
            'items' => collect($this->quotation->items)->map(function ($item) {
                return [
                    'productName' => $item['name'] ?? 'Sản phẩm',
                    'priceWithoutVat' => (float) ($item['price'] ?? 0),
                ];
            })->toArray(),
        ];

        try {
            // Gọi POST /quotes với Header X-API-Key
            $response = Http::withHeaders([
                'X-API-Key' => $executorToken,
                'Accept' => 'application/json',
            ])
            ->timeout(15) // Giới hạn timeout 15 giây
            ->post("{$executorUrl}/quotes", $payload);

            if ($response->successful()) {
                $quoteId = $response->json('quote_id');

                $this->quotation->update([
                    'executor_quote_id' => $quoteId,
                    'status' => 'processing',
                    'error' => null,
                ]);

                Log::info("[Job SendQuoteToExecutor] Báo giá #{$this->quotation->id} đã gửi tới Executor thành công. Quote ID: {$quoteId}");
            } else {
                $errorMsg = "Executor trả về mã lỗi {$response->status()}: " . $response->body();
                Log::error("[Job SendQuoteToExecutor] Lỗi: {$errorMsg}");

                $this->quotation->update([
                    'status' => 'failed',
                    'error' => $errorMsg,
                ]);
            }
        } catch (Throwable $e) {
            $errorMsg = "Không thể kết nối tới Executor tại {$executorUrl}: " . $e->getMessage();
            Log::error("[Job SendQuoteToExecutor] Ngoại lệ: {$errorMsg}");

            $this->quotation->update([
                'status' => 'failed',
                'error' => $errorMsg,
            ]);

            // Ném lỗi để Laravel Queue kích hoạt retry nếu còn lượt
            throw $e;
        }
    }

    /**
     * Xử lý khi job thất bại hoàn toàn sau tất cả các lần retry.
     */
    public function failed(Throwable $exception): void
    {
        Log::critical("[Job SendQuoteToExecutor] Báo giá #{$this->quotation->id} thất bại vĩnh viễn: " . $exception->getMessage());

        $this->quotation->update([
            'status' => 'failed',
            'error' => "Executor không phản hồi sau {$this->tries} lần thử. Chi tiết: " . $exception->getMessage(),
        ]);
    }
}
