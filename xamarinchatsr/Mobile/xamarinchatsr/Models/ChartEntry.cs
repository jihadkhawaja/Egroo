using System;

namespace xamarinchatsr.Models
{
    [Serializable]
    public class ChartEntry
    {
        public string label { get; set; }
        public string valuelabel { get; set; }
        public string color { get; set; }
        public float value { get; set; }

        public ChartEntry()
        {
        }

        public ChartEntry(string label, string valuelabel, string color, float value)
        {
            this.label = label;
            this.valuelabel = valuelabel;
            this.color = color;
            this.value = value;
        }
    }
}