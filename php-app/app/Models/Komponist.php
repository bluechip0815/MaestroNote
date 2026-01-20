<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Komponist extends Model
{
    use HasFactory;

    protected $table = 'komponisten';

    protected $fillable = [
        'vorname',
        'name',
        'born',
        'note',
    ];

    protected $casts = [
        'born' => 'date',
    ];

    public function werke()
    {
        return $this->hasMany(Werk::class);
    }
}
