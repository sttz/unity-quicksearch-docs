using System;
using System.Collections.Generic;
using UnityEngine;

namespace ch.sttz.quicksearch.docs
{

[Serializable]
public class DocsIndex
{
    /// <summary>
    /// Unity Major.Minor version.
    /// </summary>
    [Serializable]
    public struct MajorMinorVersion : IEquatable<MajorMinorVersion>
    {
        public int major;
        public int minor;

        public static bool TryParse(string input, out MajorMinorVersion version)
        {
            version = default;
            var parts = input.Split('.');
            if (parts.Length != 2 
                    || !int.TryParse(parts[0], out version.major) 
                    || !int.TryParse(parts[1], out version.minor)) {
                return false;
            }
            return true;
        }

        public static MajorMinorVersion FromUnityVersion(string unityVersion)
        {
            MajorMinorVersion version = default;
            var parts = unityVersion.Split('.');
            if (parts.Length != 3
                    || !int.TryParse(parts[0], out version.major) 
                    || !int.TryParse(parts[1], out version.minor)) {
                throw new Exception("Unexpected Unity version: " + unityVersion);
            }
            return version;
        }

        public MajorMinorVersion(int major, int minor)
        {
            this.major = major;
            this.minor = minor;
        }

        public override string ToString()
        {
            return $"{major}.{minor}";
        }

        public override int GetHashCode()
        {
            return major.GetHashCode() ^ minor.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is MajorMinorVersion other) {
                return this == other;
            }
            return false;
        }

        public bool Equals(MajorMinorVersion other)
        {
            return this == other;
        }

        public static bool operator ==(MajorMinorVersion lhs, MajorMinorVersion rhs)
        {
            return (lhs.major == rhs.major && lhs.minor == rhs.minor);
        }

        public static bool operator !=(MajorMinorVersion lhs, MajorMinorVersion rhs)
        {
            return (lhs.major != rhs.major || lhs.minor != rhs.minor);
        }

        public static bool operator <(MajorMinorVersion lhs, MajorMinorVersion rhs)
        {
            if (lhs.major < rhs.major) return true;
            if (lhs.major > rhs.major) return false;
            return (lhs.minor < rhs.minor);
        }

        public static bool operator >(MajorMinorVersion lhs, MajorMinorVersion rhs)
        {
            if (lhs.major > rhs.major) return true;
            if (lhs.major < rhs.major) return false;
            return (lhs.minor > rhs.minor);
        }
    }

    /// <summary>
    /// Parse the Unity Major.Minor version from a standard DocsIndex file name.
    /// </summary>
    /// <remarks>
    /// The standard format is: "DocsIndex-Major.Minor-DocsVersion.json"<br/>
    /// E.g. "DocsIndex-2019.2-003A.json"
    /// </remarks>
    public static MajorMinorVersion UnityVersionFromFileName(string name)
    {
        var parts = name.Split('-');
        if (parts.Length != 3 && parts.Length != 5) return default;
        if (!MajorMinorVersion.TryParse(parts[1], out var version)) return default;
        return version;
    }

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
    public MajorMinorVersion unityVersion;
    /// <summary>
    /// Version of the documentation itself.
    /// </summary>
    public string docsVersion;
    /// <summary>
    /// Publication date YYYY-MM-DD string, replaced <see cref="docsVersion"/>.
    /// </summary>
    public string publicationDate;

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
