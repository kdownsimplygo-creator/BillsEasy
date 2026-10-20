using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class ItemGroupEditViewModel : ViewModelBase
{
    private readonly IItemGroupService _itemGroupService;
    private readonly Guid _companyId;
    private readonly ItemGroupDto? _existingGroup;

    [ObservableProperty]
    private string _dialogTitle = "Add Item Group";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ItemGroupLookupDto? _selectedParentGroup;

    [ObservableProperty]
    private ObservableCollection<ItemGroupLookupDto> _parentGroupOptions = new();

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private bool _isDefaultSystemGroup;

    [ObservableProperty]
    private string? _errorMessage;

    public ItemGroupDto? SavedGroup { get; private set; }
    public bool IsSaved { get; private set; }

    public event Action? RequestClose;

    public ItemGroupEditViewModel(
        IItemGroupService itemGroupService,
        Guid companyId,
        ItemGroupDto? existingGroup = null)
    {
        _itemGroupService = itemGroupService;
        _companyId = companyId;
        _existingGroup = existingGroup;

        if (_existingGroup != null)
        {
            DialogTitle = $"Modify Item Group — {_existingGroup.Name}";
            Name = _existingGroup.Name;
            Description = _existingGroup.Description;
            IsDefaultSystemGroup = _existingGroup.IsDefaultSystemGroup;
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            var lookups = await _itemGroupService.GetLookupGroupsAsync(_companyId, _existingGroup?.Id);
            ParentGroupOptions = new ObservableCollection<ItemGroupLookupDto>(lookups);

            if (_existingGroup?.ParentGroupId != null)
            {
                SelectedParentGroup = ParentGroupOptions.FirstOrDefault(g => g.Id == _existingGroup.ParentGroupId.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load parent groups: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Item group name is required.";
            return;
        }

        try
        {
            if (_existingGroup == null)
            {
                var dto = new CreateItemGroupDto(
                    _companyId,
                    Name.Trim(),
                    SelectedParentGroup?.Id,
                    Description?.Trim());

                SavedGroup = await _itemGroupService.CreateGroupAsync(dto);
            }
            else
            {
                var dto = new UpdateItemGroupDto(
                    _existingGroup.Id,
                    _companyId,
                    Name.Trim(),
                    SelectedParentGroup?.Id,
                    Description?.Trim());

                SavedGroup = await _itemGroupService.UpdateGroupAsync(dto);
            }

            IsSaved = true;
            RequestClose?.Invoke();
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving item group: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        IsSaved = false;
        RequestClose?.Invoke();
    }
}
