using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using RoleplayingQuestCore;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLookingForGroup;
using static RoleplayingQuestCore.QuestObjective;
using static RoleplayingQuestCore.RoleplayingQuest;
using static RoleplayingQuestCore.BranchingChoice;
using static RoleplayingQuestCore.QuestEvent;
using AQuestReborn;
using System.IO;
using System.Speech.Recognition;
using Lumina.Excel.Sheets;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using McdfDataImporter;

namespace SamplePlugin.Windows;

public class EditorWindow : Window, IDisposable
{
    private Plugin Plugin;
    private RoleplayingQuestCreator _roleplayingQuestCreator = new RoleplayingQuestCreator();
    private FileDialogManager _fileDialogManager;
    private EditorWindow _subEditorWindow;
    private NPCEditorWindow _npcEditorWindow;
    private NPCTransformEditorWindow _npcTransformEditorWindow;
    private string[] _nodeNames = new string[] { };
    private string[] _dialogues = new string[] { };
    private int _selectedEvent;
    private int _selectedBranchingChoice;
    private string[] _branchingChoices = new string[] { };
    private string[] _boxStyles = new string[] {
        "Normal（普通）", "Style2（风格2）", "Telepathic（心灵感应）", "Omicron/Machine（奥密克戎/机械）", "Shout（喊叫）",
        "Written Lore（书面传说）", "Monster/Creature（怪物/生物）", "Dragon/Linkpearl（龙/通讯珠）", "System/Ascian（系统/无影）" };
    private QuestObjective _objectiveInFocus;
    private float _globalScale;
    private bool _shiftModifierHeld;
    private bool _isCreatingAppearance;

