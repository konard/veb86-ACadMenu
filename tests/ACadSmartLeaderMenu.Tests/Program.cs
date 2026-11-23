using ACadSmartLeaderMenu;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

var plugin = new SmartLeaderMenu();
var docA = new Document();
var docB = new Document();
var groups = Application.AcadApplication.MenuGroups.Groups;
var main = new MenuGroupDouble("ACAD", 0);
var unrelated = new MenuGroupDouble("BOBROVMENU", 1);
var similarName = new MenuGroupDouble("ACADSMARTLEADEREXTRA", 1);
var legacy = new MenuGroupDouble("acadsmartleader", 1) { FailUnload = true };
groups.AddRange([main, unrelated, similarName, legacy]);

plugin.Initialize();
AcApplication.RaiseIdle();
Require(Application.Menus.Count == 0, "Startup waits for an active drawing.");

AcApplication.DocumentManager.Activate(docA);
docA.Editor.IsQuiescent = false;
AcApplication.RaiseIdle();
Require(Application.Menus.Count == 0, "Startup waits for an idle editor.");
docA.Editor.IsQuiescent = true;
AcApplication.RaiseIdle();
Require(docA.Editor.Messages.Any(message => message.Contains("legacy unload failed")), "Load failures explain the cause.");
Require(Application.Menus.Count == 0, "A failed legacy migration leaves the new menu unregistered.");

legacy.FailUnload = false;
SmartLeaderMenu.LoadMenu();
SmartLeaderMenu.LoadMenu();
AcApplication.RaiseIdle();
Require(groups.SequenceEqual([main, unrelated, similarName]), "Migration removes just the 0.1.3 partial group.");

var contextMenu = Application.Menus.Single();
var contextItem = contextMenu.MenuItems.Single();
Require(contextItem.Text == "Обновить умные выноски", "Shortcut item label.");

RibbonServices.CreatePalette();
var palette = RibbonServices.RibbonPaletteSet!;
var otherTab = new RibbonTab { Id = "AnotherPlugin", Title = "Another plugin" };
palette.RibbonControl.Tabs.Add(otherTab);
AcApplication.RaiseIdle();
var buttons = MenuButtons(palette);
var actions = new (string Caption, string Command)[]
{
    ("Создать", "_SL_CREATE "),
    ("Создать групповую", "_SL_CREATE_GROUP "),
    ("Обновить", "_SL_UPDATE "),
    ("Правила", "_SL_RULES "),
};
Require(buttons.Select(button => button.Text).SequenceEqual(actions.Select(action => action.Caption)),
    "The ribbon displays all four buttons in order.");
Require(buttons.Select(button => button.Id).Distinct().Count() == buttons.Count, "Button IDs are unique.");

var tagged = new ObjectId(new MLeader("BobrovLeader"));
var untagged = new ObjectId(new MLeader());
var otherRegApp = new ObjectId(new MLeader("OtherRegApp"));
var nonLeader = new ObjectId(new DBObject());
var erased = new ObjectId(new MLeader("BobrovLeader"), IsErased: true);
foreach (var document in new[] { docA, docB })
{
    AcApplication.DocumentManager.Activate(document);
    AcApplication.RaiseIdle();
    document.Editor.PickFirst = [tagged];
    foreach (var button in buttons)
        button.CommandHandler.Execute(null);
    Require(document.Commands.SequenceEqual(actions.Select(action => action.Command)),
        "Buttons use only the global command and dispatch to the drawing current at click time.");
    Require(document.Editor.PickFirst.SequenceEqual([tagged]), "Ribbon commands preserve PickFirst.");
}

docB.Editor.PickFirst = [tagged];
contextMenu.Open();
Require(contextItem.Visible, "A tagged MLeader displays the shortcut item.");
contextItem.Invoke();
Require(docB.Commands.Count == 5 && docB.Commands[^1] == "_SL_UPDATE ", "The shortcut runs the ordinary update prompt.");
Require(docB.Editor.PickFirst.SequenceEqual([tagged]), "Opening and using the shortcut preserves PickFirst.");

docB.CommandInProgress = "LINE";
buttons[2].CommandHandler.Execute(null);
contextItem.Invoke();
docB.CommandInProgress = "";
AcApplication.CommandActive = 4;
buttons[0].CommandHandler.Execute(null);
AcApplication.CommandActive = 0;
Require(docB.Commands.Count == 5 && docB.Editor.Messages.Count == 3, "Busy commands are rejected with a message.");
Require(docB.Editor.PickFirst.SequenceEqual([tagged]), "Busy refusals leave the selection intact.");

foreach (var selection in new ObjectId[][] { [], [untagged], [otherRegApp], [nonLeader], [erased] })
{
    docB.Editor.PickFirst = selection;
    contextMenu.Open();
    Require(!contextItem.Visible, "An empty selection or selection without a live BobrovLeader MLeader hides the item.");
}

palette.LoadWorkspace(otherTab);
AcApplication.RaiseIdle();
Require(MenuButtons(palette).Count == 4 && palette.RibbonControl.Tabs.Count == 2, "A workspace rebuild restores one tab.");
Require(Application.Menus.Single() == contextMenu, "Workspace and document changes reuse the shortcut registration.");

RibbonServices.CreatePalette();
AcApplication.RaiseIdle();
Require(palette.RibbonControl.Tabs.SequenceEqual([otherTab]), "Replacing the palette leaves other tabs in place.");
palette = RibbonServices.RibbonPaletteSet!;
palette.RibbonControl.Tabs.Add(otherTab);
Require(MenuButtons(palette).Count == 4, "Replacing the palette restores the menu.");

SmartLeaderMenu.UnloadMenu();
SmartLeaderMenu.UnloadMenu();
Require(Application.Menus.Count == 0 && contextMenu.Disposed, "Unload removes and disposes the shortcut registration.");
Require(palette.RibbonControl.Tabs.SequenceEqual([otherTab]), "Unload removes just the owned tab.");
palette.LoadWorkspace(otherTab);
AcApplication.DocumentManager.Activate(docA);
AcApplication.RaiseIdle();
Require(Application.Menus.Count == 0 && palette.RibbonControl.Tabs.Count == 1, "Unloaded menu stays unloaded across events.");

SmartLeaderMenu.LoadMenu();
AcApplication.RaiseIdle();
Require(MenuButtons(palette).Count == 4 && Application.Menus.Single().MenuItems.Count == 1, "Reload creates one of each menu.");
plugin.Terminate();
Require(Application.Menus.Count == 0 && palette.RibbonControl.Tabs.SequenceEqual([otherTab]), "Termination removes both registrations.");

Console.WriteLine("Passed SmartLeader menu checks for dispatch, PickFirst, busy commands, legacy migration, and registration lifecycle.");

static List<RibbonButton> MenuButtons(RibbonPaletteSet palette)
{
    var tab = palette.RibbonControl.Tabs.Single(tab => tab.Title == "ACadSmartLeader");
    var panel = tab.Panels.Single().Source;
    Require(panel.Title == "Умные выноски", "Panel title.");
    return panel.Items;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
