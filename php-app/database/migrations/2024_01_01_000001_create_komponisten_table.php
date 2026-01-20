<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('komponisten', function (Blueprint $table) {
            $table->id();
            $table->string('vorname', 50)->nullable();
            $table->string('name', 50)->default(''); // C# default is ""
            $table->date('born')->nullable();
            $table->string('note', 1000)->default(''); // C# default is ""
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('komponisten');
    }
};
