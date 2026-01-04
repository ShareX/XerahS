using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ShareX.Editor.Annotations;

namespace ShareX.Editor.Serialization
{
    public static class AnnotationSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            IgnoreReadOnlyProperties = true
        };

        public static string Serialize(IEnumerable<Annotation> annotations)
        {
            return JsonSerializer.Serialize(annotations, _options);
        }

        public static List<Annotation>? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<List<Annotation>>(json, _options);
        }

        public static async Task SaveToFileAsync(IEnumerable<Annotation> annotations, string path)
        {
            var json = Serialize(annotations);
            await File.WriteAllTextAsync(path, json);
        }

        public static async Task<List<Annotation>> LoadFromFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                return new List<Annotation>();
            }

            var json = await File.ReadAllTextAsync(path);
            return Deserialize(json) ?? new List<Annotation>();
        }
    }
}
