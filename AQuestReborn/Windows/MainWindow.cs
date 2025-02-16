using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using RoleplayingQuestCore;
using SixLabors.ImageSharp.Drawing;
using Path = System.IO.Path;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private Plugin Plugin;
    private FileDialogManager _fileDialogManager;
    private ExcelSheet<TerritoryType>? _territorySheets;
    private string[] _installedQuestList;
    private int _currentSelectedInstalledQuest;
    List<RoleplayingQuest> _roleplayingQuests = new List<RoleplayingQuest>();

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("A Quest Reborn##mainwindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Plugin = plugin;
        _fileDialogManager = new FileDialogManager();
        _territorySheets = Plugin.DataManager.GameData.GetExcelSheet<TerritoryType>();
        Size = new Vector2(500, 500);
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        if (Plugin.RoleplayingQuestManager != null)
        {
            Plugin.RoleplayingQuestManager.ScanDirectory();
        }
        base.OnOpen();
    }
    public override void Draw()
    {
        _fileDialogManager.Draw();
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (!string.IsNullOrEmpty(Plugin.Configuration.QuestInstallFolder) && Directory.Exists(Plugin.Configuration.QuestInstallFolder))
            {
                if (ImGui.BeginTabItem("安装任务"))
                {
                    DrawInstalledQuests();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("任务目标"))
                {
                    DrawQuestObjectives();
                    ImGui.EndTabItem();
                }
            }
            if (ImGui.BeginTabItem("插件设置"))
            {
                DrawInitialSetup();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        if (ImGui.Button("捐赠以支持进一步开发", new Vector2(Size.Value.X, 50)))
        {
            ProcessStartInfo ProcessInfo = new ProcessStartInfo();
            Process Process = new Process();
            ProcessInfo = new ProcessStartInfo("https://ko-fi.com/sebastina");
            ProcessInfo.UseShellExecute = true;
            Process = Process.Start(ProcessInfo);
        }
    }

    private void DrawInitialSetup()
    {
        if (ImGui.Button("选择一个空文件夹用于自定义任务安装（无需管理员权限）"))
        {
            _fileDialogManager.Reset();
            ImGui.OpenPopup("OpenPathDialog##editorwindow");
        }
        if (ImGui.BeginPopup("OpenPathDialog##editorwindow"))
        {
            _fileDialogManager.SaveFolderDialog("Pick location", "QuestReborn", (isOk, folder) =>
            {
                if (isOk)
                {
                    if (!folder.Contains("Program Files") && !folder.Contains("FINAL FANTASY XIV - A Realm Reborn"))
                    {
                        Directory.CreateDirectory(folder);
                        //if (Directory.GetFiles(folder).Length == 0)
                        //{
                        Plugin.Configuration.QuestInstallFolder = folder;
                        Plugin.RoleplayingQuestManager.QuestInstallFolder = folder;
                        Plugin.Configuration.Save();
                        //}
                    }
                }
            }, null, true);
            ImGui.EndPopup();
        }
    }

    private void DrawQuestObjectives()
    {
        int index = 0;
        List<string> strings = new List<string>();
        foreach (var item in Plugin.RoleplayingQuestManager.GetCurrentObjectives())
        {
            if (item.Item2.SubObjectivesComplete() && !item.Item2.ObjectiveCompleted && !item.Item3.HasQuestAcceptancePopup)
            {
                if (!strings.Contains(item.Item3.QuestName))
                {
                    ImGui.TextWrapped(item.Item3.QuestName);
                    strings.Add(item.Item3.QuestName);
                }
                ImGui.SetCursorPosX(50);
                ImGui.TextWrapped(item.Item2.Objective + (item.Item2.DontShowOnMap ? " (Hidden Location)" : $" ({_territorySheets.GetRow((uint)item.Item2.TerritoryId).PlaceName.Value.Name.ToString()})"));
            }
            index++;
        }
    }

    private void DrawInstalledQuests()
    {
        InstalledQuestsTab();
    }
    private void InstalledQuestsTab()
    {
        ImGui.BeginTable("##Installed Quests", 2);
        ImGui.TableSetupColumn("任务选择器", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("任务信息", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawInstalledQuestSelector();
        ImGui.TableSetColumnIndex(1);
        DrawInstalledQuestInformation();
        ImGui.EndTable();
    }

    private void DrawInstalledQuestInformation()
    {
        if (_currentSelectedInstalledQuest < Plugin.RoleplayingQuestManager.QuestChains.Count)
        {
            var roleplayingQuest = Plugin.RoleplayingQuestManager.QuestChains.ElementAt(_currentSelectedInstalledQuest).Value;
            string startingLocation = "";
            if (roleplayingQuest.QuestObjectives.Count > 0)
            {
                startingLocation = _territorySheets.GetRow((uint)roleplayingQuest.QuestObjectives[0].TerritoryId).PlaceName.Value.Name.ToString();
            }
            ImGui.Text($"作者名称：{roleplayingQuest.QuestAuthor}");
            ImGui.Text($"任务名称：{roleplayingQuest.QuestName}");
            ImGui.TextWrapped($"任务描述：{roleplayingQuest.QuestDescription}");
            if (!string.IsNullOrEmpty(startingLocation))
            {
                ImGui.Text($"起始地点：{startingLocation}");
            }
            if (ImGui.Button("重置任务进度"))
            {
                try
                {
                    Plugin.RoleplayingQuestManager.AddQuest(Path.Combine(Plugin.Configuration.QuestInstallFolder, roleplayingQuest.QuestName + @"\main.quest"));
                }
                catch
                {
                    Plugin.RoleplayingQuestManager.QuestChains.Remove(roleplayingQuest.QuestId);
                }
                Plugin.AQuestReborn.RefreshNpcs(Plugin.ClientState.TerritoryType, roleplayingQuest.QuestId);
                Plugin.AQuestReborn.RefreshMapMarkers();
                Plugin.SaveProgress();
            }
            ImGui.SameLine();
            if (ImGui.Button("编辑任务"))
            {
                Plugin.EditorWindow.RoleplayingQuestCreator.EditQuest(Path.Combine(Plugin.Configuration.QuestInstallFolder, roleplayingQuest.QuestName + @"\main.quest"));
                Plugin.EditorWindow.IsOpen = true;
            }
            if (ImGui.Button("打开目录"))
            {
                string path = Path.Combine(Plugin.Configuration.QuestInstallFolder, roleplayingQuest.QuestName);
                ProcessStartInfo ProcessInfo;
                Process Process; ;
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch
                {
                }
                ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + path + @"""");
                ProcessInfo.UseShellExecute = true;
                Process = Process.Start(ProcessInfo);
            }
            ImGui.SameLine();
            if (ImGui.Button("导出任务"))
            {
                string foundPath = roleplayingQuest.FoundPath;
                string zipPath = "";
                if (string.IsNullOrEmpty(foundPath))
                {
                    zipPath = Path.GetDirectoryName(foundPath);
                }
                else
                {
                    zipPath = Path.Combine(Plugin.Configuration.QuestInstallFolder, roleplayingQuest.QuestName);
                }
                Plugin.RoleplayingQuestManager.ExportQuestPack(zipPath);
                string path = Path.GetDirectoryName(zipPath);
                ProcessStartInfo ProcessInfo;
                Process Process; ;
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch
                {
                }
                ProcessInfo = new ProcessStartInfo("explorer.exe", @"""" + path + @"""");
                ProcessInfo.UseShellExecute = true;
                Process = Process.Start(ProcessInfo);
            }
            ImGui.SameLine();
            if (ImGui.Button("恢复任务"))
            {
                string foundPath = roleplayingQuest.FoundPath;
                string recoveryPath = "";
                if (string.IsNullOrEmpty(foundPath))
                {
                    recoveryPath = Path.GetDirectoryName(foundPath);
                }
                else
                {
                    recoveryPath = Path.Combine(Plugin.Configuration.QuestInstallFolder, roleplayingQuest.QuestName);
                }
                Plugin.RoleplayingQuestManager.RecoverDeletedQuest(roleplayingQuest, recoveryPath);
            }
        }
    }

    private void DrawInstalledQuestSelector()
    {
        if (Plugin.RoleplayingQuestManager != null)
        {
            var installedQuests = Plugin.RoleplayingQuestManager.QuestChains;
            var questItems = new List<string>();
            foreach (var item in installedQuests)
            {
                if (item.Value != null && !string.IsNullOrEmpty(item.Value.QuestName))
                {
                    questItems.Add(item.Value.QuestName);
                }
            }
            if (_currentSelectedInstalledQuest > questItems.Count)
            {
                _currentSelectedInstalledQuest = 0;
            }
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            ImGui.ListBox("##installedQuestInformation", ref _currentSelectedInstalledQuest, questItems.ToArray(), questItems.Count, 10);
            if (ImGui.Button("任务创建器"))
            {
                Plugin.EditorWindow.IsOpen = true;
            }
            if (ImGui.Button("安装任务"))
            {
                _fileDialogManager.Reset();
                ImGui.OpenPopup("OpenPathDialog##editorwindow");
            }
            if (ImGui.BeginPopup("OpenPathDialog##editorwindow"))
            {
                _fileDialogManager.OpenFileDialog("选择任务文件", ".qmp", (isOk, file) =>
                {
                    if (isOk)
                    {
                        Plugin.RoleplayingQuestManager.OpenQuestPack(file[0]);
                        Plugin.RoleplayingQuestManager.ScanDirectory();
                    }
                }, 0, null, true);
                ImGui.EndPopup();
            }
            if (ImGui.Button("重新扫描任务"))
            {
                Plugin.RoleplayingQuestManager.ScanDirectory();
            }
        }
    }
}
