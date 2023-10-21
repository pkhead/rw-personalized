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

    DotNetBuild("./plugin/personalizer.sln", new DotNetBuildSettings
    {
        Configuration = configuration
    });

    // create output folder
    EnsureDirectoryExists("./out");
    CleanDirectory("./out");
    CopyDirectory("./assets", "./out");
    CreateDirectory("./out/plugins");
    CopyFile($"./plugin/bin/{configuration}/net48/personalizer.dll", "./out/plugins/Personalizer.dll");
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