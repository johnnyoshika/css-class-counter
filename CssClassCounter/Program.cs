using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace CssClassCounter
{
    class Program
    {
        static async Task Main(string[] args)
        {
            foreach (var pair in await ClassNamesFromHtml(Path.Combine(Directory.GetCurrentDirectory().Split("\\bin\\")[0], "data", "sample.cshtml")))
                Console.WriteLine($"{pair.Key},{pair.Value}");
        }

        static async Task<Dictionary<string, int>> ClassNamesFromHtml(string htmlFile)
        {
            var html = await File.ReadAllTextAsync(htmlFile);
            return html.ClassNames().Sort();
        }
    }

    static class DictionaryExtensions
    {
        public static Dictionary<string, int> Sort(this Dictionary<string, int> dictionary)
        {
            var list = dictionary.ToList();
            list.Sort((pair1, pair2) => pair1.Key.CompareTo(pair2.Key));
            return new Dictionary<string, int>(list);
        }

        public static void AppendCount(this Dictionary<string, int> counter, string key)
        {
            counter.TryGetValue(key, out int count); // count will be set to default (0) if key doesn't exist: https://stackoverflow.com/a/7132978/188740
            counter[key] = count + 1;
        }
    }

    static class HtmlExtensions
    {
        public static Dictionary<string, int> ClassNames(this string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            if (document.ParseErrors.Any())
                throw new ArgumentException(string.Join('|', document.ParseErrors.Select(e => e.Reason)));

            var classNames = new Dictionary<string, int>();
            foreach (var node in document.DocumentNode.Descendants())
                foreach (var className in node.ClassNames())
                    classNames.AppendCount(className);

            return classNames;
        }

        public static IEnumerable<string> ClassNames(this HtmlNode node)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    break;
                case HtmlNodeType.Comment:
                    break;
                case HtmlNodeType.Document:
                    yield return $"HTML document not allowed: '{node.InnerHtml}'";
                    break;
                case HtmlNodeType.Element:
                    if (node.IsScriptTemplate())
                        foreach (var name0 in node.InnerHtml.ClassNames())
                            for (int i = 0; i < name0.Value; i++)
                                yield return name0.Key;

                    foreach (var attribute in node.Attributes)
                        foreach (var name1 in attribute.ClassNames())
                            yield return name1;
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        public static IEnumerable<string> ClassNames(this HtmlAttribute attribute) =>
            attribute.Name == "class"
                ? attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                : new string[0];

        static bool IsScriptTemplate(this HtmlNode node) =>
            node.Name.ToLower() == "script"
            &&
            node.Attributes.Any(a => a.Name.ToLower() == "type")
            && 
            node.Attributes.First(a => a.Name.ToLower() == "type").Value != "text/javascript";
    }
}
