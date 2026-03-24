# STIG Viewer Style Guide

This style guide contains all the colors, fonts, and UI patterns used in the STIG JSON Viewer application. Copy these files to your other WPF project to maintain a consistent look and feel.

## 📁 File Structure

```
Styles/
├── StyleGuide.xaml          ← Master file (merges all others)
├── Colors.xaml              ← Color palette
├── TextStyles.xaml          ← Text/font styles
├── BorderStyles.xaml        ← Panel/border patterns
├── ButtonStyles.xaml        ← Button styles
├── DataGridStyles.xaml      ← DataGrid styles
└── BadgeStyles.xaml         ← Status badges and summary boxes
```

## 🎨 How to Use

### 1. Copy Files
Copy the entire `Styles/` folder to your other WPF project.

### 2. Add to App.xaml
In your `App.xaml`, add the StyleGuide to Application Resources:

```xaml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Styles/StyleGuide.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### 3. Use Styles in Your Windows

#### Example: Window Header
```xaml
<!-- Dark blue header with title and subtitle -->
<Border Style="{StaticResource HeaderBorderStyle}">
    <StackPanel>
        <TextBlock Text="My Window Title" 
                   Style="{StaticResource HeaderTitleStyle}"/>
        <TextBlock Text="Subtitle or description" 
                   Style="{StaticResource HeaderSubtitleStyle}"/>
    </StackPanel>
</Border>
```

#### Example: Summary Statistic Boxes
```xaml
<!-- Clickable colored boxes showing counts/stats -->
<Border Style="{StaticResource SummaryBoxInfoStyle}"
        Cursor="Hand" 
        MouseLeftButtonDown="TotalBox_Click"
        ToolTip="Click for details">
    <StackPanel>
        <TextBlock Text="125" Style="{StaticResource SummaryNumberStyle}"/>
        <TextBlock Text="Total Items" Style="{StaticResource SummaryLabelStyle}"/>
    </StackPanel>
</Border>
```

#### Example: Status Badges
```xaml
<!-- Small colored badges for status/severity -->
<Border Style="{StaticResource SuccessBadgeStyle}">
    <TextBlock Text="✓ Matched" Style="{StaticResource BadgeTextStyle}"/>
</Border>

<Border Style="{StaticResource ErrorBadgeStyle}">
    <TextBlock Text="Missing" Style="{StaticResource BadgeTextStyle}"/>
</Border>
```

#### Example: Content Panels
```xaml
<!-- Info box with gray background -->
<Border Style="{StaticResource InfoBoxStyle}">
    <TextBlock Text="Description goes here" Style="{StaticResource BodyTextStyle}"/>
</Border>

<!-- Code/check content box (blue tint) -->
<Border Style="{StaticResource CodeBoxStyle}">
    <TextBlock Text="Check procedure" FontFamily="Consolas" FontSize="12"/>
</Border>

<!-- Success panel (green) -->
<Border Style="{StaticResource SuccessPanelStyle}">
    <TextBlock Text="Implementation details" Style="{StaticResource BodyTextStyle}"/>
</Border>
```

#### Example: DataGrid
```xaml
<DataGrid Style="{StaticResource StandardDataGridStyle}">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*">
            <DataGridTextColumn.ElementStyle>
                <Style TargetType="TextBlock" BasedOn="{StaticResource DataGridCellTextBoldStyle}"/>
            </DataGridTextColumn.ElementStyle>
        </DataGridTextColumn>
        
        <!-- Centered column -->
        <DataGridTextColumn Header="Count" Binding="{Binding Count}" Width="100">
            <DataGridTextColumn.ElementStyle>
                <Style TargetType="TextBlock" BasedOn="{StaticResource DataGridCellTextCenteredStyle}"/>
            </DataGridTextColumn.ElementStyle>
        </DataGridTextColumn>
    </DataGrid.Columns>
</DataGrid>
```

## 🎨 Color Palette Reference

### Primary Colors
- **PrimaryDark**: `#2C3E50` - Dark blue-gray (headers)
- **PrimaryLight**: `#ECF0F1` - Light gray (backgrounds)

### Status Colors
- **StatusSuccess**: `#27AE60` - Green (matched, implemented, success)
- **StatusWarning**: `#E67E22` - Orange (manual, warning, unmatched)
- **StatusError**: `#E74C3C` - Red (missing, error, failed)
- **StatusInfo**: `#3498DB` - Blue (info, total counts)
- **StatusPurple**: `#9B59B6` - Purple (coverage percentages)
- **StatusTeal**: `#1ABC9C` - Teal (automation percentages)

### Severity Colors
- **SeverityHigh**: `#E74C3C` - Red
- **SeverityMedium**: `#E67E22` - Orange
- **SeverityLow**: `#F1C40F` - Yellow
- **SeverityUnknown**: `#95A5A6` - Gray

### Text Colors
- **TextDark**: `#2C3E50` - Primary text
- **TextMedium**: `#34495E` - Secondary text
- **TextLight**: `#7F8C8D` - Hints/muted text

## 📐 Common Patterns

### Window Layout Pattern
```
┌─────────────────────────────────────┐
│ Header (Dark blue, white text)     │ ← HeaderBorderStyle
├─────────────────────────────────────┤
│ File Paths / Metadata              │ ← PanelBorderStyle
├─────────────────────────────────────┤
│ Summary Stats (Colored boxes)      │ ← SummaryBoxStyle variants
├─────────────────────────────────────┤
│ Main Content (DataGrid/List)       │ ← StandardDataGridStyle
├─────────────────────────────────────┤
│ Footer (Buttons, hints)            │ ← FooterBorderStyle
└─────────────────────────────────────┘
```

