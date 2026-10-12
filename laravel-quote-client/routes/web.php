<?php

use App\Http\Controllers\QuotationController;
use Illuminate\Support\Facades\Route;

// Trang chủ chuyển hướng thẳng vào form tạo báo giá
Route::get('/', function () {
    return redirect()->route('quotations.create');
});

// Các route cho quy trình báo giá
Route::prefix('quotations')->name('quotations.')->group(function () {
    Route::get('/create', [QuotationController::class, 'create'])->name('create');
    Route::post('/', [QuotationController::class, 'store'])->name('store');
    Route::get('/{quotation}', [QuotationController::class, 'show'])->name('show');
    Route::get('/{quotation}/check-status', [QuotationController::class, 'checkStatus'])->name('checkStatus');
    Route::get('/{quotation}/download', [QuotationController::class, 'download'])->name('download');
});
