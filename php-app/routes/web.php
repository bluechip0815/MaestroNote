<?php

use Illuminate\Support\Facades\Route;
use App\Livewire\MusicRecordIndex;
use App\Livewire\MusicRecordEdit;

Route::get('/', MusicRecordIndex::class)->name('home');
Route::get('/create', MusicRecordEdit::class)->name('create');
Route::get('/edit/{id}', MusicRecordEdit::class)->name('edit');
