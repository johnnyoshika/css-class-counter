using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace CssClassCounter
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Test();
        }

        static async Task Test()
        {
            var namesFromHtml = await ClassNamesFromHtml(Path.Combine(Directory.GetCurrentDirectory().Split("\\bin\\")[0], "data", "sample.cshtml"));
            var namesFromJS = await ClassNamesFromBackboneJS(Path.Combine(Directory.GetCurrentDirectory().Split("\\bin\\")[0], "data", "sample.js"));

            namesFromJS.MergeInto(namesFromHtml);
            var names = namesFromHtml.Sort().Select(pair => $"{pair.Key},{pair.Value}");
            var lines = names.Prepend("class,count");
            File.WriteAllLines(@"C:\temp\css_classes.csv", lines);
        }

        static async Task Jobcentre()
        {
            var classNames = new Dictionary<string, int>();

            foreach (var htmlFile in Directory.GetFiles(@"C:\Users\Johnny\Documents\GitHub\jobcentre-net\src", "*.cshtml", SearchOption.AllDirectories))
                (await ClassNamesFromHtml(htmlFile)).MergeInto(classNames);

            foreach (var jsFile in Directory.GetFiles(@"C:\Users\Johnny\Documents\GitHub\jobcentre-net\src\assets\js", "*.js", SearchOption.AllDirectories))
                (await ClassNamesFromBackboneJS(jsFile)).MergeInto(classNames);

            var names = classNames.Sort().Select(pair => $"{pair.Key},{pair.Value}");
            var lines = names.Prepend("class,count");
            File.WriteAllLines(@"C:\temp\css_classes_jobcentre.csv", lines);
        }

        static async Task<Dictionary<string, int>> ClassNamesFromHtml(string htmlFile)
        {
            var html = await File.ReadAllTextAsync(htmlFile);
            return html.ClassNames().Sort();
        }

        static async Task<Dictionary<string, int>> ClassNamesFromBackboneJS(string jsFile)
        {
            var lines = await File.ReadAllLinesAsync(jsFile);
            return lines.ClassNames().Sort();
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

        public static void MergeInto(this Dictionary<string, int> source, Dictionary<string, int> destination)
        {
            foreach (var pair in source)
            {
                destination.TryGetValue(pair.Key, out int count); // count will be set to default (0) if key doesn't exist: https://stackoverflow.com/a/7132978/188740
                destination[pair.Key] = count + pair.Value;
            }
        }
    }

    static class HtmlExtensions
    {
        public static Dictionary<string, int> ClassNames(this string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var classNames = new Dictionary<string, int>();
            foreach (var node in document.DocumentNode.Descendants())
                foreach (var className in node.ClassNames())
                    classNames.AppendCount(className);

            return classNames;
        }

        static IEnumerable<string> ClassNames(this HtmlNode node)
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

        static IEnumerable<string> ClassNames(this HtmlAttribute attribute) =>
            attribute.Name == "class"
                ? attribute.Value.StripERB().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                : new string[0];

        // strip everything between and including <% %>, which is underscore.js's template syntax
        static string StripERB(this string s) =>
            Regex.Replace(s, "<%.+?%>", " ");

        static bool IsScriptTemplate(this HtmlNode node) =>
            node.Name.ToLower() == "script"
            &&
            node.Attributes.Any(a => a.Name.ToLower() == "type")
            && 
            node.Attributes.First(a => a.Name.ToLower() == "type").Value != "text/javascript";
    }

    static class JSExtensions
    {
        public static Dictionary<string, int> ClassNames(this IEnumerable<string> lines)
        {
            var regex = new Regex(@"className:\s{1,}['""]([a-zA-Z0-9\-_\s]*)['""]");

            var classNames = new Dictionary<string, int>();

            lines
                .Select(l => regex.Match(l))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value.Trim())
                .SelectMany(names => names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .ToList()
                .ForEach(name => classNames.AppendCount(name));

            return classNames;
        }
    }
}
