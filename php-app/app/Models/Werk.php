<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Werk extends Model
{
    use HasFactory;

    protected $table = 'werke';

    protected $fillable = [
        'name',
        'note',
        'komponist_id',
    ];

    public function komponist()
    {
        return $this->belongsTo(Komponist::class);
    }

    public function musicRecords()
    {
        return $this->belongsToMany(MusicRecord::class, 'music_record_werk');
    }
}
