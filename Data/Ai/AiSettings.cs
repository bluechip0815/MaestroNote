namespace MaestroNotes.Data.Ai
{
    public class AiSettings
    {
        public string System { get; set; } = "You are a music expert.";
        public string Provider { get; set; } = "ChatGPT"; // ChatGPT, Gemini, Anthropic
        public string ProviderUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public string Proxy { get; set; } = "";// Optional proxy URL
        public bool BypassLocal { get; set; } = false;
        public Dictionary<string, AiPromptSettings> Prompts { get; set; } = new();
    }

    public class AiPromptSettings
    {
        public string User { get; set; } = "";
    }
}
