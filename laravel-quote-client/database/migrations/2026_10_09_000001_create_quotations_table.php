<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::create('quotations', function (Blueprint $table) {
            $table->id();
            $table->string('customer_code')->nullable();
            $table->string('customer_name');
            $table->json('items'); // Danh sách sản phẩm: name, price
            $table->enum('status', ['pending', 'processing', 'done', 'failed'])->default('pending');
            $table->string('executor_quote_id')->nullable()->index();
            $table->string('file_path')->nullable(); // Đường dẫn file PPTX sau khi tải về
            $table->text('error')->nullable(); // Ghi nhận lỗi nếu thất bại
            $table->timestamps();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('quotations');
    }
};
