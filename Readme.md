# Quick Search Docs

This is a search provider for [Unity Quick Search](https://docs.unity3d.com/Packages/com.unity.quicksearch@latest) that makes the Unity documentation directly searchable in Quick Search.

## Installation

Quick Search Docs requires at least Unity 2019.3.

Add this Git repository as a package dependency to your project. You can add it using the Package Manager window or add it directly to `Packages/manifest.json`:

    "ch.sttz.quicksearch.docs": "https://github.com/sttz/unity-quicksearch-docs.git"

## Usage

After installation, documentation results should immediately appear in the Quick Search window. Select a result and press enter or double click it to open it in your default browser.

Quick Search Docs bundles the indexes for the latest major Unity releases (2019.3 and 2020.1).

When the documentation has been installed together with Unity, Quick Search Docs will open its results in the offline documentation. Otherwise, the online documentation will be opened.

## Creating or Updating an Index

An index can be created from the offline Unity documentation. Use the `Window » Quick Search Docs » Generate Index...` menu, select the offline documentation folder and then where to save the index.

*Important:* Do not change the file names of the indices.

When looking for an index, Quick Search Docs checks the following locations in order:
* Unity project root
* Unity installation root
* `/Users/Shared/ch.sttz.quicksearch.docs` on macOS or `C:\ProgramData\ch.sttz.quicksearch.docs` on Windows

Each of those locations can contain multiple indices, in which case the one most suitable for the current Unity version is picked (an index matching the current Unity version, the oldest index from all newer versions or the newest index from all older versions).

To see which index is being used, enter «docsindex» in quick search and a «Quick Search Docs Index» item should appear in the result with the index location.
