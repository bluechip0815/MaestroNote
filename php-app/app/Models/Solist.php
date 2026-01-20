<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Solist extends Model
{
    use HasFactory;

    protected $table = 'solisten';

    protected $fillable = [
        'vorname',
        'name',
        'born',
        'note',
    ];

    protected $casts = [
        'born' => 'date',
    ];

    public function musicRecords()
    {
        return $this->belongsToMany(MusicRecord::class, 'music_record_solist');
    }
}
