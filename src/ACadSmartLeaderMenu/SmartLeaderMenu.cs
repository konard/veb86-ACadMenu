using System;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.Windows;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;
using Orientation = System.Windows.Controls.Orientation;

[assembly: ExtensionApplication(typeof(ACadSmartLeaderMenu.SmartLeaderMenu))]
[assembly: CommandClass(typeof(ACadSmartLeaderMenu.SmartLeaderMenu))]

namespace ACadSmartLeaderMenu;

public sealed class SmartLeaderMenu : IExtensionApplication
{
    private const string TabId = "BobrovMenu.ACadSmartLeader.Tab";
    private const string LegacyGroup = "ACADSMARTLEADER";
    private const int PartialMenuGroup = 1; // ActiveX AcMenuGroupType.acPartialMenuGroup
    private const string LeaderRegApp = "BobrovLeader";
    private const string Update = "_SL_UPDATE";
    private static SmartLeaderMenu? current;

    private bool enabled;
    private RibbonPaletteSet? palette;
    private RibbonControl? ribbon;
    private RibbonTab? tab;
    private ContextMenuExtension? context;
    private MenuItem? updateItem;

    public void Initialize()
    {
        if (current != null)
            return;

        current = this;
        Enable();
    }

    public void Terminate()
    {
        if (current != this)
            return;

        Disable();
        current = null;
    }

    [CommandMethod("SL_MENU_LOAD", CommandFlags.Session)]
    public static void LoadMenu() => current?.Enable();

    [CommandMethod("SL_MENU_UNLOAD", CommandFlags.Session)]
    public static void UnloadMenu() => current?.Disable();

    private static Document? ActiveDocument => AcApplication.DocumentManager.MdiActiveDocument;

    private void Enable()
    {
        if (!enabled)
        {
            enabled = true;
            AcApplication.DocumentManager.DocumentBecameCurrent += DocumentBecameCurrent;
            RibbonServices.RibbonPaletteSetCreated += PaletteCreated;
        }

        ScheduleRefresh();
    }

    private void Disable()
    {
        enabled = false;
        AcApplication.Idle -= RefreshOnIdle;
        AcApplication.DocumentManager.DocumentBecameCurrent -= DocumentBecameCurrent;
        RibbonServices.RibbonPaletteSetCreated -= PaletteCreated;
        ObservePalette(null);

        try { RemoveContextMenu(); }
        catch (Exception error) { ReportFailure("не удалось снять контекстное меню", error); }
        try { RemoveRibbonTab(); }
        catch (Exception error) { ReportFailure("не удалось снять вкладку ленты", error); }
    }

    private void DocumentBecameCurrent(object? sender, DocumentCollectionEventArgs e) => ScheduleRefresh();
    private void PaletteCreated(object? sender, EventArgs e) => ScheduleRefresh();
    private void WorkspaceLoaded(object? sender, EventArgs e) => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        if (!enabled)
            return;

