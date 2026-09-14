using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class AccountGroupEditViewModel : ViewModelBase
{
    private readonly IAccountGroupService _accountGroupService;
    private readonly Guid _companyId;
    private readonly AccountGroupDto? _existingGroup;

    [ObservableProperty]
    private string _dialogTitle = "Add Account Group";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private AccountGroupLookupDto? _selectedParentGroup;

    [ObservableProperty]
    private ObservableCollection<AccountGroupLookupDto> _parentGroupOptions = new();

    [ObservableProperty]
    private GroupNature _selectedNature = GroupNature.Asset;

    [ObservableProperty]
    private ObservableCollection<GroupNature> _natureOptions = new();

    [ObservableProperty]
    private bool _isNatureEnabled = true;

    [ObservableProperty]
    private bool _isDefaultSystemGroup;

    [ObservableProperty]
    private string? _errorMessage;

    public AccountGroupDto? SavedGroup { get; private set; }
    public bool IsSaved { get; private set; }

    public event Action? RequestClose;

    public AccountGroupEditViewModel(
        IAccountGroupService accountGroupService,
        Guid companyId,
        AccountGroupDto? existingGroup = null)
    {
        _accountGroupService = accountGroupService;
        _companyId = companyId;
        _existingGroup = existingGroup;

        NatureOptions = new ObservableCollection<GroupNature>(Enum.GetValues<GroupNature>());

        if (existingGroup != null)
        {
            DialogTitle = $"Modify Account Group — {existingGroup.Name}";
            Name = existingGroup.Name;
            SelectedNature = existingGroup.Nature;
            IsDefaultSystemGroup = existingGroup.IsDefaultSystemGroup;
        }
        else
        {
            DialogTitle = "Add Account Group";
            SelectedNature = GroupNature.Asset;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var lookups = await _accountGroupService.GetLookupGroupsAsync(_companyId, _existingGroup?.Id);
            
            // Add a None option for primary root groups
            var list = new ObservableCollection<AccountGroupLookupDto>
            {
                new AccountGroupLookupDto(Guid.Empty, "(Primary Root Group — No Parent)", SelectedNature, SelectedNature.ToString(), null, false)
            };

            foreach (var item in lookups)
            {
                list.Add(item);
            }

            ParentGroupOptions = list;

            if (_existingGroup?.ParentGroupId.HasValue == true)
            {
                SelectedParentGroup = ParentGroupOptions.FirstOrDefault(p => p.Id == _existingGroup.ParentGroupId.Value);
            }
            else
            {
                SelectedParentGroup = ParentGroupOptions.FirstOrDefault(p => p.Id == Guid.Empty);
            }

            UpdateNatureState();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading parent groups: {ex.Message}";
        }
    }

    partial void OnSelectedParentGroupChanged(AccountGroupLookupDto? value)
    {
        UpdateNatureState();
    }

    private void UpdateNatureState()
    {
        if (SelectedParentGroup != null && SelectedParentGroup.Id != Guid.Empty)
        {
            SelectedNature = SelectedParentGroup.Nature;
            IsNatureEnabled = false;
        }
        else
        {
            IsNatureEnabled = !IsDefaultSystemGroup;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Account group name is required.";
            return;
        }

        try
        {
            Guid? parentId = (SelectedParentGroup != null && SelectedParentGroup.Id != Guid.Empty)
                ? SelectedParentGroup.Id
                : null;

            if (_existingGroup == null)
            {
                var createDto = new CreateAccountGroupDto(
                    _companyId,
                    Name.Trim(),
                    parentId,
                    SelectedNature);

                SavedGroup = await _accountGroupService.CreateGroupAsync(createDto);
            }
            else
            {
                var updateDto = new UpdateAccountGroupDto(
                    _existingGroup.Id,
                    _companyId,
                    Name.Trim(),
                    parentId,
                    SelectedNature);

                SavedGroup = await _accountGroupService.UpdateGroupAsync(updateDto);
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
            ErrorMessage = $"An error occurred while saving: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke();
    }
}