﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using SimpleHeels.Files;
using Action = System.Action;
using World = Lumina.Excel.GeneratedSheets.World;

namespace SimpleHeels; 

public class ConfigWindow : Window {
    private readonly PluginConfig config;
    private readonly Plugin plugin;

    public ConfigWindow(string name, Plugin plugin, PluginConfig config) : base(name) {
        this.config = config;
        this.plugin = plugin;
        
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(800, 400),
            MaximumSize = new Vector2(float.MaxValue)
        };

        Size = ImGuiHelpers.ScaledVector2(1000, 500);
        SizeCondition = ImGuiCond.FirstUseEver;

    }
    
    private Vector2 iconButtonSize = new(16);
    private float checkboxSize = 36;

    


    public void DrawCharacterList() {

        foreach (var (worldId, characters) in config.WorldCharacterDictionary.ToArray()) {
            var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(worldId);
            if (world == null) continue;
            
            ImGui.TextDisabled($"{world.Name.RawString}");
            ImGui.Separator();

            foreach (var (name, characterConfig) in characters.ToArray()) {
                if (ImGui.Selectable($"{name}##{world.Name.RawString}", selectedCharacter == characterConfig)) {
                    selectedCharacter = characterConfig;
                    selectedName = name;
                    selectedWorld = world.RowId;
                }
                
                if (ImGui.BeginPopupContextItem()) {
                    if (ImGui.Selectable($"Remove '{name} @ {world.Name.RawString}' from Config")) {
                        characters.Remove(name);
                        if (selectedCharacter == characterConfig) selectedCharacter = null;
                        if (characters.Count == 0) {
                            config.WorldCharacterDictionary.Remove(worldId);
                        }
                    }
                    ImGui.EndPopup();
                }
            }

            ImGuiHelpers.ScaledDummy(10);
        }

        if (Plugin.IsDebug && Plugin.IpcAssignedOffset.Count > 0) {
            ImGui.TextDisabled("[DEBUG] IPC Assignments");
            ImGui.Separator();

            foreach (var (name, worldId) in Plugin.IpcAssignedOffset.Keys.ToArray()) {
                var world = PluginService.Data.GetExcelSheet<World>()?.GetRow(worldId);
                if (world == null) continue;
                if (ImGui.Selectable($"{name}##{world.Name.RawString}##ipc", selectedName == name && selectedWorld == worldId)) {
                    config.TryGetCharacterConfig(name, worldId, out selectedCharacter);
                    selectedCharacter ??= new CharacterConfig();
                    selectedName = name;
                    selectedWorld = world.RowId;
                }
                ImGui.SameLine();
                ImGui.TextDisabled(world.Name.ToDalamudString().TextValue);
            }
            
            ImGuiHelpers.ScaledDummy(10);
        }

    }

    private CharacterConfig? selectedCharacter;
    private string selectedName = string.Empty;
    private uint selectedWorld;

    private void ShowDebugInfo() {
        if (Plugin.IsDebug && ImGui.TreeNode("DEBUG INFO")) {
            try {
                var activePlayer = PluginService.Objects.FirstOrDefault(t => t is PlayerCharacter playerCharacter && playerCharacter.Name.TextValue == selectedName && playerCharacter.HomeWorld.Id == selectedWorld);

                if (activePlayer is not PlayerCharacter pc) {
                    ImGui.TextDisabled("Character is not currently in world.");
                    return;
                }
                
                ImGui.TextDisabled($"Character: {pc:X8}");
                if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{pc.Address:X}");

                unsafe {
                    var obj = (GameObject*)activePlayer.Address;
                    var character = (Character*)obj;
                    Util.ShowStruct(character);
                    var realPosition = obj->Position;
                    if (obj->DrawObject == null) {
                        ImGui.TextDisabled("Character is not currently being drawn.");
                        return;
                    }

                    var drawPosition = obj->DrawObject->Object.Position;

                    ImGui.Text($"Actual Y Position: {realPosition.Y}");
                    ImGui.Text($"Drawn Y Position: {drawPosition.Y}");
                    if (ImGui.IsItemClicked()) {
                        ImGui.SetClipboardText($"{(ulong)(&obj->DrawObject->Object.Position.Y):X}");
                    }

                    ImGui.Text($"Active Offset: {drawPosition.Y - realPosition.Y}");
                    ImGui.Text($"Expected Offset: {plugin.GetOffset(obj)}");

                    ImGui.Text($"Height: {obj->GetHeight()}");

                    ImGui.Text($"Object Type: {obj->DrawObject->Object.GetObjectType()}");
                    if (obj->DrawObject->Object.GetObjectType() == ObjectType.CharacterBase) {
                        var characterBase = (CharacterBase*)obj->DrawObject;
                        ImGui.Text($"Model Type: {characterBase->GetModelType()}");
                        if (characterBase->GetModelType() == CharacterBase.ModelType.Human) {
                            var human = (Human*)obj->DrawObject;
                            ImGui.Text("Active Models:");
                            ImGui.Indent();
                            ImGui.Text("Top:");
                            ImGui.Indent();
                            ImGui.Text($"ID: {human->Top.Id}, {human->Top.Variant}");
                            ImGui.Text($"Name: {GetModelName(human->Top.Id, ModelSlot.Top, true) ?? "Does not replace feet"}");
                            ImGui.Text($"Path: {Plugin.GetModelPath(human, ModelSlot.Top)}");
                            ImGui.Unindent();
                            ImGui.Text("Legs:");
                            ImGui.Indent();
                            ImGui.Text($"ID: {human->Legs.Id}, {human->Legs.Variant}");
                            ImGui.Text($"Name: {GetModelName(human->Legs.Id, ModelSlot.Legs, true) ?? "Does not replace feet"}");
                            ImGui.Text($"Path: {Plugin.GetModelPath(human, ModelSlot.Legs)}");
                            ImGui.Unindent();
                            ImGui.Text("Feet:");
                            ImGui.Indent();
                            ImGui.Text($"ID: {human->Feet.Id}, {human->Feet.Variant}");
                            ImGui.Text($"Name: {GetModelName(human->Feet.Id, ModelSlot.Feet)}");
                            ImGui.Text($"Path: {Plugin.GetModelPath(human, ModelSlot.Feet)}");
                            ImGui.Unindent();
                            
                            ImGui.Unindent();
                        } else {
                            ImGui.TextDisabled("Player is not a 'Human'");
                        }
                    } else {
                        ImGui.TextDisabled("Player is not a 'Character'");
                    }
                }
                
            } finally {
                ImGui.TreePop();
            }
        }
                
    }
    
    public override void Draw() {
        if (!Plugin.IsEnabled) {
            ImGui.TextColored(ImGuiColors.DalamudRed, $"{plugin.Name} is currently disabled due to Heels Plugin being installed.\nPlease uninstall Heels Plugin to allow {plugin.Name} to run.");
            return;
        }

        if (!config.Enabled) {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudRed * new Vector4(1, 1, 1, 0.3f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGuiColors.DalamudRed * new Vector4(1, 1, 1, 0.3f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGuiColors.DalamudRed * new Vector4(1, 1, 1, 0.3f));
            ImGui.Button($"{plugin.Name} is currently disabled. No offsets will be applied.", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale));
            ImGui.PopStyleColor(3);
        }
        

        ImGui.BeginGroup();
        {
            if (ImGui.BeginChild("character_select", ImGuiHelpers.ScaledVector2(240, 0) - iconButtonSize with { X = 0 }, true)) {
                DrawCharacterList();
            }
            ImGui.EndChild();

            if (PluginService.ClientState.LocalPlayer != null) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.User)) {
                    if (PluginService.ClientState.LocalPlayer != null) {
                        config.TryAddCharacter(PluginService.ClientState.LocalPlayer.Name.TextValue, PluginService.ClientState.LocalPlayer.HomeWorld.Id);
                    }
                }
                
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add current character");
                
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.DotCircle)) {
                    if (PluginService.Targets.Target is PlayerCharacter pc) {
                        config.TryAddCharacter(pc.Name.TextValue, pc.HomeWorld.Id);
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add targeted character");
                ImGui.SameLine();
            }
            
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog)) {
                selectedCharacter = null;
                selectedName = string.Empty;
                selectedWorld = 0;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Plugin Options");
            iconButtonSize = ImGui.GetItemRectSize() + ImGui.GetStyle().ItemSpacing;
        }
        ImGui.EndGroup();
        
        ImGui.SameLine();
        if (ImGui.BeginChild("character_view", ImGuiHelpers.ScaledVector2(0), true)) {
            if (selectedCharacter != null) {
                ShowDebugInfo();
                if (Plugin.IpcAssignedOffset.TryGetValue((selectedName, selectedWorld), out var offset)) {

                    ImGui.Text("This character's offset is currently assigned by another plugin.");
                    if (Plugin.IsDebug && ImGui.Button("Clear IPC Assignment")) {
                        Plugin.IpcAssignedOffset.Remove((selectedName, selectedWorld));
                    }
                    
                    ImGui.Text($"Assigned Offset: {offset}");
                    
                    
                    return;
                }
                
                DrawCharacterView(selectedCharacter);
            } else {

                Changelog.Show(config);
                
                ImGui.Text("SimpleHeels Options");
                ImGui.Separator();

                if (ImGui.Checkbox("Enabled", ref config.Enabled)) {
                    Plugin.RequestUpdateAll();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Can be toggled using commands:\n\t/heels toggle\n\t/heels enable\n\t/heels disable");
                if (ImGui.Checkbox("Use model assigned offsets", ref config.UseModelOffsets)) {
                    Plugin.RequestUpdateAll();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Allows mod developers to assign an offset to a modded item.\nClick this for more information.");
                if (ImGui.IsItemClicked()) {
                    Util.OpenLink("https://github.com/Caraxi/SimpleHeels/blob/master/modguide.md");
                }

                ImGui.Checkbox("Show Plus/Minus buttons for offset adjustments", ref config.ShowPlusMinusButtons);
                if (config.ShowPlusMinusButtons) {
                    ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
                    ImGui.SliderFloat("Plus/Minus Button Delta", ref config.PlusMinusDelta, 0.0001f, 0.01f, "%.4f", ImGuiSliderFlags.AlwaysClamp);
                }
                
                #if DEBUG
                ImGui.Checkbox("[DEBUG] Open config window on startup", ref config.DebugOpenOnStartup);
                #endif

                if (config.UseModelOffsets && ImGui.CollapsingHeader("Model Offset Editor")) {
                    ShowModelEditor();
                }
            }
            
        }
        ImGui.EndChild();
    }

    private static FileDialogManager? _fileDialogManager;
    private float mdlEditorOffset = 0f;
    private Exception? mdlEditorException;
    private MdlFile? loadedFile;
    private string loadedFilePath = string.Empty;
    
    private void ShowModelEditor() {
        if (mdlEditorException != null) {
            ImGui.TextColored(ImGuiColors.DalamudRed, $"{mdlEditorException}");
        } else {
            ImGui.TextWrapped("This is a very simple editor that allows setting the offset for a mdl file.");
            ImGui.Spacing();
            ImGui.Text("- Select a file");
            ImGui.Text("- Set Offset");
            ImGui.Text("- Save the modified file");
            ImGui.Spacing();
            
            if (loadedFile != null) {
                var attributes = loadedFile.Attributes.ToList();
                
                
                ImGui.InputText("Loaded File", ref loadedFilePath, 2048, ImGuiInputTextFlags.ReadOnly);
                ImGui.SliderFloat("Heels Offset", ref mdlEditorOffset, -1, 1, "%.5f", ImGuiSliderFlags.AlwaysClamp);
                
                var offset = attributes.FirstOrDefault(a => a.StartsWith("heels_offset="));
                if (offset == null) {
                    ImGui.Text("Model has no offset assigned.");
                } else {
                    ImGui.Text($"Current Offset: {offset[13..]}");
                }
                
                if (ImGui.Button("Save MDL File")) {
                    if (_fileDialogManager == null) {
                        _fileDialogManager = new FileDialogManager();
                        PluginService.PluginInterface.UiBuilder.Draw += _fileDialogManager.Draw;
                    }

                    try {
                        _fileDialogManager.SaveFileDialog("Save MDL File...", "MDL File{.mdl}", "output.mdl", ".mdl", (b, files) => {
                            attributes.RemoveAll(a => a.StartsWith("heels_offset="));
                            attributes.Add($"heels_offset={mdlEditorOffset}");
                            loadedFile.Attributes = attributes.ToArray();
                            var outputBytes = loadedFile.Write();
                            File.WriteAllBytes(files, outputBytes);
                            loadedFile = null;
                        }, Path.GetDirectoryName(loadedFilePath), true);
                    } catch (Exception ex) {
                        mdlEditorException = ex;
                    }
                }

                if (ImGui.Button("Cancel")) {
                    loadedFile = null;
                }
                
            } else {
                if (ImGui.Button("Select MDL File")) {
                    if (_fileDialogManager == null) {
                        _fileDialogManager = new FileDialogManager();
                        PluginService.PluginInterface.UiBuilder.Draw += _fileDialogManager.Draw;
                    }

                    try {
                        _fileDialogManager.OpenFileDialog("Select MDL File...", "MDL File{.mdl}", (b, files) => {
                            if (files.Count != 1) return;
                            loadedFilePath = files[0];
                            PluginLog.Log($"Loading MDL: {loadedFilePath}");
                           
                            config.ModelEditorLastFolder = Path.GetDirectoryName(loadedFilePath) ?? string.Empty;
                            var bytes = File.ReadAllBytes(loadedFilePath);
                            loadedFile = new MdlFile(bytes);
                            var attributes = loadedFile.Attributes.ToList();
                            var offset = attributes.FirstOrDefault(a => a.StartsWith("heels_offset="));
                        
                            if (offset != null) {
                                if (!float.TryParse(offset[13..], out mdlEditorOffset)) {
                                    mdlEditorOffset = 0;
                                }
                            }

                        }, 1, config.ModelEditorLastFolder, true);
                    } catch (Exception ex) {
                        mdlEditorException = ex;
                    }
                }
            }
        }
    }

    private string footwearSearch = string.Empty;
    
    private void DrawCharacterView(CharacterConfig? characterConfig) {
        if (characterConfig == null) return;
        
        var activeFootwear = GetModelIdForPlayer(selectedName, selectedWorld, ModelSlot.Feet);
        var activeFootwearPath = GetModelPathForPlayer(selectedName, selectedWorld, ModelSlot.Feet);
        
        var activeTop = GetModelIdForPlayer(selectedName, selectedWorld, ModelSlot.Top);
        var activeTopPath = GetModelPathForPlayer(selectedName, selectedWorld, ModelSlot.Top);
        
        var activeLegs = GetModelIdForPlayer(selectedName, selectedWorld, ModelSlot.Legs);
        var activeLegsPath = GetModelPathForPlayer(selectedName, selectedWorld, ModelSlot.Legs);
        
        if (ImGui.BeginTable("OffsetsTable", 5)) {
            ImGui.TableSetupColumn("Enable", ImGuiTableColumnFlags.WidthFixed, checkboxSize * 2 + 1);
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 120 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Offset", ImGuiTableColumnFlags.WidthFixed, (90 + (config.ShowPlusMinusButtons ? 50 : 0)) * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Footwear", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, checkboxSize);
            ImGui.TableHeadersRow();

            var deleteIndex = -1;
            for (var i = 0; i < characterConfig.HeelsConfig.Count; i++) {
                ImGui.PushID($"heels_{i}");
                var heelConfig = characterConfig.HeelsConfig[i];
                heelConfig.Label ??= string.Empty;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.One);
                ImGui.PushFont(UiBuilder.IconFont);

                if (ImGui.Button($"{(char)FontAwesomeIcon.Trash}##delete", new Vector2(checkboxSize)) && ImGui.GetIO().KeyShift) {
                    deleteIndex = i;
                }

                if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift) {
                    ImGui.PopFont();
                    ImGui.SetTooltip("Hold SHIFT to delete.");
                    ImGui.PushFont(UiBuilder.IconFont);
                }

                ImGui.SameLine();
                ImGui.PopFont();
                ImGui.PopStyleVar();
                if (ImGui.Checkbox("##enable", ref heelConfig.Enabled)) {
                    if (heelConfig.Enabled) {
                        foreach (var heel in characterConfig.HeelsConfig.Where(h => h.ModelId == heelConfig.ModelId)) {
                            heel.Enabled = false;
                        }
                        heelConfig.Enabled = true;
                    }
                }

                if (i == 0) checkboxSize = ImGui.GetItemRectSize().X;

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##label", ref heelConfig.Label, 100);
                
                ImGui.TableNextColumn();

                if (plugin.Config.ShowPlusMinusButtons) {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Minus)) {
                        heelConfig.Offset -= plugin.Config.PlusMinusDelta;
                        if (heelConfig.Enabled) Plugin.RequestUpdateAll();
                    }
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetItemRectSize().X - ImGui.GetStyle().ItemSpacing.X);
                    if (ImGui.DragFloat("##offset", ref heelConfig.Offset, 0.001f, -1, 1, "%.4f", ImGuiSliderFlags.AlwaysClamp)) {
                        if (heelConfig.Enabled) Plugin.RequestUpdateAll();
                    }
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                        heelConfig.Offset += plugin.Config.PlusMinusDelta;
                        if (heelConfig.Enabled) Plugin.RequestUpdateAll();
                    }
                    
                    
                } else {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.DragFloat("##offset", ref heelConfig.Offset, 0.001f, -1, 1, "%.4f", ImGuiSliderFlags.AlwaysClamp)) {
                        if (heelConfig.Enabled) Plugin.RequestUpdateAll();
                    }
                }
                
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                
                var pathMode = heelConfig.PathMode;
                
                if (ImGui.BeginCombo("##footwear", pathMode ? ((heelConfig.Slot != ModelSlot.Feet ? $"[{heelConfig.Slot}] " : "") + heelConfig.Path) : GetModelName(heelConfig.ModelId, heelConfig.Slot), ImGuiComboFlags.HeightLargest)) {
                    if (ImGui.BeginTabBar("##footwear_tabs")) {
                        if (pathMode) {
                            if (ImGui.TabItemButton("Model ID")) {
                                heelConfig.PathMode = false;
                                (heelConfig.Slot, heelConfig.RevertSlot) = (heelConfig.RevertSlot, heelConfig.Slot);
                            }
                        }
                        
                        if (ImGui.BeginTabItem((pathMode ? "Model Path" : "Model ID") + "###currentConfigType")) {
                            if (pathMode) {
                                ImGui.TextWrapped("Assign offset based on the file path of the model, this can be a game path or a penumbra mod path.");
                                ImGui.TextDisabled("File Path:");
                                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                ImGui.InputText("##pathInput", ref heelConfig.Path, 1024);

                                ImGui.TextDisabled("Equip Slot:");
                                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                if (ImGui.BeginCombo("##slotInput", $"{heelConfig.Slot}")) {
                                    if (ImGui.Selectable($"{ModelSlot.Top}", heelConfig.Slot == ModelSlot.Top)) heelConfig.Slot = ModelSlot.Top;
                                    if (ImGui.Selectable($"{ModelSlot.Legs}", heelConfig.Slot == ModelSlot.Legs)) heelConfig.Slot = ModelSlot.Legs;
                                    if (ImGui.Selectable($"{ModelSlot.Feet}", heelConfig.Slot == ModelSlot.Feet)) heelConfig.Slot = ModelSlot.Feet;
                                    ImGui.EndCombo();
                                }
                                
                                var activeSlotPath = heelConfig.Slot switch {
                                    ModelSlot.Top => activeTopPath,
                                    ModelSlot.Legs => activeLegsPath,
                                    _ => activeFootwearPath,
                                };
                                
                                if (activeSlotPath != null) {
                                    if (ImGui.Button("Current Model Path")) {
                                        heelConfig.Path = activeSlotPath;
                                    }

                                    if (ImGui.IsItemHovered()) {
                                        ImGui.SetTooltip(activeSlotPath);
                                    }
                                }
                                
                                
                                
                            } else {
                                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                if (ImGui.IsWindowAppearing()) {
                                    footwearSearch = string.Empty;
                                    ImGui.SetKeyboardFocusHere();
                                }
                                ImGui.InputTextWithHint("##footwearSearch", "Search...", ref footwearSearch, 100);
                    
                                if (ImGui.BeginChild("##footwearSelectScroll", new Vector2(-1, 200))) {
                                    foreach (var shoeModel in shoeModelList.Value.Values) {
                                        if (!string.IsNullOrWhiteSpace(footwearSearch)) {
                                            if (!((shoeModel.Name ?? $"Unknown#{shoeModel.Id}").Contains(footwearSearch, StringComparison.InvariantCultureIgnoreCase) || shoeModel.Items.Any(shoeItem => shoeItem.Name.ToDalamudString().TextValue.Contains(footwearSearch, StringComparison.InvariantCultureIgnoreCase)))) {
                                                continue;
                                            }
                                        }
                            
                                        if (ImGui.Selectable($"{shoeModel.Name}##shoeModel_{shoeModel.Id}")) {
                                            heelConfig.ModelId = shoeModel.Id;
                                            heelConfig.Slot = shoeModel.Slot;
                                            ImGui.CloseCurrentPopup();
                                        }

                                        if (ImGui.IsItemHovered() && shoeModel.Items.Count > 3) {
                                            ShowModelTooltip(shoeModel.Id, shoeModel.Slot);
                                        }
                                    }
                                }
                    
                                ImGui.EndChild();
                            }
                            
                            ImGui.EndTabItem();
                        }
                            
                        if (!pathMode) {
                            if (ImGui.TabItemButton("Model Path")) {
                                heelConfig.PathMode = true;
                                (heelConfig.Slot, heelConfig.RevertSlot) = (heelConfig.RevertSlot, heelConfig.Slot);
                            }
                        }
                        
                        ImGui.EndTabBar();
                    }
                    
                    
                    
                    ImGui.EndCombo();
                }
                
                ImGui.TableNextColumn();

                if ((heelConfig.Slot == ModelSlot.Feet && ((heelConfig.PathMode == false && activeFootwear == heelConfig.ModelId) || (heelConfig.PathMode && activeFootwearPath != null && activeFootwearPath.Equals(heelConfig.Path, StringComparison.OrdinalIgnoreCase)))) 
                    || (heelConfig.Slot == ModelSlot.Legs && ((heelConfig.PathMode == false && activeLegs == heelConfig.ModelId) || (heelConfig.PathMode && activeLegsPath != null && activeLegsPath.Equals(heelConfig.Path, StringComparison.OrdinalIgnoreCase)))) 
                    || (heelConfig.Slot == ModelSlot.Top && ((heelConfig.PathMode == false && activeTop == heelConfig.ModelId) || (heelConfig.PathMode && activeTopPath != null && activeTopPath.Equals(heelConfig.Path, StringComparison.OrdinalIgnoreCase))))) {
                    
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text($"{(char)FontAwesomeIcon.ArrowLeft}");
                    ImGui.PopFont();
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip("Currently Wearing");
                    }
                }

                ImGui.PopID();
            }

            if (deleteIndex >= 0) {
                characterConfig.HeelsConfig.RemoveAt(deleteIndex);
            }

            ImGui.EndTable();

            bool ShowAddButton(ushort id, ModelSlot slot) {
                if (shoeModelList.Value.ContainsKey((id, slot))) {
                    if (ImGui.Button($"Add an Entry for {GetModelName(id, slot)}")) {
                        characterConfig.HeelsConfig.Add(new HeelConfig() {
                            ModelId = activeTop,
                            Slot = ModelSlot.Top
                        });
                    }
                    return true;
                }
                return false;
            }

            var _ = ShowAddButton(activeTop, ModelSlot.Top) || ShowAddButton(activeLegs, ModelSlot.Legs) || ShowAddButton(activeFootwear, ModelSlot.Feet);

            if (characterConfig.HeelsConfig.Count > 0) return;
            
            heelsConfigPath ??= Path.Join(PluginService.PluginInterface.ConfigFile.DirectoryName, "HeelsPlugin.json");
            heelsConfigExists ??= File.Exists(heelsConfigPath);
            if (heelsConfigExists is true && ImGui.Button("Import from Heels Plugin")) {
                var heelsJson = File.ReadAllText(heelsConfigPath);
                var heelsConfig = JsonConvert.DeserializeObject<HeelsPlugin.Configuration>(heelsJson);
                if (heelsConfig == null) {
                    heelsConfigExists = false;
                } else {
                    foreach (var e in heelsConfig.Configs) {
                        characterConfig.HeelsConfig.Add(new HeelConfig() {
                            Enabled = e.Enabled,
                            Label = e.Name ?? string.Empty,
                            ModelId = (ushort)e.ModelMain,
                            Offset = e.Offset,
                        });
                    }
                }
            }
        }
    }

    private string? heelsConfigPath;
    private bool? heelsConfigExists;
    
    

    private readonly Lazy<Dictionary<(ushort, ModelSlot), ShoeModel>> shoeModelList = new(() => {
        var dict = new Dictionary<(ushort, ModelSlot), ShoeModel> {
            [(0, ModelSlot.Feet)] = new() { Id = 0, Name = "Smallclothes (Barefoot)"},
        };

        foreach (var item in PluginService.Data.GetExcelSheet<Item>()!.Where(i => i.EquipSlotCategory?.Value?.Feet != 0)) {
            if (item.ItemUICategory.Row is not (35 or 36 or 38)) continue;
            
            var modelBytes = BitConverter.GetBytes(item.ModelMain);
            var modelId = BitConverter.ToUInt16(modelBytes, 0);

            var slot = item.ItemUICategory.Row switch {
                35 => ModelSlot.Top,
                36 => ModelSlot.Legs,
                _ => ModelSlot.Feet
            };
            
            if (!dict.ContainsKey((modelId, slot))) dict.Add((modelId, slot), new ShoeModel {
                Id = modelId, Slot = slot
            });
            
            dict[(modelId, slot)].Items.Add(item);
            dict[(modelId, slot)].Name = null;
        }

        return dict;
    });

    private class ShoeModel {
        public ushort Id;
        public ModelSlot Slot;
        public readonly List<Item> Items = new();

        private string? nameCache;
        public string? Name {
            get {
                if (nameCache != null) return nameCache;
                nameCache = Items.Count switch {
                    0 => $"Unknwon#{Id}",
                    1 => Items[0].Name.ToDalamudString().TextValue,
                    2 => string.Join(" and ", Items.Select(i => i.Name.ToDalamudString().TextValue)),
                    > 3 => $"{Items[0].Name.ToDalamudString().TextValue} & {Items.Count - 1} others.",
                    _ => string.Join(", ", Items.Select(i => i.Name.ToDalamudString().TextValue))
                };

                switch (Slot) {
                    case ModelSlot.Legs: {
                        nameCache = $"[Legs] {nameCache}";
                        break;
                    }
                    case ModelSlot.Top: {
                        nameCache = $"[Top] {nameCache}";
                        break;
                    }
                }
                

                return nameCache;
            }
            set => nameCache = value;
        }
    }

    private static unsafe ushort GetModelIdForPlayer(string name, uint world, ModelSlot slot) {
        var player = PluginService.Objects.FirstOrDefault(t => t is PlayerCharacter playerCharacter && playerCharacter.Name.TextValue == name && playerCharacter.HomeWorld.Id == world);
        if (player is not PlayerCharacter) return 0;
        var obj = (GameObject*)player.Address;
        if (obj->DrawObject == null) return 0;
        if (obj->DrawObject->Object.GetObjectType() != ObjectType.CharacterBase) return 0;
        var characterBase = (CharacterBase*)obj->DrawObject;
        if (characterBase->GetModelType() != CharacterBase.ModelType.Human) return 0;
        var human = (Human*)obj->DrawObject;
        if (human == null) return 0;
        return slot switch {
            ModelSlot.Feet => human->Feet.Id,
            ModelSlot.Top => human->Top.Id,
            ModelSlot.Legs => human->Legs.Id,
            _ => 0,
        };
    }
    
    private static unsafe string? GetModelPathForPlayer(string name, uint world, ModelSlot slot) {
        var player = PluginService.Objects.FirstOrDefault(t => t is PlayerCharacter playerCharacter && playerCharacter.Name.TextValue == name && playerCharacter.HomeWorld.Id == world);
        if (player is not PlayerCharacter) return null;
        var obj = (GameObject*)player.Address;
        if (obj->DrawObject == null) return null;
        if (obj->DrawObject->Object.GetObjectType() != ObjectType.CharacterBase) return null;
        var characterBase = (CharacterBase*)obj->DrawObject;
        if (characterBase->GetModelType() != CharacterBase.ModelType.Human) return null;
        var human = (Human*)obj->DrawObject;
        if (human == null) return null;
        if ((byte)slot > human->CharacterBase.SlotCount) return null;
        var modelArray = human->CharacterBase.Models;
        if (modelArray == null) return null;
        var feetModel = modelArray[(byte) slot];
        if (feetModel == null) return null;
        var modelResource = feetModel->ModelResourceHandle;
        if (modelResource == null) return null;

        return modelResource->ResourceHandle.FileName.ToString();
    }
    
    private string? GetModelName(ushort modelId, ModelSlot slot, bool nullOnNoMatch = false) {
        if (modelId == 0) return "Smallclothes" + (slot == ModelSlot.Feet ? " (Barefoot)" : "");

        if (shoeModelList.Value.TryGetValue((modelId, slot), out var shoeModel)) {
            return shoeModel.Name ?? $"Unknown#{modelId}";
        }
        
        return nullOnNoMatch ? null : $"Unknown#{modelId}";
    }

    private void ShowModelTooltip(ushort modelId, ModelSlot slot) {
        
        ImGui.BeginTooltip();

        try {
            if (modelId == 0) {
                ImGui.Text("Smallclothes (Barefoot)");
                return;
            }

            if (shoeModelList.Value.TryGetValue((modelId, slot), out var shoeModel)) {

                foreach (var i in shoeModel.Items) {
                    ImGui.Text($"{i.Name.ToDalamudString().TextValue}");
                }
                
            } else {
                ImGui.Text($"Unknown Item (Model#{modelId})");
            }
        } finally {
            ImGui.EndTooltip();
        }
    }

    public override void OnClose() {
        PluginService.PluginInterface.SavePluginConfig(config);
        base.OnClose();
    }
}
