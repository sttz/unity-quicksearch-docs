using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ch.sttz.quicksearch.docs
{

/// <summary>
/// Create a search index from a Unity offline documentation.
/// </summary>
public static class DocsIndexGenerator
{
    /// <summary>
    /// Relative path to where the search index JSON is stored.
    /// </summary>
    const string IndexPath = "en/ScriptReference/docdata/index.json";
    /// <summary>
    /// Relative path to the file from which the documentation version is extracted.
    /// </summary>
    const string VersionPath = "en/ScriptReference/index.html";
    /// <summary>
    /// Prefix in the documentation folder to locate files by the urls in the index.
    /// </summary>
    const string UrlPrefix = "en/ScriptReference";
    /// <summary>
    /// Regex to parse version from <see cref="VersionPath"/>.
    /// </summary>
    static readonly Regex VersionRegex = new Regex(@"Version: <b>(\d+\.\d+)</b>");
    /// <summary>
    /// Regex to parse publication date from <see cref="VersionPath"/>.
    /// </summary>
    static readonly Regex PublicationDateRegex = new Regex(@"Publication Date: (\d{4}-\d{2}-\d{2})");

    /// <summary>
    /// Used to save last open dialog location for picking the documentation path.
    /// </summary>
    const string LastDocumentationPathKey = "QuickSearchDocs.DocumentationPath";
    /// <summary>
    /// Used to save the last save dialog location for saving the index.
    /// </summary>
    const string LastSavePathKey = "QuickSearchDocs.SavePath";

    /// <summary>
    /// Struct used to parse the index JSON file.
    /// </summary>
    [Serializable]
    struct IndexData {
        #pragma warning disable 0649
        public string[][] pages;
        public object[][] info;
        public Dictionary<string, int> common;
        public Dictionary<string, int[]> searchIndex;
        #pragma warning restore 0649
    }

    /// <summary>
    /// Prompts to choose an offline documentation folder and then generates the index for it.
    /// </summary>
    [MenuItem("Window/Quick Search/Generate Docs Index...", false, 5000)]
    public static void GenerateIndex()
    {
        var path = EditorUtility.OpenFolderPanel("Select Unity Documentation", EditorPrefs.GetString(LastDocumentationPathKey), "");
        if (string.IsNullOrEmpty(path)) return;

        var indexPath = Path.Combine(path, IndexPath);
        if (!File.Exists(indexPath)) {
            EditorUtility.DisplayDialog(
                "Select Unity Documentation", 
                "Selected folder could not be recognized as Unity documentation.\n\nSelect the top level folder that contains 'Documentation.html'.", 
                "Bummer"
            );
            return;
        }
        EditorPrefs.SetString(LastDocumentationPathKey, path);

        var outputPath = EditorUtility.SaveFolderPanel("Select Output Folder", EditorPrefs.GetString(LastSavePathKey), "");
        if (string.IsNullOrEmpty(outputPath)) return;
        EditorPrefs.SetString(LastSavePathKey, outputPath);

        GenerateIndex(path, outputPath);
    }

    const string ProgressTitle = "Generate Index";

    /// <summary>
    /// Generate the index for the offline documentation at the given path.
    /// </summary>
    public static void GenerateIndex(string docsPath, string outputPath)
    {
        try {
            var indexPath = Path.Combine(docsPath, IndexPath);
            if (!File.Exists(indexPath)) {
                Debug.LogError($"Invalid docs path: Could not find index at {indexPath}");
                return;
            }

            var versionPath = Path.Combine(docsPath, VersionPath);
            if (!File.Exists(versionPath)) {
                Debug.LogError($"Invalid docs path: Could not find index at {versionPath}");
                return;
            }

            EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Converting original index...", 0.0f);

            // Deserialize index JSON
            IndexData data;
            using (var file = File.OpenText(indexPath)) {
                var serializer = new JsonSerializer();
                data = (IndexData)serializer.Deserialize(file, typeof(IndexData));
            }

            if (data.pages == null || data.info == null || data.common == null || data.searchIndex == null) {
                Debug.LogError($"Failed to parse search index. {data.pages} / {data.info} / {data.common} / {data.searchIndex}");
                return;
            }

            var index = new DocsIndex();
            index.common = data.common.Keys.ToArray();

            // Convert index dictionary to two sorted arrays for keys/values
            index.indexKeys = new string[data.searchIndex.Count];
            index.indexValues = new DocsIndex.Entry[data.searchIndex.Count];
            var k = 0;
            foreach (var pair in data.searchIndex) {
                index.indexKeys[k] = pair.Key;
                index.indexValues[k] = new DocsIndex.Entry { pages = pair.Value };
                k++;
            }

            Array.Sort(index.indexKeys, index.indexValues);

            if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Parsing Documentation...", 0.1f)) return;

            // Collect page data into Page structs
            var pageCount = data.pages.Length;
            var typeCache = new Dictionary<string, DocsIndex.PageType>(pageCount);
            index.pages = new DocsIndex.Page[pageCount];
            for (int i = 0; i < pageCount; i++) {
                var progress = 0.1f + 0.8f * (i / (pageCount - 1f));
                if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Parsing Documentation...", progress)) return;
                var url = data.pages[i][0];
                index.pages[i] = new DocsIndex.Page() {
                    title = data.pages[i][1],
                    description = (string)data.info[i][0],
                    url = url,
                    type = DeterminePageType(url, docsPath, typeCache)
                };
            }

            if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Finishing...", 0.9f)) return;

            // Determine version of documentation
            index.unityVersion = default;

            var mainHtml = File.ReadAllText(versionPath);
            var versionMatch = VersionRegex.Match(mainHtml);
            if (!versionMatch.Success) {
                Debug.LogWarning($"Could not determine Unity version of docs (using '{versionPath}').");
            } else {
                DocsIndex.MajorMinorVersion.TryParse(versionMatch.Groups[1].Value, out index.unityVersion);
            }

            var dateMatch = PublicationDateRegex.Match(mainHtml);
            if (!dateMatch.Success) {
                Debug.LogWarning($"Could not determine publication date of docs (using '{versionPath}').");
            } else {
                index.publicationDate = dateMatch.Groups[1].Value;
            }

            // Create index asset
            var output = Path.Combine(outputPath, $"DocsIndex-{index.unityVersion}-{index.publicationDate ?? index.docsVersion ?? "unknown"}.json");
            var json = JsonUtility.ToJson(index);
            File.WriteAllText(output, json);

            EditorUtility.ClearProgressBar();

            Debug.Log($"Saved index with {index.pages.Length} pages and {index.indexKeys.Length} entries to '{output}'.");
        } finally {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Identifiers to look for in documentation files to determine main types.
    /// </summary>
    static readonly Dictionary<string, DocsIndex.PageType> typeIdentifiers = new Dictionary<string, DocsIndex.PageType> {
        ["class in"] = DocsIndex.PageType.Class,
        ["struct in"] = DocsIndex.PageType.Struct,
        ["interface in"] = DocsIndex.PageType.Interface,
        ["enumeration"] = DocsIndex.PageType.Enumeration,
    };

    /// <summary>
    /// Identifiers to look for in main type documentation files to determine member types.
    /// </summary>
    static readonly Dictionary<string, DocsIndex.PageType> memberIdentifiers = new Dictionary<string, DocsIndex.PageType> {
        ["Description"] = DocsIndex.PageType.Unknown,
        ["Inherited Members"] = DocsIndex.PageType.Unknown,
        ["Static Properties"] = DocsIndex.PageType.Property,
        ["Static Methods"] = DocsIndex.PageType.Method,
        ["Properties"] = DocsIndex.PageType.Property,
        ["Constructors"] = DocsIndex.PageType.Method,
        ["Public Methods"] = DocsIndex.PageType.Method,
        ["Protected Methods"] = DocsIndex.PageType.Method,
        ["Messages"] = DocsIndex.PageType.Message,
        ["Events"] = DocsIndex.PageType.Event,
        ["Delegates"] = DocsIndex.PageType.Delegate,
        ["Operators"] = DocsIndex.PageType.Method,
    };

    /// <summary>
    /// Identifier to look for to determine obsolete types.
    /// </summary>
    const string IsObsolete = "Obsolete";
    /// <summary>
    /// Identifier to look for to determine delegates.
    /// </summary>
    const string IsDelegate = "public delegate";
    /// <summary>
    /// String used to detect broken pages that have no content.
    /// </summary>
    const string LeaveFeedback = "Leave feedback";

    /// <summary>
    /// Regex to parse member type headings in member lists.
    /// </summary>
    static readonly Regex MemberTypeRegex = new Regex(@"<div class=""subsection""><h2>([ \w]+)<\/h2>");
    /// <summary>
    /// Regex to parse member URL in member lists.
    /// </summary>
    static readonly Regex MemberRegex = new Regex(@"<td class=""lbl""><a href=""([^""\/]+)\.html"">([^<]+)<\/a>");

    /// <summary>
    /// Some pages are irregular and their correct type is defined here.
    /// </summary>
    static readonly Dictionary<string, DocsIndex.PageType> pageTypeOverrides = new Dictionary<string, DocsIndex.PageType> {
        // Pseudo-pages
        ["Array"] = DocsIndex.PageType.Class,
        ["Hashtable"] = DocsIndex.PageType.Class,
        ["String"] = DocsIndex.PageType.Class,
        ["Serializable"] = DocsIndex.PageType.Class,
        ["NonSerialized"] = DocsIndex.PageType.Class,
        ["Path"] = DocsIndex.PageType.Class,
        // Broken pages
        ["PopupWindow"] = DocsIndex.PageType.Class,
        ["XR.XRNodeState"] = DocsIndex.PageType.Struct,
        ["LightingExplorerTableColumn.DataType.Checkbox"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Color"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Custom"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Enum"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Float"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Int"] = DocsIndex.PageType.Enumerator,
        ["LightingExplorerTableColumn.DataType.Name"] = DocsIndex.PageType.Enumerator,
        ["ProjectWindowCallback.EndNameEditAction"] = DocsIndex.PageType.Class,
        ["ProjectWindowCallback.EndNameEditAction.Action"] = DocsIndex.PageType.Method,
        ["ProjectWindowCallback.EndNameEditAction.Cancelled"] = DocsIndex.PageType.Method,
    };

    /// <summary>
    /// Determine a page's type.
    /// </summary>
    /// <param name="url">Index URL of the page</param>
    /// <param name="docsPath">Path to the offline documentation</param>
    /// <param name="cache">Cache of already determined types</param>
    /// <returns></returns>
    static DocsIndex.PageType DeterminePageType(string url, string docsPath, Dictionary<string, DocsIndex.PageType> cache)
    {
        if (cache.TryGetValue(url, out var type)) {
            return type;
        }

        var pagePath = Path.Combine(docsPath, UrlPrefix, url + ".html");
        if (!File.Exists(pagePath)) {
            Debug.LogError($"Could not find documentation page at path: {pagePath}");
            type = cache[url] = DocsIndex.PageType.Unknown;
            return type;
        }

        var pageContents = File.ReadAllText(pagePath);

        if (!pageContents.Contains(LeaveFeedback)) {
            Debug.LogWarning($"Skipping page with potentially no content at path: {pagePath}");
            return type;
        }

        if ((url.StartsWith("UnityEngine") && url.EndsWith("Module")) || url == "UnityEditor") {
            type = cache[url] = DocsIndex.PageType.Module;
            return type;
        } else if (pageContents.Contains(IsObsolete)) {
            type = cache[url] = DocsIndex.PageType.Obsolete;
            return type;
        } else if (pageContents.Contains(IsDelegate)) {
            type = cache[url] = DocsIndex.PageType.Delegate;
            return type;
        } else if (!pageContents.Contains('\n')) {
            // There are some empty page for undocumented members in the index
            Debug.LogWarning($"Probably broken documentation page? ({pagePath})");
            type = cache[url] = DocsIndex.PageType.Unknown;
            return type;
        }

        // Determine wether the page represents a type or member
        var pageType = DocsIndex.PageType.Unknown;
        if (!pageTypeOverrides.TryGetValue(url, out pageType)) {
            foreach (var pair in typeIdentifiers) {
                if (pageContents.Contains(pair.Key)) {
                    pageType = pair.Value;
                    break;
                }
            }
        }

        if (pageType == DocsIndex.PageType.Unknown) {
            // For members, types are parsed in the parent type
            // We parse the parent and then use the cache to look up the member
            var lastPos = url.LastIndexOf('-');
            if (lastPos < 0) {
                lastPos = url.LastIndexOf('.');
                if (lastPos < 0) {
                    Debug.LogError($"Could not determine parent of member: {pagePath}");
                    type = cache[url] = DocsIndex.PageType.Unknown;
                    return type;
                }
            }

            var parentUrl = url.Substring(0, lastPos);
            var parentType = DeterminePageType(parentUrl, docsPath, cache);

            // Some members of obsolete type are not marked obsolete themselves
            if (parentType == DocsIndex.PageType.Obsolete) {
                type = cache[url] = DocsIndex.PageType.Obsolete;
                return type;
            }

            if (cache.TryGetValue(url, out type)) {
                return type;
            } else {
                Debug.LogError($"Could not determine member type after parsing parent: {pagePath} ({parentUrl} = {parentType})");
                type = cache[url] = DocsIndex.PageType.Unknown;
                return type;
            }
        }

        // Determine and cache types of members
        var lines = pageContents.Split('\n');
        var currentMemberType = DocsIndex.PageType.Unknown;
        foreach (var line in lines) {
            var matches = MemberTypeRegex.Matches(line);
            if (matches.Count > 0) {
                var lastMatch = matches[matches.Count - 1];
                if (memberIdentifiers.TryGetValue(lastMatch.Groups[1].Value, out var memberType)) {
                    currentMemberType = memberType;
                } else {
                    Debug.LogError($"Unknown member type: {lastMatch.Groups[1].Value} (in {url})");
                }
            }

            var match = MemberRegex.Match(line);
            if (match.Success) {
                if (currentMemberType == DocsIndex.PageType.Unknown) {
                    Debug.LogWarning($"Current member type is Unknown while processing member ({match.Groups[2].Value} on {url})");
                }
                cache[match.Groups[1].Value] = currentMemberType;
            }
        }

        return pageType;
    }
}

}
