using System;
using System.Threading.Tasks;

namespace MaestroNotes.Data
{
    public class AiDirigentResponseDto
    {
        public DateTime? Born { get; set; }
        public string? Note { get; set; }
    }

    public class AiOrchesterResponseDto
    {
        public DateTime? Founded { get; set; }
        public string? Note { get; set; }
    }

    public class AiService
    {
        public async Task<object> RequestAiData(string name, string itemType)
        {
            // Simulate network delay
            await Task.Delay(500);

            if (itemType.Equals("Dirigent", StringComparison.OrdinalIgnoreCase))
            {
                return new AiDirigentResponseDto
                {
                    Born = new DateTime(1980, 1, 1),
                    Note = $"AI Note for Dirigent {name}: Lorem ipsum dolor sit amet."
                };
            }
            else if (itemType.Equals("Orchester", StringComparison.OrdinalIgnoreCase))
            {
                return new AiOrchesterResponseDto
                {
                    Founded = new DateTime(1950, 5, 20),
                    Note = $"AI Note for Orchester {name}: Consectetur adipiscing elit."
                };
            }

            throw new ArgumentException("Unknown item type", nameof(itemType));
        }
    }
}
