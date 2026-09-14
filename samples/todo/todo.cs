#:sdk Microsoft.NET.Sdk.Razor
#:property RootNamespace=
#:package Aprillz.MewUI.MacOS@0.21.1
#:project ../../src/Naratteu.MewUI.Razor/Naratteu.MewUI.Razor.csproj

using Aprillz.MewUI;
using Naratteu.MewUI.Razor;

// Swap the two platform lines for your OS -- see the table in the README.
Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();

/// <summary>One item. Immutable, so a change is a new object and the diff can see it.</summary>
public sealed record Item(int Id, string Text, bool Done);
