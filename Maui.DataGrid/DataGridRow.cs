namespace Maui.DataGrid;

using Extensions;
using Microsoft.Maui.Controls;

internal sealed class DataGridRow : Grid
{
    private delegate bool ParserDelegate(string value);

    #region Fields

    private Color? _bgColor;
    private Color? _textColor;
    private bool _wasSelected;

    #endregion Fields

    #region Properties

    public DataGrid DataGrid
    {
        get => (DataGrid)GetValue(DataGridProperty);
        set => SetValue(DataGridProperty, value);
    }

    public object RowToEdit
    {
        get => GetValue(RowToEditProperty);
        set => SetValue(RowToEditProperty, value);
    }

    #endregion Properties

    #region Bindable Properties

    public static readonly BindableProperty DataGridProperty =
        BindablePropertyExtensions.Create<DataGridRow, DataGrid>(null, BindingMode.OneTime);

    public static readonly BindableProperty RowToEditProperty =
        BindablePropertyExtensions.Create<DataGridRow, object>(null, BindingMode.OneWay,
            propertyChanged: (b, o, n) =>
            {
                if (o == n || b is not DataGridRow row)
                {
                    return;
                }

                if (o == row.BindingContext || n == row.BindingContext)
                {
                    row.InitializeRow();
                }
            });

    #endregion Bindable Properties

    #region Methods

    private void InitializeRow()
    {
        UpdateSelectedState();

        UpdateColors();

        if (DataGrid.Columns == null || DataGrid.Columns.Count == 0)
        {
            ColumnDefinitions.Clear();
            return;
        }

        for (var i = 0; i < DataGrid.Columns.Count; i++)
        {
            var col = DataGrid.Columns[i];

            if (col.ColumnDefinition == null)
            {
                continue;
            }

            // Add or update columns as needed
            ColumnDefinitions.AddOrUpdate(col.ColumnDefinition, i);

            if (!col.IsVisible)
            {
                continue;
            }

            if (Children.TryGetItem(i, out var existingChild))
            {
                if (existingChild is not DataGridCell existingCell)
                {
                    throw new InvalidDataException($"{nameof(DataGridRow)} should only contain {nameof(DataGridCell)}s");
                }

                var isEditing = RowToEdit == BindingContext;

                if (existingCell.Column != col || existingCell.IsEditing != isEditing)
                {
                    Children[i] = GenerateCellForColumn(col, i);
                }
            }
            else
            {
                var newCell = GenerateCellForColumn(col, i);
                Children.Add(newCell);
            }
        }

        // Remove extra columns, if any
        ColumnDefinitions.RemoveAfter(DataGrid.Columns.Count);
    }

    private DataGridCell GenerateCellForColumn(DataGridColumn col, int columnIndex)
    {
        var dataGridCell = CreateCell(col);

        dataGridCell.UpdateBindings(DataGrid);

        SetColumn((BindableObject)dataGridCell, columnIndex);

        return dataGridCell;
    }

    private DataGridCell CreateCell(DataGridColumn col)
    {
        View cellContent;

        var isEditing = RowToEdit == BindingContext;

        if (isEditing)
        {
            cellContent = CreateEditCell(col);
        }
        else
        {
            cellContent = CreateViewCell(col);
        }

        return new DataGridCell(cellContent, _bgColor, col, isEditing);
    }

    private View CreateViewCell(DataGridColumn col)
    {
        View cell;

        if (col.CellTemplate != null)
        {
            cell = (View)col.CellTemplate.CreateContent();

            if (!string.IsNullOrWhiteSpace(col.PropertyName))
            {
                cell.SetBinding(BindingContextProperty,
                    new Binding(col.PropertyName, source: BindingContext));
            }
        }
        else
        {
            cell = new Label
            {
                TextColor = _textColor,
                VerticalTextAlignment = col.VerticalTextAlignment,
                HorizontalTextAlignment = col.HorizontalTextAlignment,
                LineBreakMode = col.LineBreakMode,
                FontSize = DataGrid.FontSize,
                FontFamily = DataGrid.FontFamily
            };

            if (!string.IsNullOrWhiteSpace(col.PropertyName))
            {
                cell.SetBinding(Label.TextProperty,
                    new Binding(col.PropertyName, stringFormat: col.StringFormat, source: BindingContext));
            }
        }

        return cell;
    }

    private View CreateEditCell(DataGridColumn col)
    {
        var cell = GenerateTemplatedEditCell(col);

        return cell ?? CreateDefaultEditCell(col);
    }

    private View CreateDefaultEditCell(DataGridColumn col)
    {
        return Type.GetTypeCode(col.DataType) switch
        {
            TypeCode.String => GenerateTextEditCell(col),
            TypeCode.Boolean => GenerateBooleanEditCell(col),
            TypeCode.Decimal => GenerateNumericEditCell(col, v => decimal.TryParse(v.TrimEnd(',', '.'), out _)),
            TypeCode.Double => GenerateNumericEditCell(col, v => double.TryParse(v.TrimEnd(',', '.'), out _)),
            TypeCode.Int16 => GenerateNumericEditCell(col, v => short.TryParse(v, out _)),
            TypeCode.Int32 => GenerateNumericEditCell(col, v => int.TryParse(v, out _)),
            TypeCode.Int64 => GenerateNumericEditCell(col, v => long.TryParse(v, out _)),
            TypeCode.SByte => GenerateNumericEditCell(col, v => sbyte.TryParse(v, out _)),
            TypeCode.Single => GenerateNumericEditCell(col, v => float.TryParse(v.TrimEnd(',', '.'), out _)),
            TypeCode.UInt16 => GenerateNumericEditCell(col, v => ushort.TryParse(v, out _)),
            TypeCode.UInt32 => GenerateNumericEditCell(col, v => uint.TryParse(v, out _)),
            TypeCode.UInt64 => GenerateNumericEditCell(col, v => ulong.TryParse(v, out _)),
            TypeCode.DateTime => GenerateDateTimeEditCell(col),
            _ => new TemplatedView(),
        };
    }

