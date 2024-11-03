using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Hosting;
using System.Diagnostics.CodeAnalysis;
using DotVVM.Framework.Utils;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace DotVVM.Framework.Routing
{
    public class RouteTableJsonConverter : JsonConverter<DotvvmRouteTable>
    {
        public override DotvvmRouteTable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, DotvvmRouteTable value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var route in value)
            {
                writer.WriteStartObject(route.RouteName);
                writer.WriteString("url", route.Url);
                writer.WriteString("virtualPath", route.VirtualPath);
                if (route.DefaultValues is not null)
                {
                    writer.WriteStartObject("defaultValues");
                    foreach (var (paramName, defaultValue) in route.DefaultValues)
                    {
                        writer.WritePropertyName(paramName);
                        JsonSerializer.Serialize(writer, defaultValue, options);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
    }
}
