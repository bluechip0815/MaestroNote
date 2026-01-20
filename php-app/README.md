# MaestroNotes PHP Port

This is a **Laravel 11 + Livewire 3** port of the MaestroNotes application.

## Prerequisites

*   PHP 8.2 or higher
*   Composer
*   MySQL 8.0 or higher
*   Node.js & NPM (for frontend assets)

## Installation

1.  **Install Dependencies**
    ```bash
    composer install
    npm install
    ```

2.  **Configuration**
    Copy the example environment file and configure your database and AI keys.
    ```bash
    cp .env.example .env
    php artisan key:generate
    ```
    *Edit `.env` and set your `DB_DATABASE`, `DB_USERNAME`, `DB_PASSWORD`.*

3.  **Database Migration**
    Run the migrations to create the schema.
    ```bash
    php artisan migrate
    ```

4.  **Build Assets**
    ```bash
    npm run build
    ```

5.  **Run Server**
    ```bash
    php artisan serve
    ```

## Architecture

*   **Framework**: Laravel 11
*   **Frontend**: Laravel Livewire 3 (Reactive components without writing complex JS)
*   **Database**: MySQL
*   **Services**:
    *   `App\Services\MusicService`: Handles core business logic and filtering.
    *   `App\Services\AiService`: Handles integration with OpenAI/Gemini.
*   **Authentication**:
    *   Basic Auth via Web Server (`.htaccess`) or Laravel Basic Auth middleware is recommended as per requirements.

## Security

*   CSRF Protection is enabled by default in Laravel.
*   PDO/Eloquent is used for all database queries to prevent SQL Injection.
*   XSS protection is handled by Blade templating engine ({{ }} escapes output).

## Directory Structure

*   `app/Models`: Eloquent Models (MusicRecord, Werk, Komponist, etc.)
*   `app/Services`: Business Logic
*   `app/Livewire`: Frontend Components (Index, Edit, Modals)
*   `database/migrations`: Database Schema Definitions
