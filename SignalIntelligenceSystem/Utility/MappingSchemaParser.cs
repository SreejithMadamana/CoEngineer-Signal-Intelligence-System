
using System.Xml.Linq;
public class MappingAttribute
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string FunctionalGroup { get; set; }
    public string ExcelColumnName { get; set; }
}

public static class MappingSchemaParser
{
    public static List<MappingAttribute> Parse(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath);
        var attributes = new List<MappingAttribute>();

        foreach (var attr in doc.Descendants("MappingAttribute"))
        {
            attributes.Add(new MappingAttribute
            {
                Name = attr.Element("Name")?.Value,
                Description = attr.Element("Description")?.Value,
                FunctionalGroup = attr.Element("FunctionalGroup")?.Value,
                ExcelColumnName = attr.Element("ExcelColumnName")?.Value
            });
        }
        return attributes;
    }
}