    public RoleplayingQuestCreator RoleplayingQuestCreator { get => _roleplayingQuestCreator; set => _roleplayingQuestCreator = value; }

    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public EditorWindow(Plugin plugin)
        : base("Quest Creator##" + Guid.NewGuid().ToString(), ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(1200, 1100);
        Plugin = plugin;
        _fileDialogManager = new FileDialogManager();
        if (_npcTransformEditorWindow == null)
        {
            _npcTransformEditorWindow = new NPCTransformEditorWindow(Plugin, _roleplayingQuestCreator);
            Plugin.WindowSystem.AddWindow(_npcTransformEditorWindow);
        }
    }
    public override void OnOpen()
    {
        if (_roleplayingQuestCreator.CurrentQuest != null)
        {
            WindowName = (!_roleplayingQuestCreator.CurrentQuest.IsSubQuest ? "Quest Creator##" : "Branching Quest Creator##") + Guid.NewGuid().ToString();
        }
        RefreshMenus();
    }
    public void Dispose()
    {
        if (_subEditorWindow != null)
        {
            _subEditorWindow.Dispose();
        }
        if (_npcEditorWindow != null)
        {
            _npcEditorWindow.Dispose();
        }
        if (_npcTransformEditorWindow != null)
        {
            _npcTransformEditorWindow.Dispose();
        }
    }
    public override void Draw()
    {
        _globalScale = ImGuiHelpers.GlobalScale;
        _shiftModifierHeld = ImGui.GetIO().KeyShift;
        _fileDialogManager.Draw();
        if (!_roleplayingQuestCreator.CurrentQuest.IsSubQuest)
        {
            if (ImGui.Button("保存任务"))
            {
                PersistQuest();
            }
            ImGui.SameLine();
            if (ImGui.Button("新建任务"))
            {
                _roleplayingQuestCreator.EditQuest(new RoleplayingQuest());
                RefreshMenus();
            }
            ImGui.SameLine();
            if (ImGui.Button("教程（跳转Youtube）"))
            {
                ProcessStartInfo ProcessInfo = new ProcessStartInfo();
                Process Process = new Process();
                ProcessInfo = new ProcessStartInfo("https://www.youtube.com/watch?v=JJM9aHRHkDw");
                ProcessInfo.UseShellExecute = true;
                Process = Process.Start(ProcessInfo);
            }
            if (_roleplayingQuestCreator != null && _roleplayingQuestCreator.CurrentQuest != null)
            {
                var questAuthor = _roleplayingQuestCreator.CurrentQuest.QuestAuthor;
                var questName = _roleplayingQuestCreator.CurrentQuest.QuestName;
                var questDescription = _roleplayingQuestCreator.CurrentQuest.QuestDescription;
                var contentRating = (int)_roleplayingQuestCreator.CurrentQuest.ContentRating;
                var questReward = _roleplayingQuestCreator.CurrentQuest.QuestReward;
                var questRewardType = (int)_roleplayingQuestCreator.CurrentQuest.TypeOfReward;
                var questThumbnail = _roleplayingQuestCreator.CurrentQuest.QuestThumbnailPath;
                var contentRatingTypes = Enum.GetNames(typeof(QuestContentRating));
                var questRewardTypes = Enum.GetNames(typeof(QuestRewardType));

                var questStartTitleCard = _roleplayingQuestCreator.CurrentQuest.QuestStartTitleCard;
                var questEndTitleCard = _roleplayingQuestCreator.CurrentQuest.QuestEndTitleCard;
                var questStartTitleSound = _roleplayingQuestCreator.CurrentQuest.QuestStartTitleSound;
                var questEndTitleSound = _roleplayingQuestCreator.CurrentQuest.QuestEndTitleSound;
                var hasQuestAcceptancePopup = _roleplayingQuestCreator.CurrentQuest.HasQuestAcceptancePopup;

                // 内容评级中文映射
                var contentRatingTypesTranslated = contentRatingTypes.Select(name =>
                {
                    return name switch
                    {
                        "AllAges"       => "所有年龄",
                        "Teen"          => "青少年",
                        "AdultsOnly"    => "仅限成人",
                        _ => name,
                    };
                }).ToArray();

                // 任务奖励类型中文映射
                var questRewardTypesTranslated = questRewardTypes.Select(name =>
                {
                    return name switch
                    {
                        "None"              => "无奖励",
                        "SecretMessage"     => "秘密信息",
                        "OnlineLink"        => "在线链接",
                        "MediaFile"         => "媒体文件",
                        _ => name,
                    };
                }).ToArray();

                ImGui.BeginTable("##Info Table", 2);
                ImGui.TableSetupColumn("信息 1", ImGuiTableColumnFlags.WidthFixed, 400);
                ImGui.TableSetupColumn("信息 2", ImGuiTableColumnFlags.WidthStretch, 600);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (ImGui.InputText("作者##", ref questAuthor, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestAuthor = questAuthor;
                }
                if (ImGui.InputText("任务名称##", ref questName, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestName = questName;
                }
                if (ImGui.InputText("任务描述##", ref questDescription, 56))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestDescription = questDescription;
                }
                if (ImGui.InputText("任务缩略图##", ref questThumbnail, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestThumbnailPath = questThumbnail;
                }
                if (ImGui.Combo("内容评级##", ref contentRating, contentRatingTypesTranslated, contentRatingTypesTranslated.Length))
                {
                    _roleplayingQuestCreator.CurrentQuest.ContentRating = (QuestContentRating)contentRating;
                }
                if (ImGui.Checkbox("有任务接受弹窗", ref hasQuestAcceptancePopup))
                {
                    _roleplayingQuestCreator.CurrentQuest.HasQuestAcceptancePopup = hasQuestAcceptancePopup;
                }
                ImGui.TableSetColumnIndex(1);
                if (ImGui.InputText("任务开始标题卡##", ref questStartTitleCard, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestStartTitleCard = questStartTitleCard;
                }
                if (ImGui.InputText("任务结束标题卡##", ref questEndTitleCard, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestEndTitleCard = questEndTitleCard;
                }
                if (ImGui.InputText("任务开始标题音效##", ref questStartTitleSound, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestStartTitleSound = questStartTitleSound;
                }
                if (ImGui.InputText("任务结束标题音效##", ref questEndTitleSound, 255))
                {
                    _roleplayingQuestCreator.CurrentQuest.QuestEndTitleSound = questEndTitleSound;
                }


                if (ImGui.Combo("任务奖励类型##", ref questRewardType, questRewardTypesTranslated, questRewardTypesTranslated.Length))
                {
                    _roleplayingQuestCreator.CurrentQuest.TypeOfReward = (QuestRewardType)questRewardType;
                }
                switch (_roleplayingQuestCreator.CurrentQuest.TypeOfReward)
                {
                    case QuestRewardType.SecretMessage:
                        if (ImGui.InputText("任务奖励 (秘密信息)", ref questReward, 255))
                        {
                            _roleplayingQuestCreator.CurrentQuest.QuestReward = questReward;
                        }
                        break;
                    case QuestRewardType.OnlineLink:
                        if (ImGui.InputText("任务奖励 (下载链接)", ref questReward, 255))
                        {
                            _roleplayingQuestCreator.CurrentQuest.QuestReward = questReward;
                        }
                        break;
                    case QuestRewardType.MediaFile:
                        if (ImGui.InputText("任务奖励 (媒体文件路径)", ref questReward, 255))
                        {
                            _roleplayingQuestCreator.CurrentQuest.QuestReward = questReward;
                        }
                        break;
                }
                ImGui.EndTable();
                if (ImGui.Button("编辑 NPC 外观数据##"))
                {
                    if (_npcEditorWindow == null)
                    {
                        _npcEditorWindow = new NPCEditorWindow(Plugin);
                        Plugin.WindowSystem.AddWindow(_npcEditorWindow);
                    }
                    if (_npcEditorWindow != null)
                    {
                        _npcEditorWindow.SetEditingQuest(_roleplayingQuestCreator.CurrentQuest);
                        _npcEditorWindow.IsOpen = true;
                    }
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("导出以便复用"))
        {
            _fileDialogManager.Reset();
            ImGui.OpenPopup("OpenPathDialog##editorwindow");
        }
        if (ImGui.BeginPopup("OpenPathDialog##editorwindow"))
        {
            _fileDialogManager.SaveFileDialog("导出任务线数据", ".quest", "", ".quest", (isOk, file) =>
            {
                if (isOk)
                {
                    _roleplayingQuestCreator.SaveQuestline(_roleplayingQuestCreator.CurrentQuest, file);
                }
            }, "", true);
            ImGui.EndPopup();
        }
        ImGui.BeginTable("##Editor Table", 2);
        ImGui.TableSetupColumn("目标列表", ImGuiTableColumnFlags.WidthFixed, 300);
        ImGui.TableSetupColumn("目标编辑器", ImGuiTableColumnFlags.WidthStretch, 600);
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawQuestObjectives();
        ImGui.TableSetColumnIndex(1);
        DrawQuestNodeEditor();
        ImGui.EndTable();
    }

    private void PersistQuest()
    {
        string questPath = Path.Combine(Plugin.Configuration.QuestInstallFolder, _roleplayingQuestCreator.CurrentQuest.QuestName);
        _roleplayingQuestCreator.SaveQuest(questPath);
        Plugin.RoleplayingQuestManager.AddQuest(Path.Combine(questPath, "main.quest"), false, true);
        Plugin.AQuestReborn.RefreshNpcs(Plugin.ClientState.TerritoryType, _roleplayingQuestCreator.CurrentQuest.QuestId, true);
        Plugin.AQuestReborn.RefreshMapMarkers();
    }

    private void DrawQuestNodeEditor()
    {
        if (_objectiveInFocus != null)
        {
            var questObjective = _objectiveInFocus;
            var territoryId = questObjective.TerritoryId;
            var territoryDiscriminator = questObjective.TerritoryDiscriminator;
            var usesTerritoryDiscriminator = questObjective.UsesTerritoryDiscriminator;
            var objective = questObjective.Objective;
            var coordinates = questObjective.Coordinates;
            var questText = questObjective.QuestText;
            var questPointType = (int)questObjective.TypeOfQuestPoint;
            var objectiveStatusType = (int)questObjective.ObjectiveStatus;
            var objectiveTriggerType = (int)questObjective.TypeOfObjectiveTrigger;
            var questPointTypes = Enum.GetNames(typeof(QuestPointType));
            var objectiveStatusTypes = Enum.GetNames(typeof(ObjectiveStatusType));
            var objectiveTriggerTypes = Enum.GetNames(typeof(ObjectiveTriggerType));
            var triggerText = questObjective.TriggerText;
            var objectiveImmediatelySatisfiesParent = questObjective.ObjectiveImmediatelySatisfiesParent;
            var maximum3dIndicatorDistance = questObjective.Maximum3dIndicatorDistance;
            var dontShowOnMap = questObjective.DontShowOnMap;
            var playerPositionIsLockedDuringEvents = questObjective.PlayerPositionIsLockedDuringEvents;

            // 任务目标触发类型中文映射
            var objectiveTriggerTypesTranslated = objectiveTriggerTypes.Select(name =>
            {
                return name switch
                {
                    "NormalInteraction"         => "正常互动",
                    "DoEmote"                   => "执行表情",
                    "SayPhrase"                 => "说出语句",
                    "SubObjectivesFinished"     => "子目标完成",
                    "KillEnemy"                 => "击杀敌人",
                    "BoundingTrigger"           => "边界触发",
                    _ => name,
                };
            }).ToArray();

            // 任务目标状态类型中文映射
            var objectiveStatusTypesTranslated = objectiveStatusTypes.Select(name =>
            {
                return name switch
                {
                    "Pending"   => "待处理",
                    "Complete"  => "已完成",
                    _ => name,
                };
            }).ToArray();

            // 任务点类型中文映射
            var questPointTypesTranslated = questPointTypes.Select(name =>
            {
                return name switch
                {
                    "NPC"           => "NPC",
                    "GroundItem"    => "地面物品",
                    "TallItem"      => "高处物品",
                    "StandAndWait"  => "站立等待",
                    _ => name,
                };
            }).ToArray();

            ImGui.SetNextItemWidth(400);
            ImGui.LabelText("##objectiveIdLabel", $"任务目标 ID：" + questObjective.Id);
            ImGui.SameLine();
            if (ImGui.Button("复制 ID 到剪贴板"))
            {
                ImGui.SetClipboardText(questObjective.Id.Trim());
            }
            ImGui.SameLine();
            if (ImGui.Button("设置任务目标坐标"))
            {
                questObjective.Coordinates = Plugin.ClientState.LocalPlayer.Position;
                questObjective.TerritoryId = Plugin.ClientState.TerritoryType;
                questObjective.TerritoryDiscriminator = Plugin.AQuestReborn.Discriminator;
            }
            ImGui.SetNextItemWidth(200);
            ImGui.LabelText("##coordinatesLabel", $"坐标：X:{Math.Round(questObjective.Coordinates.X)}," +
                $"Y:{Math.Round(questObjective.Coordinates.Y)}," +
                $"Z:{Math.Round(questObjective.Coordinates.Z)}");
            ImGui.SetNextItemWidth(125);
            ImGui.SameLine();
            ImGui.LabelText("##territoryLabel", $"区域 ID：{questObjective.TerritoryId}");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300);
            ImGui.LabelText("##discriminatorLabel", $"判别符：" + questObjective.TerritoryDiscriminator);
            ImGui.SetNextItemWidth(110);
            if (ImGui.InputFloat("最大指示器距离##", ref maximum3dIndicatorDistance))
            {
                questObjective.Maximum3dIndicatorDistance = maximum3dIndicatorDistance;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("不在地图上显示##", ref dontShowOnMap))
            {
                questObjective.DontShowOnMap = dontShowOnMap;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("锁定到服务器/区/地块/房间##", ref usesTerritoryDiscriminator))
            {
                questObjective.UsesTerritoryDiscriminator = usesTerritoryDiscriminator;
            }
            if (!questObjective.IsAPrimaryObjective)
            {
                if (ImGui.Checkbox("立即满足父目标##", ref objectiveImmediatelySatisfiesParent))
                {
                    questObjective.ObjectiveImmediatelySatisfiesParent = objectiveImmediatelySatisfiesParent;
                }
            }
            if (ImGui.InputText("任务目标描述##", ref objective, 500))
            {
                questObjective.Objective = objective;
            }
            ImGui.SetNextItemWidth(110);
            if (ImGui.Combo("任务点类型##", ref questPointType, questPointTypesTranslated, questPointTypesTranslated.Length))
            {
                questObjective.TypeOfQuestPoint = (QuestPointType)questPointType;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(110);
            if (ImGui.Combo("任务目标状态类型##", ref objectiveStatusType, objectiveStatusTypesTranslated, objectiveStatusTypesTranslated.Length))
            {
                questObjective.ObjectiveStatus = (ObjectiveStatusType)objectiveStatusType;
            }
            if (ImGui.Combo("任务目标触发类型##", ref objectiveTriggerType, objectiveTriggerTypesTranslated, objectiveTriggerTypesTranslated.Length))
            {
                questObjective.TypeOfObjectiveTrigger = (ObjectiveTriggerType)objectiveTriggerType;
            }
            switch (questObjective.TypeOfObjectiveTrigger)
            {
                case ObjectiveTriggerType.DoEmote:
                    if (ImGui.InputText("表情 ID##", ref triggerText, 500))
                    {
                        questObjective.TriggerText = triggerText;
                    }
                    break;
                case ObjectiveTriggerType.SayPhrase:
                    if (ImGui.InputText("说话短语##", ref triggerText, 500))
                    {
                        questObjective.TriggerText = triggerText;
                    }
                    break;
                case ObjectiveTriggerType.KillEnemy:
                    if (ImGui.InputText("敌人名称##", ref triggerText, 500))
                    {
                        questObjective.TriggerText = triggerText;
                    }
                    break;
                case ObjectiveTriggerType.BoundingTrigger:
                    var minimumX = questObjective.Collider.MinimumX;
                    var maximumX = questObjective.Collider.MaximumX;
                    var minimumY = questObjective.Collider.MinimumY;
                    var maximumY = questObjective.Collider.MaximumY;
                    var minimumZ = questObjective.Collider.MinimumZ;
                    var maximumZ = questObjective.Collider.MaximumZ;
                    ImGui.TextWrapped($"最小 X: {minimumX}, 最大 X: {maximumX}, 最小 Y: {minimumY}, 最大 Y: {maximumY}, 最小 Z: {minimumZ}, 最大 Z: {maximumZ}");
                    if (ImGui.Button("设置最小 XZ##"))
                    {
                        questObjective.Collider.MinimumX = Plugin.ClientState.LocalPlayer.Position.X;
                        questObjective.Collider.MinimumZ = Plugin.ClientState.LocalPlayer.Position.Z;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("设置最大 XZ##"))
                    {
                        if (Plugin.ClientState.LocalPlayer.Position.X < minimumX)
                        {
                            questObjective.Collider.MaximumX = questObjective.Collider.MinimumX;
                            questObjective.Collider.MinimumX = Plugin.ClientState.LocalPlayer.Position.X;
                        }
                        else
                        {
                            questObjective.Collider.MaximumX = Plugin.ClientState.LocalPlayer.Position.X;
                        }
                        if (Plugin.ClientState.LocalPlayer.Position.Z < minimumZ)
                        {
                            questObjective.Collider.MaximumZ = questObjective.Collider.MinimumZ;
                            questObjective.Collider.MinimumZ = Plugin.ClientState.LocalPlayer.Position.Z;
                        }
                        else
                        {
                            questObjective.Collider.MaximumZ = Plugin.ClientState.LocalPlayer.Position.Z;
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("设置最小 Y##"))
                    {
                        questObjective.Collider.MinimumY = Plugin.ClientState.LocalPlayer.Position.Y - 5;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("设置最大 Y##"))
                    {
                        questObjective.Collider.MaximumY = Plugin.ClientState.LocalPlayer.Position.Y;
                    }
                    break;
            }
            if (ImGui.Button("编辑 NPC 变换数据##"))
            {
                if (_npcTransformEditorWindow != null)
                {
                    _npcTransformEditorWindow.SetEditingQuest(questObjective);
                    _npcTransformEditorWindow.IsOpen = true;
                }
            }
            if (questObjective.IsAPrimaryObjective)
            {
                ImGui.SameLine();
                if (ImGui.Button("预览任务目标##"))
                {
                    Plugin.RoleplayingQuestManager.SkipToObjective(_roleplayingQuestCreator.CurrentQuest, questObjective.Index);
                    PersistQuest();
                }
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("在事件期间锁定玩家位置", ref playerPositionIsLockedDuringEvents))
            {
                questObjective.PlayerPositionIsLockedDuringEvents = playerPositionIsLockedDuringEvents;
            }
            ImGui.BeginTable("##Event Table", 2);
            ImGui.TableSetupColumn("事件列表", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("事件编辑器", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawQuestEvents();
            ImGui.TableSetColumnIndex(1);
            DrawQuestEventEditor();
            ImGui.EndTable();
        }
    }

    private void DrawQuestEventEditor()
    {
        if (_objectiveInFocus != null)
        {
            var questEvent = _objectiveInFocus.QuestText;
            if (questEvent.Count > 0)
            {
                if (_selectedEvent > questEvent.Count || _selectedEvent < 0)
                {
                    _selectedEvent = 0;
                }
                var item = questEvent[_selectedEvent];
                var dialogueCondition = (int)item.ConditionForDialogueToOccur;
                var objectiveIdToComplete = item.ObjectiveIdToComplete;
                var faceExpression = item.FaceExpression;
                var bodyExpression = item.BodyExpression;
                var faceExpressionPlayer = item.FaceExpressionPlayer;
                var bodyExpressionPlayer = item.BodyExpressionPlayer;
                var npcAlias = item.NpcAlias;
                var npcName = item.NpcName;
                var dialogue = item.Dialogue;
                var boxStyle = item.DialogueBoxStyle;
                var dialogueAudio = item.DialogueAudio;
                var eventBackgroundType = (int)item.TypeOfEventBackground;
                var eventBackground = item.EventBackground;
                var eventEndBehaviour = (int)item.EventEndBehaviour;
                var eventNumberToSkipTo = item.EventNumberToSkipTo;
                var objectiveNumberToSkipTo = item.ObjectiveNumberToSkipTo;
                var eventEndTypes = Enum.GetNames(typeof(QuestEvent.EventBehaviourType));
                var eventBackgroundTypes = Enum.GetNames(typeof(QuestEvent.EventBackgroundType));
                var eventConditionTypes = Enum.GetNames(typeof(QuestEvent.EventConditionType));
                var eventPlayerAppearanceApplicationTypes = Enum.GetNames(typeof(QuestEvent.AppearanceSwapType));
                var appearanceSwap = item.AppearanceSwap;
                var playerAppearanceSwap = item.PlayerAppearanceSwap;
                var playerAppearanceSwapType = (int)item.PlayerAppearanceSwapType;
                var loopAnimation = item.LoopAnimation;
                var loopAnimationPlayer = item.LoopAnimationPlayer;
                var timeLimit = item.TimeLimit;
                var eventHasNoReading = item.EventHasNoReading;
                var looksAtPlayerDuringEvent = item.LooksAtPlayerDuringEvent;
                var eventSetsNewNpcPosition = item.EventSetsNewNpcCoordinates;
                var npcMovementPosition = item.NpcMovementPosition;
                var npcMovementRotation = item.NpcMovementRotation;

                // 事件发生条件中文映射
                var eventConditionTypesTranslated = eventConditionTypes.Select(name =>
                {
                    return name switch
                    {
                        "None"                          => "无",
                        "CompletedSpecificObjectiveId"  => "完成特定目标ID",
                        "PlayerClanId"                  => "玩家兵团ID",
                        "PlayerPhysicalPresentationId"  => "玩家外观ID",
                        "PlayerClassId"                 => "玩家职业ID",
                        "PlayerOutfitTopId"             => "玩家上装ID",
                        "PlayerOutfitBottomId"          => "玩家下装ID",
                        "TimeLimitFailure"              => "超时失败",
                        _ => name,
                    };
                }).ToArray();

                // 事件背景类型中文映射
                var eventBackgroundTypesTranslated = eventBackgroundTypes.Select(name =>
                {
                    return name switch
                    {
                        "None"              => "无背景",
                        "Image"             => "图片",
                        "Video"             => "视频",
                        "ImageTransparent"  => "透明图片",
                        _ => name,
                    };
                }).ToArray();

                // 事件结束行为中文映射
                var eventEndTypesTranslated = eventEndTypes.Select(name =>
                {
                    return name switch
                    {
                        "None" => "无行为",
                        "EventSkipsToDialogueNumber"                        => "跳过对话编号",
                        "EventEndsEarlyWhenHit"                             => "被击中时提前结束",
                        "EventEndsEarlyWhenHitNoProgression"                => "被击中时提前结束（无进度）",
                        "EventEndsEarlyWhenHitAndSkipsToObjective"          => "被击中时提前结束并跳到目标",
                        "EventEndsEarlyWhenHitAndNPCFollowsPlayer"          => "被击中时提前结束并NPC跟随玩家",
                        "EventEndsEarlyWhenHitAndNPCStopsFollowingPlayer"   => "被击中时提前结束并NPC停止跟随玩家",
                        "NPCFollowsPlayer"                                  => "NPC跟随玩家",
                        "NPCStopsFollowingPlayer"                           => "NPC停止跟随玩家",
                        "EventEndsEarlyWhenHitAndStartsTimer"               => "被击中时提前结束并启动计时器",
                        "StartsTimer"                                       => "启动计时器",
                        _=> name,
                    };
                }).ToArray();

                // 玩家外观替换类型中文映射
                var eventPlayerAppearanceApplicationTypesTranslated = eventPlayerAppearanceApplicationTypes.Select(name =>
                {
                    return name switch
                    {
                        "EntireAppearance"                  => "完整外观",
                        "RevertAppearance"                  => "恢复外观",
                        "PreserveRace"                      => "保留种族",
                        "PreserveMasculinityAndFemininity"  => "保留性别特征",
                        "PreserveAllPhysicalTraits"         => "保留所有身体特征",
                        "OnlyGlamourerData"                 => "仅保留 Glamourer 数据",
                        "OnlyCustomizeData"                 => "仅保留外貌数据",
                        "OnlyModData"                       => "仅保留模组数据",
                        _ => name,
                    };
                }).ToArray();

                if (ImGui.BeginTabBar("事件编辑器标签"))
                {
                    if (ImGui.BeginTabItem("叙事"))
                    {
                        if (ImGui.Combo("事件发生条件##", ref dialogueCondition, eventConditionTypesTranslated, eventConditionTypesTranslated.Length))
                        {
                            item.ConditionForDialogueToOccur = (EventConditionType)dialogueCondition;
                        }
                        switch (item.ConditionForDialogueToOccur)
                        {
                            case EventConditionType.None:
                                break;
                            case EventConditionType.CompletedSpecificObjectiveId:
                                if (ImGui.InputText("完成的目标ID##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.PlayerClanId:
                                if (ImGui.InputText("所需的氏族ID##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.PlayerPhysicalPresentationId:
                                if (ImGui.InputText("(Masculine: 0, Feminine: 1)##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.PlayerClassId:
                                if (ImGui.InputText("玩家职业ID (SMN, RPR, WHM等)##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.PlayerOutfitTopId:
                                if (ImGui.InputText("玩家上衣ID##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.PlayerOutfitBottomId:
                                if (ImGui.InputText("玩家下装ID##", ref objectiveIdToComplete, 40))
                                {
                                    item.ObjectiveIdToComplete = objectiveIdToComplete;
                                }
                                break;
                            case EventConditionType.TimeLimitFailure:
                                break;
                        }
                        ImGui.SetNextItemWidth(150);
                        if (ImGui.InputText("NPC别名##", ref npcAlias, 40))
                        {
                            item.NpcAlias = npcAlias;
                        }
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150);
                        if (ImGui.InputText("NPC名称##", ref npcName, 40))
                        {
                            item.NpcName = npcName;
                        }
                        if (ImGui.InputText("对话##", ref dialogue, 500))
                        {
                            item.Dialogue = dialogue;
                        }
                        if (ImGui.InputText("对话音频路径##", ref dialogueAudio, 255))
                        {
                            item.DialogueAudio = dialogueAudio;
                        }

                        if (ImGui.Combo("对话框样式##", ref boxStyle, _boxStyles, _boxStyles.Length))
                        {
                            item.DialogueBoxStyle = boxStyle;
                        }
                        ImGui.SetNextItemWidth(100);
                        if (ImGui.InputInt("NPC面部表情ID##", ref faceExpression))
                        {
                            item.FaceExpression = faceExpression;
                        }
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(100);
                        if (ImGui.InputInt("NPC身体表情ID##", ref bodyExpression))
                        {
                            item.BodyExpression = bodyExpression;
                        }
                        ImGui.SameLine();
                        if (ImGui.Checkbox("循环动画##", ref loopAnimation))
                        {
                            item.LoopAnimation = loopAnimation;
                        }

                        ImGui.SetNextItemWidth(100);
                        if (ImGui.InputInt("玩家面部表情ID##", ref faceExpressionPlayer))
                        {
                            item.FaceExpressionPlayer = faceExpressionPlayer;
                        }
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(100);
                        if (ImGui.InputInt("玩家身体表情ID##", ref bodyExpressionPlayer))
                        {
                            item.BodyExpressionPlayer = bodyExpressionPlayer;
                        }
                        ImGui.SameLine();
                        if (ImGui.Checkbox("循环玩家动画##", ref loopAnimationPlayer))
                        {
                            item.LoopAnimationPlayer = loopAnimationPlayer;
                        }

                        if (ImGui.Combo("事件背景类型##", ref eventBackgroundType, eventBackgroundTypesTranslated, eventBackgroundTypesTranslated.Length))
                        {
                            item.TypeOfEventBackground = (EventBackgroundType)eventBackgroundType;
                        }
                        switch (item.TypeOfEventBackground)
                        {
                            case EventBackgroundType.Image:
                            case EventBackgroundType.ImageTransparent:
                                if (ImGui.InputText("事件背景图片路径##", ref eventBackground, 255))
                                {
                                    item.EventBackground = eventBackground;
                                }
                                break;
                            case EventBackgroundType.Video:
                                if (ImGui.InputText("事件背景视频路径##", ref eventBackground, 255))
                                {
                                    item.EventBackground = eventBackground;
                                }
                                break;
                        }
                        if (ImGui.Combo("事件结束行为##", ref eventEndBehaviour, eventEndTypesTranslated, eventEndTypesTranslated.Length))
                        {
                            item.EventEndBehaviour = (EventBehaviourType)eventEndBehaviour;
                        }

                        switch (item.EventEndBehaviour)
                        {
                            case EventBehaviourType.EventSkipsToDialogueNumber:
                                if (ImGui.InputInt("跳过到的事件编号##", ref eventNumberToSkipTo))
                                {
                                    item.EventNumberToSkipTo = eventNumberToSkipTo;
                                }
                                break;
                            case EventBehaviourType.EventEndsEarlyWhenHitAndSkipsToObjective:
                                if (ImGui.InputInt("跳过到的目标编号##", ref objectiveNumberToSkipTo))
                                {
                                    item.ObjectiveNumberToSkipTo = objectiveNumberToSkipTo;
                                }
                                break;
                            case EventBehaviourType.EventEndsEarlyWhenHitAndStartsTimer:
                            case EventBehaviourType.StartsTimer:
                                if (ImGui.InputInt("时间限制（毫秒）##", ref timeLimit))
                                {
                                    item.TimeLimit = timeLimit;
                                }
                                break;
                        }
                        if (ImGui.Checkbox("事件无读取##", ref eventHasNoReading))
                        {
                            item.EventHasNoReading = eventHasNoReading;
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("分支选择## we're unique"))
                    {
                        DrawBranchingChoicesMenu();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("外观替换##we're unique and such"))
                    {
                        if (ImGui.InputText("NPC外观替换##", ref appearanceSwap, 4000))
                        {
                            item.AppearanceSwap = appearanceSwap;
                        }
                        if (_isCreatingAppearance)
                        {
                            ImGui.BeginDisabled();
                        }
                        if (ImGui.Button(_isCreatingAppearance ? "正在创建外观，请稍候" : "从当前玩家外观创建NPC外观##"))
                        {
                            Task.Run(() =>
                            {
                                _isCreatingAppearance = true;
                                string mcdfName = npcName + "-" + Guid.NewGuid().ToString() + ".mcdf";
                                string questPath = Path.Combine(Plugin.Configuration.QuestInstallFolder, _roleplayingQuestCreator.CurrentQuest.QuestName);
                                string mcdfPath = Path.Combine(questPath, mcdfName);
                                Directory.CreateDirectory(questPath);
                                AppearanceAccessUtils.AppearanceManager.CreateMCDF(mcdfPath);
                                Plugin.EditorWindow.RoleplayingQuestCreator.SaveQuest(questPath);
                                item.AppearanceSwap = mcdfName;
                                _isCreatingAppearance = false;
                            });
                        }
                        if (_isCreatingAppearance)
                        {
                            ImGui.EndDisabled();
                        }
                        if (ImGui.InputText("玩家外观替换##", ref playerAppearanceSwap, 4000))
                        {
                            item.PlayerAppearanceSwap = playerAppearanceSwap;
                        }
                        if (ImGui.Combo("玩家外观替换类型", ref playerAppearanceSwapType, eventPlayerAppearanceApplicationTypesTranslated, eventPlayerAppearanceApplicationTypesTranslated.Length))
                        {
                            item.PlayerAppearanceSwapType = (AppearanceSwapType)playerAppearanceSwapType;
                        }
                        if (_isCreatingAppearance)
                        {
                            ImGui.BeginDisabled();
                        }
                        if (ImGui.Button(_isCreatingAppearance ? "正在创建外观，请稍候" : "从当前玩家外观创建玩家外观##"))
                        {
                            Task.Run(() =>
                            {
                                _isCreatingAppearance = true;
                                string mcdfName = npcName + "-" + Guid.NewGuid().ToString() + ".mcdf";
                                string questPath = Path.Combine(Plugin.Configuration.QuestInstallFolder, _roleplayingQuestCreator.CurrentQuest.QuestName);
                                string mcdfPath = Path.Combine(questPath, mcdfName);
                                Directory.CreateDirectory(questPath);
                                AppearanceAccessUtils.AppearanceManager.CreateMCDF(mcdfPath);
                                Plugin.EditorWindow.RoleplayingQuestCreator.SaveQuest(questPath);
                                item.PlayerAppearanceSwap = mcdfName;
                                _isCreatingAppearance = false;
                            });
                        }
                        if (_isCreatingAppearance)
                        {
                            ImGui.EndDisabled();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("定位## we're unique"))
                    {
                        if (ImGui.Checkbox("事件期间看着玩家", ref looksAtPlayerDuringEvent))
                        {
                            item.LooksAtPlayerDuringEvent = looksAtPlayerDuringEvent;
                        }
                        if (ImGui.Checkbox("事件设置新NPC位置", ref eventSetsNewNpcPosition))
                        {
                            item.EventSetsNewNpcCoordinates = eventSetsNewNpcPosition;
                        }
                        if (eventSetsNewNpcPosition)
                        {
                            if (ImGui.DragFloat3("NPC移动位置", ref npcMovementPosition))
                            {
                                item.NpcMovementPosition = npcMovementPosition;
                            }
                            if (ImGui.DragFloat3("NPC移动旋转", ref npcMovementRotation))
                            {
                                item.NpcMovementRotation = npcMovementRotation;
                            }
                            if (ImGui.Button("基于玩家位置设置坐标"))
                            {
                                item.NpcMovementPosition = Plugin.ClientState.LocalPlayer.Position;
                                item.NpcMovementRotation = new Vector3(0, CoordinateUtility.ConvertRadiansToDegrees(Plugin.ClientState.LocalPlayer.Rotation) + 180, 0);
                            }
                        }
                        ImGui.EndTabItem();
                    }
                }
            }
        }
    }

    private void DrawBranchingChoicesMenu()
    {
        ImGui.BeginTable("##Branching Choices Table", 2);
        ImGui.TableSetupColumn("分支选择列表", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("分支选择编辑器", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawBranchingChoices();
        ImGui.TableSetColumnIndex(1);
        DrawBranchingChoicesEditor();
        ImGui.EndTable();
    }

    private void DrawBranchingChoicesEditor()
    {
        if (_objectiveInFocus != null)
        {
            var questText = _objectiveInFocus.QuestText;
            if (questText.Count > 0)
            {
                if (_selectedEvent > questText.Count)
                {
                    _selectedEvent = questText.Count - 1;
                }
                var branchingChoices = questText[_selectedEvent].BranchingChoices;
                if (branchingChoices.Count > 0)
                {
                    if (_selectedBranchingChoice > branchingChoices.Count)
                    {
                        _selectedBranchingChoice = branchingChoices.Count - 1;
                    }
                    var item = branchingChoices[_selectedBranchingChoice];
                    var choiceText = item.ChoiceText;
                    var choiceType = (int)item.ChoiceType;
                    var roleplayingQuest = item.RoleplayingQuest;
                    var eventToJumpTo = item.EventToJumpTo;
                    var eventToJumpToFailure = item.EventToJumpToFailure;
                    var minimumDiceRoll = item.MinimumDiceRoll;
                    var branchingChoiceTypes = Enum.GetNames(typeof(BranchingChoiceType));

                    // 分支选择类型中文映射
                    var branchingChoiceTypesTranslated = branchingChoiceTypes.Select(name =>
                    {
                        return name switch
                        {
                            "SkipToEventNumber"             => "跳过至事件编号",
                            "BranchingQuestline"            => "分支任务线",
                            "RollD20ThenSkipToEventNumber"  => "掷D20后跳过至事件编号",
                            "SkipToEventNumberRandomized"   => "随机跳过至事件编号",
                            _ => name,
                        };
                    }).ToArray();

                    if (ImGui.InputText("选择文本##", ref choiceText, 255))
                    {
                        item.ChoiceText = choiceText;
                    }
                    if (ImGui.Combo("分支选择类型##", ref choiceType, branchingChoiceTypesTranslated, branchingChoiceTypesTranslated.Length))
                    {
                        item.ChoiceType = (BranchingChoiceType)choiceType;
                    }
                    switch (item.ChoiceType)
                    {
                        case BranchingChoiceType.SkipToEventNumber:
                            if (ImGui.InputInt("跳转到的事件编号##", ref eventToJumpTo))
                            {
                                item.EventToJumpTo = eventToJumpTo;
                            }
                            break;
                        case BranchingChoiceType.RollD20ThenSkipToEventNumber:
                            if (ImGui.InputInt("成功时跳转的事件编号##", ref eventToJumpTo))
                            {
                                item.EventToJumpTo = eventToJumpTo;
                            }
                            if (ImGui.InputInt("失败时跳转的事件编号##", ref eventToJumpToFailure))
                            {
                                item.EventToJumpToFailure = eventToJumpToFailure;
                            }
                            if (ImGui.InputInt("成功的最低骰子点数", ref minimumDiceRoll))
                            {
                                if (minimumDiceRoll > 20)
                                {
                                    minimumDiceRoll = 20;
                                }
                                else if (minimumDiceRoll < 0)
                                {
                                    minimumDiceRoll = 0;
                                }
                                item.MinimumDiceRoll = minimumDiceRoll;
                            }
                            break;
                        case BranchingChoiceType.SkipToEventNumberRandomized:
                            var width = ImGui.GetColumnWidth();
                            ImGui.PushID("Vertical Scroll Branching");
                            ImGui.BeginGroup();
                            const ImGuiWindowFlags child_flags = ImGuiWindowFlags.MenuBar;
                            var child_id = ImGui.GetID("Branching Events");
                            bool child_is_visible = ImGui.BeginChild(child_id, new Vector2(width, 200), true, child_flags);
                            for (int i = 0; i < item.RandomizedEventToSkipTo.Count; i++)
                            {
                                try
                                {
                                    // Apparently the for loop evaluation is not enough
                                    if (i < item.RandomizedEventToSkipTo.Count)
                                    {
                                        var randomizedEventToJumpTo = item.RandomizedEventToSkipTo[i];
                                        ImGui.SetNextItemWidth(200);
                                        if (ImGui.InputInt($"跳转到的随机事件编号##{i}", ref randomizedEventToJumpTo))
                                        {
                                            item.RandomizedEventToSkipTo[i] = randomizedEventToJumpTo;
                                        }
                                        ImGui.SameLine();
                                        if (item.RandomizedEventToSkipTo.Count < 2)
                                        {
                                            ImGui.BeginDisabled();
                                        }
                                        ImGui.SameLine();
                                        if (ImGui.Button($"删除##{i}"))
                                        {
                                            item.RandomizedEventToSkipTo.RemoveAt(i);
                                            break;
                                        }
                                        if (item.RandomizedEventToSkipTo.Count < 2)
                                        {
                                            ImGui.EndDisabled();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Plugin.PluginLog.Warning(e, e.Message);
                                }
                            }
                            ImGui.EndChild();
                            ImGui.EndGroup();
                            ImGui.PopID();
                            if (ImGui.Button($"添加随机跳转##"))
                            {
                                item.RandomizedEventToSkipTo.Add(0);
                                break;
                            }
                            break;
                        case BranchingChoiceType.BranchingQuestline:
                            if (ImGui.Button("配置分支任务线"))
                            {
                                if (_subEditorWindow == null)
                                {
                                    _subEditorWindow = new EditorWindow(Plugin);
                                    Plugin.WindowSystem.AddWindow(_subEditorWindow);
                                }
                                _subEditorWindow.IsOpen = true;
                                _subEditorWindow.RoleplayingQuestCreator.EditQuest(roleplayingQuest);
                                _subEditorWindow.RefreshMenus();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("导入分支任务线"))
                            {
                                _fileDialogManager.Reset();
                                ImGui.OpenPopup("OpenPathDialog##editorwindow");
                            }
                            if (ImGui.BeginPopup("OpenPathDialog##editorwindow"))
                            {
                                _fileDialogManager.OpenFileDialog("选择任务线数据", ".quest", (isOk, file) =>
                                {
                                    if (isOk)
                                    {
                                        item.RoleplayingQuest = _roleplayingQuestCreator.ImportQuestline(file[0]);
                                        item.RoleplayingQuest.ConfigureSubQuest(_roleplayingQuestCreator.CurrentQuest);
                                    }
                                }, 0, "", true);
                                ImGui.EndPopup();
                            }
                            break;
                    }
                }
            }
        }
    }

    private void DrawBranchingChoices()
    {
        if (_objectiveInFocus != null)
        {
            var questText = _objectiveInFocus.QuestText;
            if (questText.Count > 0)
            {
                var branchingChoices = questText[_selectedEvent].BranchingChoices;
                ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                if (ImGui.ListBox("##branchingChoice", ref _selectedBranchingChoice, _branchingChoices, _branchingChoices.Length, 12))
                {
                    RefreshMenus();
                }
                if (ImGui.Button("添加"))
                {
                    var branchingChoice = new BranchingChoice();
                    branchingChoices.Add(branchingChoice);
                    branchingChoice.RoleplayingQuest.ConfigureSubQuest(_roleplayingQuestCreator.CurrentQuest);
                    branchingChoice.RoleplayingQuest.IsSubQuest = true;
                    _branchingChoices = Utility.FillNewList(branchingChoices.Count, "选择");
                    _selectedBranchingChoice = branchingChoices.Count - 1;
                    RefreshMenus();
                }
                ImGui.SameLine();
                if (ImGui.Button("移除"))
                {
                    branchingChoices.RemoveAt(_selectedBranchingChoice);
                    _branchingChoices = Utility.FillNewList(branchingChoices.Count, "选择");
                    _selectedBranchingChoice = branchingChoices.Count - 1;
                    RefreshMenus();
                }
            }
        }
    }

    private void DrawQuestEvents()
    {
        if (_objectiveInFocus != null)
        {
            var questText = _objectiveInFocus.QuestText;
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            if (ImGui.ListBox("##questEvent", ref _selectedEvent, _dialogues, _dialogues.Length, 15))
            {
                _selectedBranchingChoice = 0;
                RefreshMenus();
            }
            if (ImGui.Button("添加"))
            {
                questText.Add(new QuestEvent());
                _dialogues = Utility.FillNewList(questText.Count, "事件");
                _selectedEvent = questText.Count - 1;
                RefreshMenus();
            }
            ImGui.SameLine();
            if (ImGui.Button("移除"))
            {
                questText.RemoveAt(_selectedEvent);
                _dialogues = Utility.FillNewList(questText.Count, "事件");
                _selectedEvent = questText.Count - 1;
                RefreshMenus();
            }
            if (ImGui.Button("添加剪贴板"))
            {
                _roleplayingQuestCreator.StoryScriptToObjectiveEvents(ImGui.GetClipboardText().Replace("…","..."), _objectiveInFocus);
                RefreshMenus();
            }
            if (ImGui.Button("复制到剪贴板"))
            {
                ImGui.SetClipboardText(_roleplayingQuestCreator.ObjectiveToStoryScriptFormat(_objectiveInFocus));
            }
        }
    }

    private void RefreshMenus()
    {
        if (_objectiveInFocus != null)
        {
            var questText = _objectiveInFocus.QuestText;
            _dialogues = Utility.FillNewList(questText.Count, "事件");
            _nodeNames = Utility.FillNewList(_roleplayingQuestCreator.CurrentQuest.QuestObjectives.Count, "目标");
            if (questText.Count > 0)
            {
                if (_selectedEvent < questText.Count)
                {
                    var choices = questText[_selectedEvent].BranchingChoices;
                    if (_selectedBranchingChoice > choices.Count)
                    {
                        _selectedBranchingChoice = choices.Count - 1;
                    }
                    _branchingChoices = Utility.FillNewList(choices.Count, "选择");
                }
            }
            else
            {
                _branchingChoices = new string[] { };
            }
            if (_subEditorWindow != null)
            {
                _subEditorWindow.RefreshMenus();
                _subEditorWindow.IsOpen = false;
            }
        }
        else
        {
            _branchingChoices = new string[] { };
            _nodeNames = new string[] { };
            _dialogues = new string[] { };
            _dialogues = Utility.FillNewList(0, "事件");
            _nodeNames = Utility.FillNewList(0, "目标");
            _branchingChoices = Utility.FillNewList(0, "选择");
            _selectedBranchingChoice = 0;
            _selectedEvent = 0;
        }
    }

    public void DrawQuestObjectivesRecursive(List<QuestObjective> questObjectives, int level)
    {
        int i = 0;
        List<QuestObjective> invalidatedObjectives = new List<QuestObjective>();
        foreach (var objective in questObjectives)
        {
            if (objective != null && !objective.Invalidate)
            {
                if (level > 0)
                {
                    objective.IsAPrimaryObjective = false;
                }
                else
                {
                    objective.Index = i;
                }
                if (ImGui.TreeNode((level == 0 ? "(" + i + ") " : "") + "" + objective.Objective + "##" + i))
                {
                    if (ImGui.Button("编辑##" + i))
                    {
                        _objectiveInFocus = objective;
                        RefreshMenus();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("添加子目标##" + i))
                    {
                        objective.SubObjectives.Add(new QuestObjective()
                        {
                            Coordinates = Plugin.ClientState.LocalPlayer.Position,
                            Rotation = new Vector3(0, CoordinateUtility.ConvertRadiansToDegrees(Plugin.ClientState.LocalPlayer.Rotation), 0),
                            TerritoryId = Plugin.ClientState.TerritoryType
                        });
                    }
                    if (!_shiftModifierHeld)
                    {
                        ImGui.BeginDisabled();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("删除##" + i))
                    {
                        objective.Invalidate = true;
                    }
                    if (!_shiftModifierHeld)
                    {
                        ImGui.EndDisabled();
                    }
                    DrawQuestObjectivesRecursive(objective.SubObjectives, level + 1);
                    ImGui.TreePop();
                }
            }
            else
            {
                invalidatedObjectives.Add(objective);
            }
            i++;
        }
        foreach (var objective in invalidatedObjectives)
        {
            questObjectives.Remove(objective);
        }
    }
    private void DrawQuestObjectives()
    {
        var width = ImGui.GetColumnWidth();
        ImGui.PushID("Vertical Scroll");
        ImGui.BeginGroup();
        const ImGuiWindowFlags child_flags = ImGuiWindowFlags.MenuBar;
        var child_id = ImGui.GetID("目标");
        bool child_is_visible = ImGui.BeginChild(child_id, new Vector2(width, 600), true, child_flags);
        DrawQuestObjectivesRecursive(_roleplayingQuestCreator.CurrentQuest.QuestObjectives, 0);
        ImGui.EndChild();
        ImGui.EndGroup();
        ImGui.PopID();
        ImGui.TextUnformatted("按住 Shift 键删除目标");
        if (ImGui.Button("添加主目标"))
        {
            _npcTransformEditorWindow.RefreshMenus();
            _roleplayingQuestCreator.AddQuestObjective(new QuestObjective()
            {
                Coordinates = Plugin.ClientState.LocalPlayer.Position,
                Rotation = new Vector3(0, CoordinateUtility.ConvertRadiansToDegrees(Plugin.ClientState.LocalPlayer.Rotation), 0),
                TerritoryId = Plugin.ClientState.TerritoryType,
                TerritoryDiscriminator = Plugin.AQuestReborn.Discriminator
            });
            RefreshMenus();
        }
    }

    private void OpenBranchingQuest(RoleplayingQuest roleplayingQuest)
    {
        _subEditorWindow.RoleplayingQuestCreator.EditQuest(roleplayingQuest);
        if (roleplayingQuest.QuestObjectives.Count > 0)
        {
            _objectiveInFocus = roleplayingQuest.QuestObjectives[0];
        }
        else
        {
            _objectiveInFocus = null;
        }
        _subEditorWindow.RefreshMenus();
    }
}
