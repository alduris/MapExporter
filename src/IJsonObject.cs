using System.Collections.Generic;

namespace MapExporterNew;

interface IJsonObject
{
    Dictionary<string, object> ToJson();
}
