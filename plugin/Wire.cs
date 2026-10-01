using System.Collections.Generic;
using Newtonsoft.Json;

namespace SPTGlobalChat
{
    // The frames in docs/PROTOCOL.md, as fixed classes. Nothing from the relay is ever
    // read into anything looser than these.

    public sealed class ClientFrame
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)] public string Text;
        [JsonProperty("token", NullValueHandling = NullValueHandling.Ignore)] public string Token;
    }

    public sealed class ServerFrame
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("from")] public string From;
        [JsonProperty("tag")] public string Tag;
        [JsonProperty("text")] public string Text;
        [JsonProperty("at")] public long At;
        [JsonProperty("reason")] public string Reason;
        [JsonProperty("name")] public string Name;
        [JsonProperty("messages")] public List<ServerFrame> Messages;
    }

    public static class Wire
    {
        // TypeNameHandling.None is the default, and stated anyway: any other value lets
        // the sender choose which .NET type is constructed, which is a known route to
        // running code. Metadata properties ($type, $ref) are ignored outright.
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 8,
        };

        public static string Say(string text) =>
            JsonConvert.SerializeObject(new ClientFrame { Type = "say", Text = text }, Settings);

        /// <summary>Null for anything that is not a well-formed frame.</summary>
        public static ServerFrame Parse(string raw)
        {
            try
            {
                return JsonConvert.DeserializeObject<ServerFrame>(raw, Settings);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
