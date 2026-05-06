using System.Xml.Serialization;

namespace Kolokvijum_1.Config;

[XmlRoot("SystemConfig")]
public class SystemConfig
{
    [XmlElement("WorkerCount")]
    public int WorkerCount { get; set; }

    [XmlElement("MaxQueueSize")]
    public int MaxQueueSize { get; set; }

    [XmlArray("Jobs")]
    [XmlArrayItem("Job")]
    public List<JobConfigEntry> Jobs { get; set; } = new();
}

public class JobConfigEntry
{
    [XmlAttribute("Type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("Payload")]
    public string Payload { get; set; } = string.Empty;

    [XmlAttribute("Priority")]
    public int Priority { get; set; }
}