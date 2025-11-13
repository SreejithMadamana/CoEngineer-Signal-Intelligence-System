
using System.Xml.Linq;
public class MappingXmlParser
{    public class MappingAttribute
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string FunctionalGroup { get; set; } = "";
        public string ExcelColumnName { get; set; } = "";
    }

    public Dictionary<string, string> PropertyToExcelColumn { get; private set; } = new();
    public List<MappingAttribute> Attributes { get; private set; } = new();

    public MappingXmlParser(string xmlPath)
    {
        LoadMapping(xmlPath);
    }

    private void LoadMapping(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"Mapping XML file not found: {xmlPath}");

        var doc = XDocument.Load(xmlPath);
        foreach (var attr in doc.Descendants("MappingAttribute"))
        {
            var mappingAttr = new MappingAttribute
            {
                Name = attr.Element("Name")?.Value ?? "",
                Description = attr.Element("Description")?.Value ?? "",
                FunctionalGroup = attr.Element("FunctionalGroup")?.Value ?? "",
                ExcelColumnName = attr.Element("ExcelColumnName")?.Value ?? ""
            };

            if (!string.IsNullOrWhiteSpace(mappingAttr.Name) && !string.IsNullOrWhiteSpace(mappingAttr.ExcelColumnName))
            {
                PropertyToExcelColumn[mappingAttr.Name] = mappingAttr.ExcelColumnName;
                Attributes.Add(mappingAttr);
            }
        }
    }

    // Example: Get Excel column name for a property
    public string? GetExcelColumnName(string propertyName)
    {
        PropertyToExcelColumn.TryGetValue(propertyName, out var excelCol);
        return excelCol;
    }

    // Example: Get all mapping attributes
    public IEnumerable<MappingAttribute> GetAllAttributes() => Attributes;
}