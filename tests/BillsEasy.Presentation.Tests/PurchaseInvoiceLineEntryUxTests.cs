using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Presentation.Wpf.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.Presentation.Tests;

public class PurchaseInvoiceLineEntryUxTests
{
    private readonly IPurchaseService _purchaseService = Substitute.For<IPurchaseService>();
    private readonly ILedgerService _ledgerService = Substitute.For<ILedgerService>();
    private readonly IAccountGroupService _accountGroupService = Substitute.For<IAccountGroupService>();
    private readonly IItemService _itemService = Substitute.For<IItemService>();
    private readonly IUnitService _unitService = Substitute.For<IUnitService>();
    private readonly IItemRepository _itemRepository = Substitute.For<IItemRepository>();
    private readonly IVoucherRepository _voucherRepository = Substitute.For<IVoucherRepository>();
    private readonly ICompanyContextService _contextService = Substitute.For<ICompanyContextService>();

    private readonly Guid _companyId;
    private readonly Guid _samsungUnitId = Guid.NewGuid();
    private readonly Guid _lenovoUnitId = Guid.NewGuid();
    private readonly Item _samsungItemEntity;
    private readonly Item _lenovoItemEntity;

    public PurchaseInvoiceLineEntryUxTests()
    {
        var company = Company.Create("Shree Krishna Electronics", "Shree Krishna", null, "Gujarat", "24", new DateOnly(2023, 4, 1), new DateOnly(2023, 4, 1));
        _companyId = company.Id;
        _contextService.ActiveCompany.Returns(company);

        var itemGroup = ItemGroup.Create(_companyId, "Electronics");
        _samsungItemEntity = new Item(Guid.NewGuid(), _companyId, itemGroup.Id, _samsungUnitId, "Samsung M21' Monitor");
        _lenovoItemEntity = new Item(Guid.NewGuid(), _companyId, itemGroup.Id, _lenovoUnitId, "Lenovo Business Laptop");

        _itemRepository.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<Item> { _samsungItemEntity, _lenovoItemEntity });

