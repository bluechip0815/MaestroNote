<?php

namespace App\Services;

use OpenAI;
use Illuminate\Support\Facades\Log;

class AiService
{
    protected $client;

    public function __construct()
    {
        $apiKey = config('services.openai.key') ?? env('OPENAI_API_KEY');
        if ($apiKey) {
            $this->client = OpenAI::client($apiKey);
        }
    }

    public function requestAiData(string $userPrompt, string $context = ''): ?string
    {
        if (!$this->client) {
            return null;
        }

        try {
            $response = $this->client->chat()->create([
                'model' => 'gpt-3.5-turbo',
                'messages' => [
                    ['role' => 'system', 'content' => 'You are a helpful assistant for a classical music database. ' . $context],
                    ['role' => 'user', 'content' => $userPrompt],
                ],
            ]);

            return $response->choices[0]->message->content;
        } catch (\Exception $e) {
            Log::error("OpenAI Error: " . $e->getMessage());
            return null;
        }
    }
}
