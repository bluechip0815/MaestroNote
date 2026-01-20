<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('music_records', function (Blueprint $table) {
            $table->id();
            $table->string('bezeichnung', 200)->default('');
            $table->dateTime('datum')->useCurrent();
            $table->string('spielsaison', 64)->default('');
            $table->text('bewertung')->nullable(); // C# max 2000

            $table->foreignId('dirigent_id')
                  ->nullable()
                  ->constrained('dirigenten')
                  ->nullOnDelete();

            $table->foreignId('orchester_id')
                  ->nullable()
                  ->constrained('orchester')
                  ->nullOnDelete();

            $table->foreignId('ort_id')
                  ->nullable()
                  ->constrained('orte')
                  ->nullOnDelete();

            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('music_records');
    }
};
