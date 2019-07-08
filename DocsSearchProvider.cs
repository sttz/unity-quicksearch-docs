using System;
using System.Collections.Generic;
using System.Linq;
using Unity.QuickSearch;
using UnityEditor;
using UnityEngine;

namespace ch.sttz.quicksearch.docs
{

/// <summary>
/// Search Unity Docs directly in Quick Search.
/// </summary>
public static class DocsSearchProvider
{
    // ---------- Provider ----------

    /// <summary>
    /// Package name, needs to be same as in `package.json`.
    /// </summary>
    const string packageName = "ch.sttz.quicksearch.docs";
    /// <summary>
    /// Path prefix to package assets.
    /// </summary>
    static string packageFolderName = $"Packages/{packageName}";

    [SearchItemProvider]
    public static SearchProvider CreateProvider()
    {
        return new SearchProvider("ch.sttz.quicksearch-docs", "Docs") {
            filterId = "docs:",
            fetchItems = (context, items, provider) => {
                var tokens = context.tokenizedSearchQueryLower;
                var results = GetSearchResults(tokens, context.searchQuery);
                foreach (var result in results) {
                    items.Add(provider.CreateItem(
                        "ch.sttz.quicksearch-docs." + result.url,
                        -result.score,
                        result.title,
                        result.description,
                        Icons[result.type],
                        result
                    ));
                }
            }
        };
    }

    [SearchActionsProvider]
    static IEnumerable<SearchAction> ActionHandlers()
    {
        return new[]
        {
            new SearchAction("ch.sttz.quicksearch-docs", "open", null, "Open In Browser...")
            {
                handler = (item, context) =>
                {
                    var result = (DocsIndex.Page)item.data;
                    System.Diagnostics.Process.Start(new Uri($"{BASE_URL}{result.url}.html").AbsoluteUri);
                }
            },
        };
    }

    // ---------- Search Algorithm ----------

    /// <summary>
    /// Base URL of Unity's online documentation.
    /// </summary>
    const string BASE_URL = "https://docs.unity3d.com/ScriptReference/";

    /// <summary>
    /// The currently loaded search index.
    /// </summary>
    static DocsIndex searchIndex;

    /// <summary>
    /// Helper to add or increase hits of a result.
    /// </summary>
    static void AddResults(int[] pageIndices, Dictionary<int, int> pages) {
        foreach (var pageIndex in pageIndices) {
            pages.TryGetValue(pageIndex, out var count);
            pages[pageIndex] = count + 1;
        }
    }

    /// <summary>
    /// Search the documentation.
    /// </summary>
    /// <param name="tokens">Tokenized search query</param>
    /// <param name="query">Original search query</param>
    /// <returns>Pages matching the query</returns>
    public static IEnumerable<DocsIndex.Page> GetSearchResults(string[] tokens, string query) {
        if (searchIndex == null) {
            LoadIndex();
            if (searchIndex == null) {
                return Enumerable.Empty<DocsIndex.Page>();
            }
        }

        // First use a binary prefix search to find the index keys and gather initial page matches
        var keys = searchIndex.indexKeys;
        var indexLength = keys.Length;

        var pages = new Dictionary<int, int>(50);
        var minScore = tokens.Length;
        foreach (var token in tokens) {
            if (searchIndex.common.Contains(token)) {
                minScore--;
                continue;
            }

            var doPrefixSerach = (token.Length >= 3);
            var lo = 0;
            var hi = indexLength - 1;
            while (lo <= hi) {
                var i = lo + ((hi - lo) >> 1);
                var key = keys[i];
                var c = string.Compare(key, token);
                if (c == 0 || (doPrefixSerach && key.StartsWith(token))) {
                    AddResults(searchIndex.indexValues[i].pages, pages);
                    if (doPrefixSerach) {
                        for (var j = i - 1; j >= 0 && keys[j].StartsWith(token); j--) {
                            AddResults(searchIndex.indexValues[j].pages, pages);
                        }
                        for (var j = i + 1; j < indexLength && keys[j].StartsWith(token); j++) {
                            AddResults(searchIndex.indexValues[j].pages, pages);
                        }
                    }
                    break;
                } else if (c < 0) {
                    lo = i + 1;
                } else {
                    hi = i - 1;
                }
            }
        }

        // Process page matches and calculate result score
        var results = new List<DocsIndex.Page>(pages.Count);
        foreach (var pair in pages) {
            var score = pair.Value;

            if (score < minScore) {
                continue;
            }

            var result = searchIndex.pages[pair.Key];
            var title = result.title;
            var desc = result.description;

            int placement;
            foreach (var token in tokens) {
                placement = title.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (placement >= 0) {
                    score += 50;
                    if (placement == 0 || title[placement - 1] == '.')
                        score += 500;
                    if (placement + token.Length == title.Length 
                            || title[placement + token.Length] == '.')
                        score += 500;
                    goto saveResult;
                }
                
                placement = desc.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (placement >= 0) {
                    score += Math.Max(20 - placement, 10);
                }
            }

            placement = title.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (placement == 0 && query.Length == title.Length) {
                score += 10000;
            } else if (placement > 0) {
                score += Math.Max(200 - placement, 100);
            } else {
                placement = desc.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (placement >= 0) {
                    score += Math.Max(50 - placement, 25);
                }
            }

        saveResult:
            if (result.type == DocsIndex.PageType.Obsolete) {
                score -= 100000;
            }
            result.score = score;
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Try to find the search index.
    /// </summary>
    static void LoadIndex()
    {
        var guids = AssetDatabase.FindAssets("t:DocsIndex");
        if (guids.Length == 0) {
            Debug.LogError("No DocsIndex could be found.");
            return;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        searchIndex = AssetDatabase.LoadAssetAtPath<DocsIndex>(path);
    }

    // ---------- Icons ----------

    /// <summary>
    /// Path prefix to icon assets in package.
    /// </summary>
    /// <value></value>
    static string iconFolder = $"{packageFolderName}/Icons";

    /// <summary>
    /// Search result icons by documentation page type.
    /// </summary>
    static readonly Dictionary<DocsIndex.PageType, Texture2D> Icons = new Dictionary<DocsIndex.PageType, Texture2D> {
        [DocsIndex.PageType.Class] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Class.png"),
        [DocsIndex.PageType.Delegate] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Delegate.png"),
        [DocsIndex.PageType.Enumeration] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Enumeration.png"),
        [DocsIndex.PageType.Enumerator] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Enumerator.png"),
        [DocsIndex.PageType.Event] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Event.png"),
        [DocsIndex.PageType.Interface] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Interface.png"),
        [DocsIndex.PageType.Message] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Message.png"),
        [DocsIndex.PageType.Method] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Method.png"),
        [DocsIndex.PageType.Module] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Module.png"),
        [DocsIndex.PageType.Obsolete] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Obsolete.png"),
        [DocsIndex.PageType.Property] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Property.png"),
        [DocsIndex.PageType.Struct] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Struct.png"),
        [DocsIndex.PageType.Unknown] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Unknown.png"),
    };
}

}
