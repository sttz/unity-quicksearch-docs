using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
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
    
    /// <summary>
    /// Folder where the bundled indices are stored.
    /// </summary>
    static string indicesFolder = $"{packageFolderName}/Indices~";

    /// <summary>
    /// Base URL of Unity's online documentation.
    /// </summary>
    const string docsBaseUrl = "https://docs.unity3d.com";
    /// <summary>
    /// Sub path script reference in Unity's online versioned documentation.
    /// </summary>
    const string docsSubPath = "Documentation/ScriptReference";
    /// <summary>
    /// Sub path to script reference in the local documentation.
    /// </summary>
    const string localDocsSubPath = "en/ScriptReference";

    [UsedImplicitly, SearchItemProvider]
    public static SearchProvider CreateProvider()
    {
        return new SearchProvider("ch.sttz.quicksearch-docs", "Docs") {
            filterId = "docs:",
            fetchItems = (context, items, provider) => {
                var results = GetSearchResults(context.searchWords, context.searchQuery)
                    .Select(result => 
                        provider.CreateItem(
                            "ch.sttz.quicksearch-docs.result." + result.url, 
                            -result.score, 
                            result.title, 
                            result.description, 
                            Icons[result.type], 
                            result
                        )
                    );

                if (searchIndex != null) {
                    results = results.Append(
                        provider.CreateItem(
                            "ch.sttz.quicksearch-docs.index", 
                            10000, 
                            "Quick Search Docs Index", 
                            $"Index ({searchIndex.docsVersion}) for Unity {searchIndex.unityVersion} at {searchIndexPath}", 
                            null, 
                            searchIndexPath
                        )
                    );
                }

                return results;
            }
        };
    }

    [UsedImplicitly, SearchActionsProvider]
    static IEnumerable<SearchAction> ActionHandlers()
    {
        return new[] {
            new SearchAction("ch.sttz.quicksearch-docs", "open", null, "Open in Browser...") {
                handler = (item, context) => {
                    if (item.data is DocsIndex.Page result) {
                        string url;
                        if (localDocsPath != null) {
                            url = $"file://{localDocsPath}/{localDocsSubPath}/{result.url}.html";
                        } else {
                            url = $"{docsBaseUrl}/{searchIndex.unityVersion}/{docsSubPath}/{result.url}.html";
                        }
                        System.Diagnostics.Process.Start(new Uri(url).AbsoluteUri);
                    } else if (item.data is string path) {
                        EditorUtility.RevealInFinder(path);
                    }
                }
            },
        };
    }

    // ---------- Search Algorithm ----------

    /// <summary>
    /// The currently loaded search index.
    /// </summary>
    static DocsIndex searchIndex;
    /// <summary>
    /// The path the current index was loaded from.
    /// </summary>
    static string searchIndexPath;
    /// <summary>
    /// Path to the locally installed documentation, if it exists.
    /// </summary>
    static string localDocsPath;

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
                yield break;
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

            var doPrefixSearch = (token.Length >= 3);
            var lo = 0;
            var hi = indexLength - 1;
            while (lo <= hi) {
                var i = lo + ((hi - lo) >> 1);
                var key = keys[i];
                var c = String.CompareOrdinal(key, token);
                if (c == 0 || (doPrefixSearch && key.StartsWith(token))) {
                    AddResults(searchIndex.indexValues[i].pages, pages);
                    if (doPrefixSearch) {
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
            yield return result;
        }
    }

    /// <summary>
    /// Try to find the search index.
    /// </summary>
    static void LoadIndex()
    {
        // First try override from project directory
        var projectPath = Path.Combine(Application.dataPath, "..");
        if (LoadIndex(projectPath)) {
            return;
        }

        // Next try override from Unity installation folder
        var unityPath = Path.GetDirectoryName(EditorApplication.applicationPath);
        if (LoadIndex(unityPath)) {
            return;
        }

        // Then try local data folder
        string dataPath;
        #if UNITY_EDITOR_OSX
        dataPath = "/Users/Shared";
        #else
        dataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        #endif
        dataPath = Path.Combine(dataPath, packageName);
        if (LoadIndex(dataPath)) {
            return;
        }

        // Finally use built-in indices
        if (LoadIndex(indicesFolder)) {
            return;
        }

        Debug.LogError("Could not find an index for Quick Search Docs.");
    }

    static DocsIndex.MajorMinorVersion unityVersion;

    /// <summary>
    /// Load the best available index from the given path.
    /// </summary>
    static bool LoadIndex(string path)
    {
        if (!Directory.Exists(path)) return false;

        if (unityVersion.major == 0) {
            unityVersion = DocsIndex.MajorMinorVersion.FromUnityVersion(Application.unityVersion);
        }

        var bestVersion = new DocsIndex.MajorMinorVersion(0, 0);
        string bestIndex = null;
        var indices = Directory.GetFiles(path, "DocsIndex-*.json");
        foreach (var index in indices) {
            var indexVersion = DocsIndex.UnityVersionFromFileName(Path.GetFileName(index));
            if (indexVersion.major == 0) continue;
            
            // Always use exact version match
            if (indexVersion == unityVersion) {
                bestVersion = indexVersion;
                bestIndex = index;
                break;
            }

            // For indices of older Unity versions, prefer the newest
            if (indexVersion < unityVersion) {
                if (indexVersion > bestVersion) {
                    bestVersion = indexVersion;
                    bestIndex = index;
                }
            
            // For indices of newer Unity versions, prefer the oldest
            // But always prefer indices of newer Unity versions
            } else {
                if (bestVersion < unityVersion || indexVersion < bestVersion) {
                    bestVersion = indexVersion;
                    bestIndex = index;
                }
            }
        }

        if (bestIndex == null) {
            return false;
        }

        var json = File.ReadAllText(bestIndex);
        searchIndex = JsonUtility.FromJson<DocsIndex>(json);
        searchIndexPath = bestIndex;
        localDocsPath = null;

        // Check for location documentation
        var unityPath = Path.GetDirectoryName(EditorApplication.applicationPath);
        var docsPath = Path.Combine(unityPath, "Documentation");
        if (Directory.Exists(docsPath)) {
            localDocsPath = docsPath;
        }

        return true;
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
        [DocsIndex.PageType.Class]       = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Class.png"),
        [DocsIndex.PageType.Delegate]    = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Delegate.png"),
        [DocsIndex.PageType.Enumeration] = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Enumeration.png"),
        [DocsIndex.PageType.Enumerator]  = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Enumerator.png"),
        [DocsIndex.PageType.Event]       = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Event.png"),
        [DocsIndex.PageType.Interface]   = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Interface.png"),
        [DocsIndex.PageType.Message]     = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Message.png"),
        [DocsIndex.PageType.Method]      = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Method.png"),
        [DocsIndex.PageType.Module]      = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Module.png"),
        [DocsIndex.PageType.Obsolete]    = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Obsolete.png"),
        [DocsIndex.PageType.Property]    = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Property.png"),
        [DocsIndex.PageType.Struct]      = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Struct.png"),
        [DocsIndex.PageType.Unknown]     = (Texture2D)EditorGUIUtility.Load($"{iconFolder}/Unknown.png"),
    };
}

}
