<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('music_record_werk', function (Blueprint $table) {
            $table->id();

            $table->foreignId('music_record_id')
                  ->constrained('music_records')
                  ->cascadeOnDelete();

            $table->foreignId('werk_id')
                  ->constrained('werke')
                  ->cascadeOnDelete();
        });

        Schema::create('music_record_solist', function (Blueprint $table) {
            $table->id();

            $table->foreignId('music_record_id')
                  ->constrained('music_records')
                  ->cascadeOnDelete();

            $table->foreignId('solist_id')
                  ->constrained('solisten')
                  ->cascadeOnDelete();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('music_record_solist');
        Schema::dropIfExists('music_record_werk');
    }
};
