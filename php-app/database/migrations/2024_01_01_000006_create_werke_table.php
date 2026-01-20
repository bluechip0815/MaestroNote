<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('werke', function (Blueprint $table) {
            $table->id();
            $table->string('name', 200)->default('');
            $table->string('note', 1000)->nullable();

            $table->foreignId('komponist_id')
                  ->nullable()
                  ->constrained('komponisten')
                  ->nullOnDelete();

            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('werke');
    }
};
