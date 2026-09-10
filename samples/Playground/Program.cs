using Aprillz.MewUI;
using Naratteu.MewUI.Razor;
using Playground;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();
