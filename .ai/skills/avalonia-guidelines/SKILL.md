---
name: avalonia-guidelines
description: XerahS-specific Avalonia rules (FluentAvalonia flyouts, ScrollViewer sizing, compiled bindings, MVVM toolkit) for AXAML or view-model work.
---

# Avalonia in XerahS

Generic Avalonia API knowledge is assumed; see https://docs.avaloniaui.net/ when unsure. This file lists only the conventions and pitfalls specific to XerahS (.NET 10, `.axaml`, FluentAvalonia theme, CommunityToolkit.Mvvm).

## FluentAvalonia: use ContextFlyout, not ContextMenu

`ContextMenu` uses legacy popup windows that render unstyled or invisible under `FluentAvaloniaTheme`. Always use `ContextFlyout` + `MenuFlyout`.

Flyouts live in a separate visual tree, so inside a `DataTemplate` bind to the view's view model through the parent:

```xml
<Border.ContextFlyout>
    <MenuFlyout>
        <MenuItem Header="Edit"
                  Command="{Binding $parent[UserControl].DataContext.EditCommand}"
                  CommandParameter="{Binding}"/>
    </MenuFlyout>
</Border.ContextFlyout>
```

Shared flyouts go in `UserControl.Resources` and are referenced with `{StaticResource}`.

## ScrollViewer needs a finite height

A `ScrollViewer` only scrolls when its parent passes a bounded height during measure. These XerahS layouts pass infinity and break scrolling:

| Cause | Fix |
|---|---|
| `SplitView` used as a two-column shell (default `VerticalContentAlignment=Top`) | `Grid ColumnDefinitions="auto,*"` |
| `TransitioningContentControl` as a page host | `ContentControl` with `HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch"` |
| `TabControl` at default `VerticalContentAlignment` | Set `VerticalContentAlignment="Stretch"` |
| `ScrollViewer Padding="N"` (shrinks viewport, not extent, so the last N px is unreachable) | Remove the padding; put `Margin="N"` on the inner panel |

```xml
<TabControl VerticalContentAlignment="Stretch">
    <TabItem Header="General">
        <ScrollViewer>
            <StackPanel Spacing="24" Margin="24"> ... </StackPanel>
        </ScrollViewer>
    </TabItem>
</TabControl>
```

## Repository conventions

- Compiled bindings everywhere: set `x:DataType` (and `x:CompileBindings="True"` where not global). Use `{Binding #Element.Path}`, never `{ReflectionBinding #...}`. Avoid runtime reflection for AOT/trimming.
- MVVM uses CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `partial` view models). Do not add ReactiveUI or `Avalonia.ReactiveUI`.
- Diagnostics use `AvaloniaUI.DiagnosticsSupport` + `AvaloniaUI.DeveloperTools`, not `Avalonia.Diagnostics`.
- Use `StyledProperty` for styleable custom properties and `DirectProperty` for non-styleable or hot-path ones.
- Use pseudo-class selectors or container queries instead of WPF triggers or `VisualStateManager`.
- Use `NavigationPage` / `TabbedPage` / `DrawerPage` rather than manual `ContentControl.Content` page swapping.
- Marshal to the UI thread with `Dispatcher.UIThread.InvokeAsync()`.
- Asset URIs use `avares://AssemblyName/path`.

For visual redesign of a window or dialog, use [design-ui-window](../design-ui-window/SKILL.md).
