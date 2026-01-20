<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Ort extends Model
{
    use HasFactory;

    protected $table = 'orte';

    protected $fillable = [
        'name',
        'note',
    ];
}
