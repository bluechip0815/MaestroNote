<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Dirigent extends Model
{
    use HasFactory;

    protected $table = 'dirigenten';

    protected $fillable = [
        'vorname',
        'name',
        'born',
        'note',
    ];

    protected $casts = [
        'born' => 'date',
    ];
}
