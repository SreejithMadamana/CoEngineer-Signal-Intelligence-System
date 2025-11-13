public class SignalItem
{
    // Holds all attribute values, keyed by mapping attribute name
    public Dictionary<string, object> Attributes { get; set; } = new();

    // Optionally, provide indexer for convenience
    public object this[string key]
    {
        get => Attributes.ContainsKey(key) ? Attributes[key] : null;
        set => Attributes[key] = value;
    }
}