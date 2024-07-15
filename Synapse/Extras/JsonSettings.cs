using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Synapse.Extras;

internal static class JsonSettings
{
    internal static JsonSerializerSettings Settings { get; } = new()
    {
        ContractResolver = new PrivateResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    private class PrivateResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty prop = base.CreateProperty(member, memberSerialization);
            if (prop.Writable)
            {
                return prop;
            }

            PropertyInfo? property = member as PropertyInfo;
            bool hasPrivateSetter = property?.GetSetMethod(true) != null;
            prop.Writable = hasPrivateSetter;
            return prop;
        }
    }
}
