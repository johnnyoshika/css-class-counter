using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSSParser;
using CSSParser.ContentProcessors;
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
            await WriteCsv(namesFromHtml, @"C:\temp\html_class_names_test.csv");

            var namesFromCss = await ClassNamesFromCss(Path.Combine(Directory.GetCurrentDirectory().Split("\\bin\\")[0], "data", "sample.css"));
            await WriteCsv(namesFromCss, @"C:\temp\css_class_names_test.csv");

            var join = namesFromHtml.OuterJoin(namesFromCss);
            await WriteCsv(join, @"C:\temp\css_class_names_join_test.csv", "HTML", "Stylesheet");
        }

        static async Task Jobcentre()
        {
            var namesFromHtml = new Dictionary<string, int>();

            foreach (var htmlFile in Directory.GetFiles(@"C:\Users\Johnny\Documents\GitHub\jobcentre-net\src", "*.cshtml", SearchOption.AllDirectories))
                (await ClassNamesFromHtml(htmlFile)).MergeInto(namesFromHtml);

            foreach (var jsFile in Directory.GetFiles(@"C:\Users\Johnny\Documents\GitHub\jobcentre-net\src\assets\js", "*.js", SearchOption.AllDirectories))
                (await ClassNamesFromBackboneJS(jsFile)).MergeInto(namesFromHtml);

            await WriteCsv(namesFromHtml, @"C:\temp\html_class_names_jobcentre.csv");

            var namesFromCss = await ClassNamesFromCss(@"C:\Users\Johnny\Documents\GitHub\jobcentre-net\src\assets\css\jobcentre.css");
            await WriteCsv(namesFromCss, @"C:\temp\css_class_names_jobcentre.csv");

            var join = namesFromHtml.OuterJoin(namesFromCss);
            await WriteCsv(join, @"C:\temp\css_class_names_join_jobcentre.csv", "HTML", "Stylesheet");
        }

        static async Task WriteCsv(Dictionary<string, int> classNames, string file)
        {
            var names = classNames.Sort().Select(pair => $"{pair.Key},{pair.Value}");
            var lines = names.Prepend("class,count");
            await File.WriteAllLinesAsync(file, lines);
        }

        static async Task WriteCsv(Dictionary<string, Pair> join, string file, string leftLabel, string rightLabel)
        {
            var names = join.Sort().Select(pair => $"{pair.Key},{pair.Value.Left},{pair.Value.Right}");
            var lines = names.Prepend($"class,{leftLabel},{rightLabel}");
            await File.WriteAllLinesAsync(file, lines);
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

        static async Task<Dictionary<string, int>> ClassNamesFromCss(string cssFile)
        {
            var css = await File.ReadAllTextAsync(cssFile);

            var classNames = new Dictionary<string, int>();
            Parser.ParseCSS(css)
                .Where(c => c.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty && c.Value.StartsWith('.'))
                .SelectMany(c => c.Value.Split('.', StringSplitOptions.RemoveEmptyEntries))
                .Select(c => c.Split(':').First()) // Remove elemant state: btn:hover
                .Select(c => Regex.Replace(c, "\\[.+?\\]", "")) // Remove attribute selector: btn-default[disabled]
                .ToList()
                .ForEach(name => classNames.AppendCount(name.TrimEnd(',')));

            return classNames.Sort();
        }
    }

    class Pair
    {
        public Pair(int left, int right)
        {
            Left = left;
            Right = right;
        }

        public int Left { get; }
        public int Right { get; }
    }

    static class DictionaryExtensions
    {
        public static Dictionary<string, int> Sort(this Dictionary<string, int> dictionary)
        {
            var list = dictionary.ToList();
            list.Sort((pair1, pair2) => pair1.Key.CompareTo(pair2.Key));
            return new Dictionary<string, int>(list);
        }

        public static Dictionary<string, Pair> Sort(this Dictionary<string, Pair> dictionary)
        {
            var list = dictionary.ToList();
            list.Sort((pair1, pair2) => pair1.Key.CompareTo(pair2.Key));
            return new Dictionary<string, Pair>(list);
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

        public static Dictionary<string, Pair> OuterJoin(this Dictionary<string, int> left, Dictionary<string, int> right)
        {
            var join = new Dictionary<string, Pair>();
            foreach (var l in left)
            {
                right.TryGetValue(l.Key, out int r);
                join[l.Key] = new Pair(l.Value, r);
            }

            foreach (var r in right)
            {
                if (join.ContainsKey(r.Key))
                    continue;

                join[r.Key] = new Pair(0, r.Value);
            }

            return join;
        }
    }

    static class HtmlExtensions
    {
        public static Dictionary<string, int> ClassNames(this string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html.Cleans());

            var classNames = new Dictionary<string, int>();
            foreach (var node in document.DocumentNode.Descendants())
                foreach (var className in node.ClassNames())
                    classNames.AppendCount(className);

            return classNames;
        }

        static string Cleans(this string s)
        {
            var regex1 = new Regex(@"class=\""\""[a-zA-Z0-9\-_\s]+?\""\""");
            var regex2 = new Regex(@"class=\\""[a-zA-Z0-9\-_\s]+?\\""");

            s = regex1.Matches(s).Aggregate(s, (accumulator, m) => s.Replace(m.Value, m.Value.Replace(@"""""", @"""")));
            s = regex2.Matches(s).Aggregate(s, (accumulator, m) => s.Replace(m.Value, m.Value.Replace(@"\""", @"""")));

            return s;
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
                            if (!name1.IsNoise())
                                if (name1 == "blahblahblahblahblah")
                                    throw new InvalidOperationException(); // debugging: conditional breakpoints don't seem to work, so put a breakpoint here
                                else
                                    yield return name1;
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        static bool IsNoise(this string s) =>
            new[]
            {
                "!=",
                "%",
                "?",
                "=="
            }.Contains(s)
            ||
            int.TryParse(s, out int result)
            ||
            s.StartsWith("(")
            ||
            s.StartsWith("@");

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
