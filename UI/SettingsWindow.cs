using Dalamud.Interface.Windowing;
using AutoRetainerSellList.Presentation.UI.ViewModels;
using Dalamud.Game.Text;
using Dalamud.Bindings.ImGui;

namespace AutoRetainerSellList.UI;

public class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;
    private string _searchQuery = string.Empty;
    private int _selectedRetainerIndex = -1;
    private bool _showItemSearch = false;
    private int _quantityInput = 1;

    public SettingsWindow(SettingsWindowViewModel viewModel)
        : base("Auto Retainer Sell List Settings##SettingsWindow")
    {
        _viewModel = viewModel;

        Size = new System.Numerics.Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;

        Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        // Language settings at the top
        ImGui.Text("Chat Log Language:");
        ImGui.SameLine();

        string[] languages = { "Japanese", "English" };
        int currentLanguageIndex = _viewModel.ChatLanguage == "English" ? 1 : 0;
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##ChatLanguage", ref currentLanguageIndex, languages, languages.Length))
        {
            _viewModel.SaveChatLanguage(languages[currentLanguageIndex]);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Left panel: Retainer list
        if (ImGui.BeginChild("RetainerList", new System.Numerics.Vector2(200, 0), true))
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Retainers");
            ImGui.Separator();

            for (int i = 0; i < _viewModel.Retainers.Count; i++)
            {
                var retainer = _viewModel.Retainers[i];
                if (ImGui.Selectable($"{retainer.Name}##{i}", _selectedRetainerIndex == i))
                {
                    // Discard any unsaved changes when switching retainers
                    if (_viewModel.HasChanges)
                    {
                        _viewModel.CancelChanges();
                    }
                    _selectedRetainerIndex = i;
                    _viewModel.LoadSellList(retainer);
                }
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // Right panel: Sell list
        if (ImGui.BeginChild("SellListPanel", new System.Numerics.Vector2(0, 0), true))
        {
            if (_viewModel.SelectedRetainer != null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), $"Sell List for {_viewModel.SelectedRetainer.Name}");
                ImGui.Text($"Items: {_viewModel.SellListItems.Count}/20");
                ImGui.Separator();
                ImGui.Spacing();

                // Add item button
                if (ImGui.Button("Add Item", new System.Numerics.Vector2(100, 25)))
                {
                    _showItemSearch = true;
                    _quantityInput = 1;
                }

                ImGui.SameLine();
                if (ImGui.Button("Clear All", new System.Numerics.Vector2(100, 25)))
                {
                    _viewModel.ClearList();
                }

                // Save/Cancel buttons - only show when there are changes
                if (_viewModel.HasChanges)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.15f, 0.5f, 0.15f, 1.0f));
                    if (ImGui.Button("Save Changes", new System.Numerics.Vector2(120, 30)))
                    {
                        _viewModel.SaveChanges();
                    }
                    ImGui.PopStyleColor(3);

                    ImGui.SameLine();

                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.2f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.7f, 0.3f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.5f, 0.15f, 0.15f, 1.0f));
                    if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 30)))
                    {
                        _viewModel.CancelChanges();
                    }
                    ImGui.PopStyleColor(3);
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Sell list table
                if (ImGui.BeginTable("SellListTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < _viewModel.SellListItems.Count; i++)
                    {
                        var item = _viewModel.SellListItems[i];

                        ImGui.TableNextRow();

                        // Item name
                        ImGui.TableNextColumn();
                        ImGui.Text(item.ItemName);

                        // Quantity
                        ImGui.TableNextColumn();
                        int quantity = item.QuantityToMaintain;
                        ImGui.SetNextItemWidth(70);
                        if (ImGui.InputInt($"##qty{i}", ref quantity, 1, 1))
                        {
                            if (quantity >= 1 && quantity <= 999)
                            {
                                _viewModel.UpdateQuantity(item.Guid, quantity);
                            }
                        }

                        // Delete button
                        ImGui.TableNextColumn();
                        if (ImGui.Button($"Delete##{i}", new System.Numerics.Vector2(50, 20)))
                        {
                            _viewModel.RemoveItem(item.Guid);
                        }
                    }

                    ImGui.EndTable();
                }

                // Item search popup
                if (_showItemSearch)
                {
                    ImGui.OpenPopup("Add Item##AddItemPopup");
                }

                if (ImGui.BeginPopupModal("Add Item##AddItemPopup", ref _showItemSearch, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Search for an item:");
                    ImGui.SetNextItemWidth(400);
                    if (ImGui.InputText("##ItemSearch", ref _searchQuery, 100))
                    {
                        _viewModel.SearchItems(_searchQuery);
                    }

                    ImGui.Spacing();

                    ImGui.Text("Quantity:");
                    ImGui.SetNextItemWidth(100);
                    ImGui.InputInt("##Quantity", ref _quantityInput, 1, 1);
                    if (_quantityInput < 1) _quantityInput = 1;
                    if (_quantityInput > 999) _quantityInput = 999;

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Search results
                    if (ImGui.BeginChild("SearchResults", new System.Numerics.Vector2(400, 300), true))
                    {
                        for (int i = 0; i < _viewModel.SearchResults.Count; i++)
                        {
                            var result = _viewModel.SearchResults[i];
                            if (ImGui.Selectable($"{result.ItemName}##{i}"))
                            {
                                _viewModel.AddItem(result.ItemId, result.ItemName, _quantityInput);
                                _showItemSearch = false;
                                _searchQuery = string.Empty;
                            }
                        }
                    }
                    ImGui.EndChild();

                    ImGui.Spacing();

                    if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
                    {
                        _showItemSearch = false;
                        _searchQuery = string.Empty;
                    }

                    ImGui.EndPopup();
                }
            }
            else
            {
                ImGui.TextWrapped("Select a retainer from the left panel to configure their sell list.");
            }
        }
        ImGui.EndChild();
    }

    public override void OnOpen()
    {
        _viewModel.LoadRetainers();
        if (_viewModel.Retainers.Count > 0 && _selectedRetainerIndex == -1)
        {
            _selectedRetainerIndex = 0;
            _viewModel.LoadSellList(_viewModel.Retainers[0]);
        }
    }
}

