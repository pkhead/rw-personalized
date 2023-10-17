var target = Argument("Target", "Install");
var configuration = Argument("configuration", "Debug");
var rainWorldDir = EnvironmentVariable<string>("RainWorldDir", "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rain World");

// Tasks
Task("Build")
    .Does(() =>
{
    if (!HasEnvironmentVariable("RainWorldDir") || rainWorldDir == "")
    {
        throw new Exception("The environment variable 'RainWorldDir' is not provided");
    }

    DotNetBuild("./plugin/gaming.sln", new DotNetBuildSettings
    {
        Configuration = configuration
    });

    // create output folder
    EnsureDirectoryExists("./out");
    CleanDirectory("./out");
    CopyDirectory("./assets", "./out");
    CreateDirectory("./out/plugins");
    CopyFile($"./plugin/gaming/bin/{configuration}/net48/gaming.dll", "./out/plugins/mod.dll");
});

Task("Install")
    .IsDependentOn("Build")
    .Does(() =>
{
    var modOutputDir = rainWorldDir + "/RainWorld_Data/StreamingAssets/mods/pkhead.personalizer";

    EnsureDirectoryExists(modOutputDir);
    CleanDirectory(modOutputDir);
    CopyDirectory("./out", modOutputDir);
});

// execution
RunTarget(target);