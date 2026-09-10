#:sdk Microsoft.NET.Sdk.Razor
#:property RootNamespace=
#:package Aprillz.MewUI.MacOS@0.21.1
#:project ../../src/Naratteu.MewUI.Razor/Naratteu.MewUI.Razor.csproj

using Aprillz.MewUI;
using Naratteu.MewUI.Razor;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();