### Clickable Summary Boxes Pattern
```xaml
<StackPanel Orientation="Horizontal">
    <!-- Blue - Total/Info -->
    <Border Style="{StaticResource SummaryBoxInfoStyle}" 
            MouseLeftButtonDown="TotalBox_Click">
        <StackPanel>
            <TextBlock Text="125" Style="{StaticResource SummaryNumberStyle}"/>
            <TextBlock Text="Total" Style="{StaticResource SummaryLabelStyle}"/>
        </StackPanel>
    </Border>
    
    <!-- Green - Success/Matched -->
    <Border Style="{StaticResource SummaryBoxSuccessStyle}" 
            MouseLeftButtonDown="MatchedBox_Click">
        <StackPanel>
            <TextBlock Text="45" Style="{StaticResource SummaryNumberStyle}"/>
            <TextBlock Text="Matched" Style="{StaticResource SummaryLabelStyle}"/>
        </StackPanel>
    </Border>
    
    <!-- Orange - Warning/Manual -->
    <Border Style="{StaticResource SummaryBoxWarningStyle}" 
            MouseLeftButtonDown="ManualBox_Click">
        <StackPanel>
            <TextBlock Text="30" Style="{StaticResource SummaryNumberStyle}"/>
            <TextBlock Text="Manual" Style="{StaticResource SummaryLabelStyle}"/>
        </StackPanel>
    </Border>
    
    <!-- Red - Error/Missing -->
    <Border Style="{StaticResource SummaryBoxErrorStyle}" 
            MouseLeftButtonDown="MissingBox_Click">
        <StackPanel>
            <TextBlock Text="80" Style="{StaticResource SummaryNumberStyle}"/>
            <TextBlock Text="Missing" Style="{StaticResource SummaryLabelStyle}"/>
        </StackPanel>
    </Border>
</StackPanel>
```

### Severity Badge in DataGrid
```xaml
<DataGridTemplateColumn Header="Severity" Width="100">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Border Background="{Binding SeverityColor}" 
                    CornerRadius="3" 
                    Margin="5,2" 
                    Padding="6,3">
                <TextBlock Text="{Binding Severity}" 
                           Foreground="White" 
                           FontWeight="SemiBold"
                           TextAlignment="Center"
                           FontSize="11"/>
            </Border>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

Where `SeverityColor` is a property in your view model:
```csharp
public SolidColorBrush SeverityColor => Severity.ToLower() switch
{
    "high" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
    "medium" => new SolidColorBrush(Color.FromRgb(230, 126, 34)),
    "low" => new SolidColorBrush(Color.FromRgb(241, 196, 15)),
    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
};
```

## 🖼️ UI Component Examples

### Section Headers
```xaml
<!-- Section with border at bottom -->
<Border Style="{StaticResource PanelBorderStyle}">
    <StackPanel>
        <TextBlock Text="Summary" Style="{StaticResource SectionTitleStyle}"/>
        <!-- Content -->
    </StackPanel>
</Border>
```

### Step-by-Step Panels (like DisaSelectionWindow)
```xaml
<!-- Step 1 - Blue theme -->
<Border Style="{StaticResource HighlightedPanelBlueStyle}" Margin="0,0,0,15">
    <StackPanel>
        <TextBlock Text="📦 Step 1: Select File" 
                   FontSize="18" FontWeight="Bold" 
                   Foreground="{StaticResource TextDarkBrush}"/>
        <TextBlock Text="Description..." Style="{StaticResource BodyTextStyle}"/>
        <!-- Controls -->
    </StackPanel>
</Border>

<!-- Step 2 - Green theme -->
<Border Style="{StaticResource HighlightedPanelGreenStyle}">
    <StackPanel>
        <TextBlock Text="📋 Step 2: Process" 
                   FontSize="18" FontWeight="Bold" 
                   Foreground="{StaticResource TextDarkBrush}"/>
        <TextBlock Text="Description..." Style="{StaticResource BodyTextStyle}"/>
        <!-- Controls -->
    </StackPanel>
</Border>
```

### Buttons
```xaml
<!-- Primary action button -->
<Button Content="▶ Start" 
        Style="{StaticResource PrimaryButtonStyle}"
        Width="180" Height="42"/>

<!-- Secondary button -->
<Button Content="Cancel" 
        Style="{StaticResource SecondaryButtonStyle}"
        Width="120" Height="42"/>
```

## 💡 Tips for Consistency

### Font Sizes
- **32**: Main window title
- **28**: Large headers
- **24**: Window headers
- **18**: Section headers / Steps
- **16**: Menu buttons
- **14-15**: Body text, labels, DataGrid headers
- **13**: Standard text
- **11-12**: Small text, hints, badges

### Spacing
- **Padding**: 15-20px for sections, 10-12px for content
- **Margin**: 10-15px between sections, 5-8px between elements
- **Border thickness**: 1px standard, 2px for highlighted panels

### Consistent Patterns
1. **All windows** start with dark header (HeaderBorderStyle)
2. **Summary boxes** are clickable and use SummaryBoxStyle variants
3. **DataGrids** use StandardDataGridStyle with alternating rows
4. **Status badges** are rounded (CornerRadius="3") with white text
5. **Footers** use FooterBorderStyle with buttons on right

## 🚀 Quick Start for Your Other Project

1. Copy `Styles/` folder to your project
2. Update `App.xaml` to merge StyleGuide.xaml (see above)
3. Replace hard-coded colors/styles with StaticResource references
4. Test one window at a time

## 📞 Need Help?

These styles capture the exact look from STIG JSON Viewer. If you need help applying them to specific windows in your other project, share the XAML and we can update it together!