        var units = new List<UnitDto>
        {
            new(_samsungUnitId, _companyId, "Pcs", "Pieces", 0, DateTime.UtcNow, "admin", null, null),
            new(_lenovoUnitId, _companyId, "NOS", "Numbers", 0, DateTime.UtcNow, "admin", null, null)
        };
        _unitService.GetAllUnitsAsync(_companyId, Arg.Any<CancellationToken>()).Returns(units);
    }

    private PurchaseInvoiceEditViewModel CreateViewModel()
    {
        return new PurchaseInvoiceEditViewModel(
            _purchaseService,
            _ledgerService,
            _accountGroupService,
            _itemService,
            _unitService,
            _itemRepository,
            _voucherRepository,
            _contextService);
    }

    [Fact]
    public async Task InitializeAsync_LoadsCompanyScopedItemsOnly()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.AvailableItems.Should().HaveCount(2);
        vm.AvailableItems.Select(i => i.Name).Should().Contain(new[] { "Samsung M21' Monitor", "Lenovo Business Laptop" });
        await _itemRepository.Received(1).GetAllAsync(_companyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_FiltersSuppliersToSundryCreditorsOnly_ExcludesDebtorsAndCash()
    {
        // Arrange
        var creditorGroupId = Guid.NewGuid();
        var debtorGroupId = Guid.NewGuid();
        var cashGroupId = Guid.NewGuid();

        var groups = new List<AccountGroupDto>
        {
            CreateGroupDto(creditorGroupId, _companyId, "Sundry Creditors"),
            CreateGroupDto(debtorGroupId, _companyId, "Sundry Debtors"),
            CreateGroupDto(cashGroupId, _companyId, "Cash-in-hand")
        };
        _accountGroupService.GetAllGroupsAsync(_companyId, Arg.Any<CancellationToken>()).Returns(groups);

        var ledgers = new List<LedgerDto>
        {
            CreateLedgerDto(Guid.NewGuid(), _companyId, creditorGroupId, "Yashika Pvt Ltd."),
            CreateLedgerDto(Guid.NewGuid(), _companyId, debtorGroupId, "Apex Computers"),
            CreateLedgerDto(Guid.NewGuid(), _companyId, cashGroupId, "Cash")
        };
        _ledgerService.GetAllLedgersAsync(_companyId, Arg.Any<CancellationToken>()).Returns(ledgers);

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.SupplierOptions.Should().ContainSingle();
        vm.SupplierOptions.First().Name.Should().Be("Yashika Pvt Ltd.");
        vm.SupplierOptions.Select(s => s.Name).Should().NotContain("Apex Computers");
        vm.SupplierOptions.Select(s => s.Name).Should().NotContain("Cash");
    }

    private static AccountGroupDto CreateGroupDto(Guid id, Guid companyId, string name, Guid? parentId = null) =>
        new(id, companyId, name, parentId, null, GroupNature.Liability, "Liability", true, true, DateTime.UtcNow, "system", null, null);

    private static LedgerDto CreateLedgerDto(Guid id, Guid companyId, Guid groupId, string name) =>
        new(id, companyId, groupId, "Group", GroupNature.Liability, "Liability", name, name, null, 0, DebitCreditType.Credit, "0.00", false, null, null, null, null, null, null, null, null, "India", null, GSTRegistrationType.Regular, null, null, null, null, null, null, null, null, null, null, false, DateTime.UtcNow, "system", null, null);

    [Fact]
    public async Task ItemSelection_SelectSamsung_ResolvesUnitPcsAndRecalculatesAmount()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var line = vm.ItemLines[0];
        var samsung = vm.AvailableItems.First(i => i.Name.Contains("Samsung"));

        // Act
        line.SelectedItem = samsung;

        // Assert
        line.SelectedItem.Should().Be(samsung);
        line.UnitName.Should().Be("Pcs");
        line.UnitId.Should().Be(_samsungUnitId);
    }

    [Fact]
    public async Task ItemSelection_SearchMatching_FindsItemByPrefix()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var line = vm.ItemLines[0];

        // Act - Simulate typing "Len" in search text
        line.ItemSearchText = "Len";

        // Assert
        line.SelectedItem.Should().NotBeNull();
        line.SelectedItem!.Name.Should().Be("Lenovo Business Laptop");
        line.UnitName.Should().Be("NOS");
        line.UnitId.Should().Be(_lenovoUnitId);
    }

    [Fact]
    public async Task LineEntry_QuantityAndPriceEditing_CalculatesAmountCorrectly()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var line1 = vm.ItemLines[0];
        var samsung = vm.AvailableItems.First(i => i.Name.Contains("Samsung"));

        // Act
        line1.SelectedItem = samsung;
        line1.Quantity = 2;
        line1.Price = 5500;

        // Assert
        line1.Amount.Should().Be(11000);
        vm.ItemSubtotal.Should().Be(11000);
        vm.GrandTotal.Should().Be(11000);
    }

    [Fact]
    public async Task FullPurchaseScenario_Line1SamsungAndLine2Lenovo_CalculatesCorrectTotals()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Line 1: Samsung M21' Monitor (Qty=2, Price=5500 -> Amount=11000)
        var line1 = vm.ItemLines[0];
        var samsung = vm.AvailableItems.First(i => i.Name.Contains("Samsung"));
        line1.SelectedItem = samsung;
        line1.Quantity = 2;
        line1.Price = 5500;

        // Line 2: Lenovo Business Laptop (Qty=1, Price=40000 -> Amount=40000)
        vm.AddItemLine();
        var line2 = vm.ItemLines[1];
        var lenovo = vm.AvailableItems.First(i => i.Name.Contains("Lenovo"));
        line2.SelectedItem = lenovo;
        line2.Quantity = 1;
        line2.Price = 40000;

        // Assert
        line1.UnitName.Should().Be("Pcs");
        line1.Amount.Should().Be(11000);

        line2.UnitName.Should().Be("NOS");
        line2.Amount.Should().Be(40000);

        vm.ItemSubtotal.Should().Be(51000);
        vm.GrandTotal.Should().Be(51000);

        // Add Freight Sundry ₹500
        vm.AddSundryLine();
        var sundry = vm.SundryLines[0];
        sundry.Amount = 500;

        vm.SundrySubtotal.Should().Be(500);
        vm.GrandTotal.Should().Be(51500);
    }

    [Fact]
    public async Task AddItemWorkflow_CreatesNewRowResequencesAndIsReadyForSelection()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        // Act
        vm.AddItemLine();
        vm.AddItemLine();

        // Assert
        vm.ItemLines.Should().HaveCount(3);
        vm.ItemLines[0].SequenceNumber.Should().Be(1);
        vm.ItemLines[1].SequenceNumber.Should().Be(2);
        vm.ItemLines[2].SequenceNumber.Should().Be(3);
        vm.SelectedLine.Should().Be(vm.ItemLines[2]);
    }

    [Fact]
    public async Task DeleteItemWorkflow_RemovesLineResequencesAndRecalculatesTotals()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var samsung = vm.AvailableItems.First(i => i.Name.Contains("Samsung"));
        var lenovo = vm.AvailableItems.First(i => i.Name.Contains("Lenovo"));

        vm.ItemLines[0].SelectedItem = samsung;
        vm.ItemLines[0].Quantity = 2;
        vm.ItemLines[0].Price = 5500;

        vm.AddItemLine();
        vm.ItemLines[1].SelectedItem = lenovo;
        vm.ItemLines[1].Quantity = 1;
        vm.ItemLines[1].Price = 40000;

        vm.GrandTotal.Should().Be(51000);

        // Act - Delete Line 1 (Samsung)
        vm.SelectedLine = vm.ItemLines[0];
        vm.RemoveItemLine();

        // Assert
        vm.ItemLines.Should().HaveCount(1);
        vm.ItemLines[0].SelectedItem.Should().Be(lenovo);
        vm.ItemLines[0].SequenceNumber.Should().Be(1);
        vm.ItemSubtotal.Should().Be(40000);
        vm.GrandTotal.Should().Be(40000);
    }

    [Fact]
    public void Dialog_DataGrids_ConfiguredForEditing_NotReadOnly()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            var dialog = new BillsEasy.Presentation.Wpf.Views.Purchase.PurchaseInvoiceEditDialog(vm);
            dialog.Should().NotBeNull();

            var grids = FindLogicalChildren<System.Windows.Controls.DataGrid>(dialog).ToList();
            grids.Should().HaveCount(2);

            var itemsGrid = grids[0];
            var sundriesGrid = grids[1];

            itemsGrid.IsReadOnly.Should().BeFalse();
            sundriesGrid.IsReadOnly.Should().BeFalse();
        });
    }

    private static void EnsureAppWithThemes()
    {
        if (System.Windows.Application.Current == null)
        {
            var app = new System.Windows.Application();
            string[] themeFiles = new[]
            {
                "Colors.xaml",
                "Typography.xaml",
                "Spacing.xaml",
                "ButtonStyles.xaml",
                "InputStyles.xaml",
                "DataGridStyles.xaml",
                "TabControlStyles.xaml",
                "DialogStyles.xaml",
                "MenuStyles.xaml",
                "StatusBarStyles.xaml",
                "Icons.xaml"
            };

            foreach (var theme in themeFiles)
            {
                var uri = new Uri($"pack://application:,,,/BillsEasy.Presentation.Wpf;component/Themes/{theme}", UriKind.Absolute);
                var dict = new System.Windows.ResourceDictionary { Source = uri };
                app.Resources.MergedDictionaries.Add(dict);
            }

            app.Resources.Add("InverseNullToVis", new BillsEasy.Presentation.Wpf.Converters.InverseNullToVisibilityConverter());
            app.Resources.Add("NullToVis", new BillsEasy.Presentation.Wpf.Converters.NullToVisibilityConverter());
            app.Resources.Add("NullToVisibilityConverter", new BillsEasy.Presentation.Wpf.Converters.NullToVisibilityConverter());
            app.Resources.Add("BoolToVis", new System.Windows.Controls.BooleanToVisibilityConverter());
            app.Resources.Add("InverseBoolToVis", new BillsEasy.Presentation.Wpf.Converters.InverseBooleanToVisibilityConverter());
            app.Resources.Add("NullToBoolConverter", new BillsEasy.Presentation.Wpf.Converters.NullToBoolConverter());
            app.Resources.Add("IndianCurrency", new BillsEasy.Presentation.Wpf.Converters.IndianCurrencyConverter());
            app.Resources.Add("MoneyToStringConverter", new BillsEasy.Presentation.Wpf.Converters.MoneyToStringConverter());
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                EnsureAppWithThemes();
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
            throw new Exception("STA failure", ex);
    }

    private static IEnumerable<T> FindLogicalChildren<T>(System.Windows.DependencyObject depObj) where T : System.Windows.DependencyObject
    {
        if (depObj == null) yield break;

        foreach (object rawChild in System.Windows.LogicalTreeHelper.GetChildren(depObj))
        {
            if (rawChild is System.Windows.DependencyObject child)
            {
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T childOfChild in FindLogicalChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
