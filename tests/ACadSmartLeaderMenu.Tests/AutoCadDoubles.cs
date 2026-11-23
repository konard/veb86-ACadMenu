using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;

namespace System.Windows.Controls
{
    public enum Orientation { Vertical }
}

namespace Autodesk.AutoCAD.Runtime
{
    public interface IExtensionApplication { void Initialize(); void Terminate(); }
    public sealed class ExtensionApplicationAttribute(Type type) : Attribute { public Type Type => type; }
    public sealed class CommandClassAttribute(Type type) : Attribute { public Type Type => type; }
    public sealed class CommandMethodAttribute(string name, CommandFlags flags) : Attribute
    {
        public string Name => name;
        public CommandFlags Flags => flags;
    }
    public enum CommandFlags { Session }
    public static class RXObject { public static Type GetClass(Type type) => type; }
}

namespace Autodesk.AutoCAD.ApplicationServices.Core
{
    public static class Application
    {
        public static DocumentCollection DocumentManager { get; } = new();
        public static bool IsQuiescent { get; set; } = true;
        public static short CommandActive { get; set; }
        public static short GetSystemVariable(string name) =>
            name == "CMDACTIVE" ? CommandActive : throw new ArgumentException(name);
        public static event EventHandler? Idle;
        public static void RaiseIdle() => Idle?.Invoke(null, EventArgs.Empty);
    }
}

namespace Autodesk.AutoCAD.ApplicationServices
{
    public static class Application
    {
        public static AcadApplicationDouble AcadApplication { get; } = new();
        public static List<ContextMenuExtension> Menus { get; } = new();
        public static void AddObjectContextMenuExtension(Type type, ContextMenuExtension menu)
        {
            if (type != typeof(MLeader)) throw new ArgumentException(nameof(type));
            Menus.Add(menu);
        }
        public static void RemoveObjectContextMenuExtension(Type type, ContextMenuExtension menu) => Menus.Remove(menu);
    }

    public sealed class AcadApplicationDouble { public MenuGroupsDouble MenuGroups { get; } = new(); }
    public sealed class MenuGroupsDouble
    {
        public List<MenuGroupDouble> Groups { get; } = new();
        public int Count => Groups.Count;
        public MenuGroupDouble Item(int index) => Groups[index];
    }
    public sealed class MenuGroupDouble(string name, int type)
    {
        public string Name => name;
        public int Type => type;
        public bool FailUnload { get; set; }
        public void Unload()
        {
            if (FailUnload) throw new InvalidOperationException("legacy unload failed");
            Application.AcadApplication.MenuGroups.Groups.Remove(this);
        }
    }

    public sealed class DocumentCollectionEventArgs : EventArgs { }
    public sealed class DocumentCollection
    {
        public Document? MdiActiveDocument { get; private set; }
        public event EventHandler<DocumentCollectionEventArgs>? DocumentBecameCurrent;
        public void Activate(Document? document)
        {
            MdiActiveDocument = document;
            DocumentBecameCurrent?.Invoke(this, new DocumentCollectionEventArgs());
        }
    }
    public sealed class Document
    {
        public string CommandInProgress { get; set; } = "";
        public Editor Editor { get; } = new();
        public Database Database { get; } = new();
        public List<string> Commands { get; } = new();
        public void SendStringToExecute(string command, bool activate, bool wrapUpInactiveDoc, bool echoCommand)
        {
            if (wrapUpInactiveDoc) throw new InvalidOperationException("Inactive drawings must be left alone.");
            Commands.Add(command);
        }
    }
}

namespace Autodesk.AutoCAD.DatabaseServices
{
    public sealed class Database { public TransactionManager TransactionManager { get; } = new(); }
    public sealed class TransactionManager { public Transaction StartOpenCloseTransaction() => new(); }
    public enum OpenMode { ForRead, ForWrite }
    public readonly record struct ObjectId(DBObject Object, bool IsValid = true, bool IsErased = false);
    public sealed class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode) =>
            mode == OpenMode.ForRead ? id.Object : throw new InvalidOperationException("Selection is read-only.");
        public void Dispose() { }
    }
    public class DBObject { }
    public sealed class MLeader(string? regApp = null) : DBObject
    {
        public ResultBuffer? GetXDataForApplication(string name) => name == regApp ? new() : null;
    }
    public sealed class ResultBuffer : IDisposable { public void Dispose() { } }
}

namespace Autodesk.AutoCAD.EditorInput
{
    public enum PromptStatus { OK, Error }
    public sealed class SelectionSet(ObjectId[] ids) : IDisposable
    {
        public ObjectId[] GetObjectIds() => ids;
        public void Dispose() { }
    }
    public sealed record PromptSelectionResult(PromptStatus Status, SelectionSet Value);
    public sealed class Editor
    {
        public ObjectId[] PickFirst { get; set; } = [];
        public bool IsQuiescent { get; set; } = true;
        public List<string> Messages { get; } = new();
        public void WriteMessage(string message) => Messages.Add(message);
        public PromptSelectionResult SelectImplied() =>
            new(PickFirst.Length == 0 ? PromptStatus.Error : PromptStatus.OK, new(PickFirst));
    }
}

namespace Autodesk.AutoCAD.Windows
{
    public sealed class ContextMenuExtension : IDisposable
    {
        public event EventHandler? Popup;
        public List<MenuItem> MenuItems { get; } = new();
        public void Open() => Popup?.Invoke(this, EventArgs.Empty);
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    public sealed class MenuItem(string text)
    {
        public string Text => text;
        public bool Visible { get; set; } = true;
        public event EventHandler? Click;
        public void Invoke() => Click?.Invoke(this, EventArgs.Empty);
    }
}

namespace Autodesk.AutoCAD.Ribbon
{
    public static class RibbonServices
    {
        public static RibbonPaletteSet? RibbonPaletteSet { get; private set; }
        public static event EventHandler? RibbonPaletteSetCreated;
        public static void CreatePalette()
        {
            RibbonPaletteSet = new();
            RibbonPaletteSetCreated?.Invoke(null, EventArgs.Empty);
        }
    }
    public sealed class RibbonPaletteSet
    {
        public RibbonControl RibbonControl { get; } = new();
        public event EventHandler? WorkspaceLoaded;
        public void LoadWorkspace(params RibbonTab[] tabs)
        {
            RibbonControl.Tabs.Clear();
            RibbonControl.Tabs.AddRange(tabs);
            WorkspaceLoaded?.Invoke(this, EventArgs.Empty);
        }
    }
}

namespace Autodesk.Windows
{
    public sealed class RibbonControl { public List<RibbonTab> Tabs { get; } = new(); }
    public sealed class RibbonTab
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public List<RibbonPanel> Panels { get; } = new();
    }
    public sealed class RibbonPanel { public required RibbonPanelSource Source { get; init; } }
    public sealed class RibbonPanelSource
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public List<RibbonButton> Items { get; } = new();
    }
    public enum RibbonItemSize { Large }
    public sealed class RibbonButton
    {
        public string Id { get; init; } = "";
        public string Text { get; init; } = "";
        public bool ShowText { get; init; }
        public bool ShowImage { get; init; }
        public RibbonItemSize Size { get; init; }
        public System.Windows.Controls.Orientation Orientation { get; init; }
        public required ICommand CommandHandler { get; init; }
    }
}
