<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Orchester extends Model
{
    use HasFactory;

    protected $table = 'orchester';

    protected $fillable = [
        'name',
        'founded',
        'note',
    ];

    protected $casts = [
        'founded' => 'date',
    ];
}
