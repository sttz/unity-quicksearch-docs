# Quick Search Docs

This is a search provider for [Unity Quick Search](https://docs.unity3d.com/Packages/com.unity.quicksearch@latest) that makes the Unity documentation directly searchable in Quick Search.

This is an early version that uses a local index but can only open results in the online Unity documentation. It also searches only the latest online documentation.

## Installation

Quick Search Docs requires at least Unity 2018.4.

Add this Git repository as a package dependency to your project. In Unity 2019.3+ you can add it using the Package Manager window, in previous version, add it directly to `Packages/manifest.json`:

    "ch.sttz.quicksearch.docs": "https://github.com/sttz/unity-quicksearch-docs.git"

## Usage

After installation, documentation results should immediately appear in the Quick Search window. Select a result and press enter or double click it to open it in your default browser.
