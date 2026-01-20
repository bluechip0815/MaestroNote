<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('documents', function (Blueprint $table) {
            $table->id();
            $table->string('file_name', 250)->default('');
            $table->string('encrypted_name', 250)->default('');
            $table->unsignedTinyInteger('document_type')->default(0); // 0=Pdf, 1=Image

            $table->foreignId('music_record_id')
                  ->constrained('music_records')
                  ->cascadeOnDelete();

            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('documents');
    }
};
