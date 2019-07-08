using System;
using System.Collections.Generic;
using UnityEngine;

namespace ch.sttz.quicksearch.docs
{

/// <summary>
/// Generated index for a Unity documentation.
/// </summary>
public class DocsIndex : ScriptableObject
{
    /// <summary>
    /// The type of documentation page.
    /// </summary>
    public enum PageType {
        Unknown,
        
        Module,
        Class,
        Struct,
        Enumeration,
        Interface,

        Property,
        Method,
        Event,
        Delegate,
        Message,
        Enumerator,

        Obsolete,
    }

    /// <summary>
    /// Information about a single documentation page.
    /// </summary>
    [Serializable]
    public struct Page {
        /// <summary>
        /// The page's title.
        /// </summary>
        public string title;
        /// <summary>
        /// The page's short description.
        /// </summary>
        public string description;
        /// <summary>
        /// The page's relative URL.
        /// </summary>
        public string url;
        /// <summary>
        /// The page's type.
        /// </summary>
        public PageType type;

        /// <summary>
        /// Score field used by search provider.
        /// </summary>
        [NonSerialized] public int score;
    }

    /// <summary>
    /// All documentation pages.
    /// </summary>
    public Page[] pages;
    /// <summary>
    /// Common words which are ignored in the query.
    /// </summary>
    public string[] common;
    /// <summary>
    /// Unity version the documentation belongs to.
    /// </summary>
    public string unityVersion;
    /// <summary>
    /// Version of the documentation itself.
    /// </summary>
    public string docsVersion;

    /// <summary>
    /// Helper struct to enable Unity to serialize and array of arrays.
    /// </summary>
    [Serializable]
    public struct Entry {
        /// <summary>
        /// Indices into the <see cref="pages"/> array.
        /// </summary>
        public int[] pages;
    }

    /// <summary>
    /// Sorted array of index search terms.
    /// A search term's index matches the index in the <see cref="indexValues"/> array.
    /// </summary>
    public string[] indexKeys;
    /// <summary>
    /// Array containing the pages in which search terms appears.
    /// </summary>
    public Entry[] indexValues;
}

}
