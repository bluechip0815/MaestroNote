<?php

namespace App\Models;

use App\Enums\DocumentType;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class Document extends Model
{
    use HasFactory;

    protected $fillable = [
        'file_name',
        'encrypted_name',
        'document_type',
        'music_record_id',
    ];

    protected $casts = [
        'document_type' => DocumentType::class,
    ];

    public function musicRecord()
    {
        return $this->belongsTo(MusicRecord::class);
    }
}