    private View? GenerateTemplatedEditCell(DataGridColumn col)
    {
        if (col.EditCellTemplate == null)
        {
            return null;
        }

        var cell = (View)col.EditCellTemplate.CreateContent();

        if (!string.IsNullOrWhiteSpace(col.PropertyName))
        {
            cell.SetBinding(BindingContextProperty,
                new Binding(col.PropertyName, source: BindingContext));
        }

        return cell;
    }

    private Entry GenerateTextEditCell(DataGridColumn col)
    {
        var entry = new Entry
        {
            TextColor = _textColor,
            VerticalTextAlignment = col.VerticalTextAlignment,
            HorizontalTextAlignment = col.HorizontalTextAlignment,
            FontSize = DataGrid.FontSize,
            FontFamily = DataGrid.FontFamily
        };

        if (!string.IsNullOrWhiteSpace(col.PropertyName))
        {
            entry.SetBinding(Entry.TextProperty,
                new Binding(col.PropertyName, BindingMode.TwoWay, stringFormat: col.StringFormat, source: BindingContext));
        }

        return entry;
    }

    private CheckBox GenerateBooleanEditCell(DataGridColumn col)
    {
        var checkBox = new CheckBox
        {
            Color = _textColor,
            BackgroundColor = _bgColor,
        };

        if (!string.IsNullOrWhiteSpace(col.PropertyName))
        {
            checkBox.SetBinding(CheckBox.IsCheckedProperty,
                new Binding(col.PropertyName, BindingMode.TwoWay, source: BindingContext));
        }

        return checkBox;
    }

    private Entry GenerateNumericEditCell(DataGridColumn col, ParserDelegate parserDelegate)
    {
        var entry = new Entry
        {
            TextColor = _textColor,
            VerticalTextAlignment = col.VerticalTextAlignment,
            HorizontalTextAlignment = col.HorizontalTextAlignment,
            FontSize = DataGrid.FontSize,
            FontFamily = DataGrid.FontFamily,
            Keyboard = Keyboard.Numeric
        };

        entry.TextChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.NewTextValue) && !parserDelegate(e.NewTextValue))
            {
                ((Entry)s!).Text = e.OldTextValue;
            }
        };

        if (!string.IsNullOrWhiteSpace(col.PropertyName))
        {
            entry.SetBinding(Entry.TextProperty,
                new Binding(col.PropertyName, BindingMode.TwoWay, source: BindingContext));
        }

        return entry;
    }

    private DatePicker GenerateDateTimeEditCell(DataGridColumn col)
    {
        var datePicker = new DatePicker
        {
            TextColor = _textColor,
        };

        if (!string.IsNullOrWhiteSpace(col.PropertyName))
        {
            datePicker.SetBinding(DatePicker.DateProperty,
                new Binding(col.PropertyName, BindingMode.TwoWay, source: BindingContext));
        }

        return datePicker;
    }

    private void UpdateColors()
    {
        var rowIndex = DataGrid.InternalItems.IndexOf(BindingContext);

        if (rowIndex == -1)
        {
            return;
        }

        _bgColor = DataGrid.SelectionMode != SelectionMode.None && _wasSelected
                ? DataGrid.ActiveRowColor
                : DataGrid.RowsBackgroundColorPalette.GetColor(rowIndex, BindingContext);
        _textColor = DataGrid.RowsTextColorPalette.GetColor(rowIndex, BindingContext);

        foreach (var cell in Children.OfType<DataGridCell>())
        {
            cell.UpdateCellColors(_bgColor, _textColor);
        }
    }

    /// <inheritdoc/>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InitializeRow();
    }

    /// <inheritdoc/>
    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent == null)
        {
            DataGrid.ItemSelected -= DataGrid_ItemSelected;
            DataGrid.Columns.CollectionChanged -= OnColumnsChanged;

            foreach (var column in DataGrid.Columns)
            {
                column.VisibilityChanged -= OnVisibilityChanged;
            }
        }
        else
        {
            DataGrid.ItemSelected += DataGrid_ItemSelected;
            DataGrid.Columns.CollectionChanged += OnColumnsChanged;

            foreach (var column in DataGrid.Columns)
            {
                column.VisibilityChanged += OnVisibilityChanged;
            }
        }
    }

    private void OnColumnsChanged(object? sender, EventArgs e)
    {
        InitializeRow();
    }

    private void OnVisibilityChanged(object? sender, EventArgs e)
    {
        InitializeRow();
    }

    private void DataGrid_ItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_wasSelected || (e.CurrentSelection.Count > 0 && e.CurrentSelection.Any(s => s == BindingContext)))
        {
            UpdateSelectedState();
            UpdateColors();
        }
    }

    private void UpdateSelectedState()
    {
        _wasSelected = DataGrid.SelectedItem == BindingContext || DataGrid.SelectedItems.Contains(BindingContext);
    }

    #endregion Methods
}
