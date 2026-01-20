<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class MusicRecord extends Model
{
    use HasFactory;

    protected $table = 'music_records';

    protected $fillable = [
        'bezeichnung',
        'datum',
        'spielsaison',
        'bewertung',
        'dirigent_id',
        'orchester_id',
        'ort_id',
    ];

    protected $casts = [
        'datum' => 'datetime',
    ];

    public function dirigent()
    {
        return $this->belongsTo(Dirigent::class);
    }

    public function orchester()
    {
        return $this->belongsTo(Orchester::class);
    }

    public function ort()
    {
        return $this->belongsTo(Ort::class);
    }

    public function werke()
    {
        return $this->belongsToMany(Werk::class, 'music_record_werk');
    }

    public function solisten()
    {
        return $this->belongsToMany(Solist::class, 'music_record_solist');
    }

    public function documents()
    {
        return $this->hasMany(Document::class);
    }
}
