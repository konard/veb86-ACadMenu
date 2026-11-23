#:property PublishAot=false

// Issue #3. Requires .NET 10 and AcCui.dll from AutoCAD 2020–2024.
// Runs the same menu item reader and display-name accessor on the shipped CUIX.
using System.IO.Compression;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run experiments/Inspect-BobrovMenuReader.cs -- AcCui.dll BobrovMenu.cuix [...]");
    return 2;
}

var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var readerType = assembly.GetType("Autodesk.AutoCAD.Customization.CuiSerializationReader", throwOnError: true)!;
var sectionType = assembly.GetType("Autodesk.AutoCAD.Customization.CustomizationSection", throwOnError: true)!;
const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
var init = typeof(XmlSerializationReader).GetMethod("Init", instance)!;
var readMenuItem = readerType.GetMethod("Read46_PopMenuItem", instance)!;
// CustomizationSection sets this flag while it loads a CUI file.
sectionType.GetProperty("IsParsing", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, true);

bool valid = true;
foreach (var path in args.Skip(1))
{
    using var cuix = ZipFile.OpenRead(path);
    using var menu = cuix.GetEntry("PopMenuRoot.cui")!.Open();
    using var xml = XmlReader.Create(menu);
    if (!xml.ReadToFollowing("PopMenuItem"))
    {
        Console.Error.WriteLine($"{path}: missing PopMenuItem");
        valid = false;
        continue;
    }

    var reader = Activator.CreateInstance(readerType, nonPublic: true)!;
    init.Invoke(reader, [xml, new XmlDeserializationEvents(), null]);
    var item = readMenuItem.Invoke(reader, [false, true])!;
    var itemType = item.GetType();
    var macro = (string?)itemType.GetProperty("MacroID")!.GetValue(item);
    string? label = null;
    try
    {
        label = (string?)itemType.GetProperty("DisplayName")!.GetValue(item);
        Console.WriteLine($"{path}: label = {label}, macro = {macro}");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is NullReferenceException)
    {
        Console.Error.WriteLine($"{path}: DisplayName threw NullReferenceException, macro = {macro}");
    }
    valid &= label == "Палитра XDATA" && macro == "ID_BOBROV_XDATAPALETTE";
}
return valid ? 0 : 1;