        AcApplication.Idle -= RefreshOnIdle;
        AcApplication.Idle += RefreshOnIdle;
    }

    private void RefreshOnIdle(object? sender, EventArgs e)
    {
        try
        {
            var document = ActiveDocument;
            if (document == null)
            {
                // DocumentBecameCurrent will retry when the Start tab gives way to a drawing.
                AcApplication.Idle -= RefreshOnIdle;
                return;
            }

            if (!AcApplication.IsQuiescent || !document.Editor.IsQuiescent ||
                !string.IsNullOrEmpty(document.CommandInProgress))
                return;

            AcApplication.Idle -= RefreshOnIdle;
            ObservePalette(RibbonServices.RibbonPaletteSet);
            UnloadLegacyGroup();
            EnsureContextMenu();
            EnsureRibbonTab(palette?.RibbonControl);
        }
        catch (Exception error)
        {
            AcApplication.Idle -= RefreshOnIdle;
            ReportFailure("не удалось загрузить меню", error);
        }
    }

    private void ObservePalette(RibbonPaletteSet? next)
    {
        if (palette == next)
            return;

        if (palette != null)
            palette.WorkspaceLoaded -= WorkspaceLoaded;
        palette = next;
        if (palette != null)
            palette.WorkspaceLoaded += WorkspaceLoaded;
    }

    private static void UnloadLegacyGroup()
    {
        dynamic application = Application.AcadApplication;
        dynamic groups = application.MenuGroups;
        for (int index = 0; index < (int)groups.Count; index++)
        {
            dynamic group = groups.Item(index);
            if (string.Equals((string)group.Name, LegacyGroup, StringComparison.OrdinalIgnoreCase) &&
                (int)group.Type == PartialMenuGroup)
            {
                // The 0.1.3 partial CUIX can remain loaded in an upgraded profile.
                group.Unload();
                return;
            }
        }
    }

    private void EnsureRibbonTab(RibbonControl? next)
    {
        if (next != ribbon)
            RemoveRibbonTab();
        if (next == null || next.Tabs.Any(existing => existing.Id == TabId))
            return;

        var panel = new RibbonPanelSource { Id = "BobrovMenu.ACadSmartLeader.Panel", Title = "Умные выноски" };
        panel.Items.Add(Button("Создать", "_SL_CREATE"));
        panel.Items.Add(Button("Создать групповую", "_SL_CREATE_GROUP"));
        panel.Items.Add(Button("Обновить", Update));
        panel.Items.Add(Button("Правила", "_SL_RULES"));

        var added = new RibbonTab { Id = TabId, Title = "ACadSmartLeader" };
        added.Panels.Add(new RibbonPanel { Source = panel });
        next.Tabs.Add(added);
        ribbon = next;
        tab = added;
    }

    private static RibbonButton Button(string caption, string globalCommand) => new RibbonButton
    {
        Id = "BobrovMenu.ACadSmartLeader." + globalCommand,
        Text = caption,
        ShowText = true,
        ShowImage = false,
        Size = RibbonItemSize.Large,
        Orientation = Orientation.Vertical,
        CommandHandler = new RibbonCommand(globalCommand),
    };

    private void RemoveRibbonTab()
    {
        if (tab != null)
            ribbon!.Tabs.Remove(tab);
        tab = null;
        ribbon = null;
    }

    private void EnsureContextMenu()
    {
        if (context != null)
            return;

        var extension = new ContextMenuExtension();
        var item = new MenuItem("Обновить умные выноски") { Visible = false };
        item.Click += UpdateFromContext;
        extension.Popup += ContextPopup;
        extension.MenuItems.Add(item);

        try
        {
            Application.AddObjectContextMenuExtension(RXObject.GetClass(typeof(MLeader)), extension);
        }
        catch
        {
            extension.Dispose();
            throw;
        }

        context = extension;
        updateItem = item;
    }

    private void RemoveContextMenu()
    {
        if (context == null)
            return;

        updateItem!.Visible = false;
        Application.RemoveObjectContextMenuExtension(RXObject.GetClass(typeof(MLeader)), context);
        context.Popup -= ContextPopup;
        updateItem.Click -= UpdateFromContext;
        context.Dispose();
        updateItem = null;
        context = null;
    }

    private void ContextPopup(object? sender, EventArgs e)
    {
        updateItem!.Visible = false;
        var document = ActiveDocument;
        if (!enabled || document == null)
            return;

        try
        {
            // Read the PickFirst set without changing it. SL_UPDATE consumes the same set.
            var selection = document.Editor.SelectImplied();
            if (selection.Status != PromptStatus.OK)
                return;

            using var picked = selection.Value;
            using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
            foreach (var id in picked.GetObjectIds())
            {
                if (!id.IsValid || id.IsErased ||
                    transaction.GetObject(id, OpenMode.ForRead) is not MLeader leader)
                    continue;

                using var xdata = leader.GetXDataForApplication(LeaderRegApp);
                if (xdata == null)
                    continue;

                updateItem.Visible = true;
                break;
            }
        }
        catch (Exception error)
        {
            ReportFailure("не удалось проверить выбранные выноски", error);
        }
    }

    private void UpdateFromContext(object? sender, EventArgs e)
    {
        if (enabled && updateItem?.Visible == true)
            Dispatch(Update);
    }

    private static void Dispatch(string globalCommand)
    {
        var document = ActiveDocument;
        if (document == null)
            return;

        try
        {
            if (!string.IsNullOrEmpty(document.CommandInProgress) ||
                Convert.ToInt32(AcApplication.GetSystemVariable("CMDACTIVE")) != 0)
            {
                document.Editor.WriteMessage("\nACadSmartLeader — завершите текущую команду и повторите действие.\n");
                return;
            }

            document.SendStringToExecute(globalCommand + " ", true, false, true);
        }
        catch (Exception error)
        {
            ReportFailure($"не удалось запустить {globalCommand}", error);
        }
    }

    private static void ReportFailure(string operation, Exception error) =>
        ActiveDocument?.Editor.WriteMessage($"\nACadSmartLeader — {operation}. {error.Message}\n");

    private sealed class RibbonCommand : ICommand
    {
        private readonly string globalCommand;

        public RibbonCommand(string globalCommand) => this.globalCommand = globalCommand;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Dispatch(globalCommand);
    }
}
