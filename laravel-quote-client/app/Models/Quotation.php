<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Quotation extends Model
{
    use HasFactory;

    protected $fillable = [
        'customer_code',
        'customer_name',
        'items',
        'status',
        'executor_quote_id',
        'file_path',
        'error',
    ];

    protected $casts = [
        'items' => 'array',
    ];
}
