<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('orchester', function (Blueprint $table) {
            $table->id();
            $table->string('name', 100)->default('');
            $table->date('founded')->nullable();
            $table->string('note', 1000)->nullable();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('orchester');
    }
};
