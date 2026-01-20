<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('solisten', function (Blueprint $table) {
            $table->id();
            $table->string('vorname', 50)->default('');
            $table->string('name', 50)->default('');
            $table->date('born')->nullable();
            $table->string('note', 1000)->nullable();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('solisten');
    }
};
