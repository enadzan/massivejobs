using System.Collections.Generic;

namespace MassiveJobs.Core
{
    public class StatsMessage
    {
        public string PublisherId { get; set; }
        public Dictionary<string, string> Stats { get; set; } = new Dictionary<string, string>();
    }
}
