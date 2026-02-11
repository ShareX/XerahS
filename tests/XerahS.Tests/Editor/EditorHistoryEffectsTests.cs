#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Editor;
using XerahS.Editor.Annotations;
using XerahS.Editor.ImageEffects.Adjustments;
using SkiaSharp;
using ImageEffect = XerahS.Editor.ImageEffects.ImageEffect;

namespace XerahS.Tests.Editor;

[TestFixture]
public class EditorHistoryEffectsTests
{
    private EditorCore _core = null!;

    [SetUp]
    public void SetUp()
    {
        _core = new EditorCore();
    }

    [TearDown]
    public void TearDown()
    {
        _core.Dispose();
    }

    #region Scenario 1: Add annotation → Apply effect → Undo effect → Undo annotation → Redo annotation → Redo effect

    [Test]
    public void Scenario1_AddAnnotation_ApplyEffect_UndoRedo_FullCycle()
    {
        var annotation = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 10),
            EndPoint = new SKPoint(100, 100)
        };
        var effect = new BrightnessImageEffect { Amount = 25 };

        // Add annotation
        _core.AddAnnotation(annotation);
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(0));

        // Apply effect
        _core.AddEffect(effect);
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Undo effect
        _core.Undo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotation should remain after undo effect");
        Assert.That(_core.Effects.Count, Is.EqualTo(0), "Effect should be removed after undo");

        // Undo annotation
        _core.Undo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(0), "Annotation should be removed after undo");
        Assert.That(_core.Effects.Count, Is.EqualTo(0), "Effects should remain empty");

        // Redo annotation
        _core.Redo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotation should be restored after redo");
        Assert.That(_core.Effects.Count, Is.EqualTo(0), "Effects should still be empty");

        // Redo effect
        _core.Redo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotation should still be present");
        Assert.That(_core.Effects.Count, Is.EqualTo(1), "Effect should be restored after redo");
        Assert.That(((BrightnessImageEffect)_core.Effects[0]).Amount, Is.EqualTo(25));
    }

    #endregion

    #region Scenario 2: Apply effect → Add annotation → Undo annotation → Undo effect → Redo effect → Redo annotation

    [Test]
    public void Scenario2_ApplyEffect_AddAnnotation_UndoRedo_ReverseCycle()
    {
        var effect = new BrightnessImageEffect { Amount = 30 };
        var annotation = new RectangleAnnotation
        {
            StartPoint = new SKPoint(20, 20),
            EndPoint = new SKPoint(80, 80)
        };

        // Apply effect
        _core.AddEffect(effect);
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Add annotation
        _core.AddAnnotation(annotation);
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Undo annotation
        _core.Undo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(0));
        Assert.That(_core.Effects.Count, Is.EqualTo(1), "Effect should remain after undo annotation");

        // Undo effect
        _core.Undo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(0));
        Assert.That(_core.Effects.Count, Is.EqualTo(0), "Effect should be removed after undo");

        // Redo effect
        _core.Redo();
        Assert.That(_core.Effects.Count, Is.EqualTo(1), "Effect should be restored after redo");
        Assert.That(_core.Annotations.Count, Is.EqualTo(0));

        // Redo annotation
        _core.Redo();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotation should be restored after redo");
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
    }

    #endregion

    #region Scenario 3: Mix multiple annotations and effects, undo/redo full stack

    [Test]
    public void Scenario3_MultipleAnnotationsAndEffects_FullUndoRedo()
    {
        var ann1 = new RectangleAnnotation { StartPoint = new SKPoint(0, 0), EndPoint = new SKPoint(50, 50) };
        var eff1 = new BrightnessImageEffect { Amount = 10 };
        var ann2 = new RectangleAnnotation { StartPoint = new SKPoint(60, 60), EndPoint = new SKPoint(120, 120) };
        var eff2 = new GrayscaleImageEffect();

        _core.AddAnnotation(ann1);  // Step 1
        _core.AddEffect(eff1);      // Step 2
        _core.AddAnnotation(ann2);  // Step 3
        _core.AddEffect(eff2);      // Step 4

        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        // Undo all 4 steps
        _core.Undo(); // Undo Step 4
        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Undo(); // Undo Step 3
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Undo(); // Undo Step 2
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(0));

        _core.Undo(); // Undo Step 1
        Assert.That(_core.Annotations.Count, Is.EqualTo(0));
        Assert.That(_core.Effects.Count, Is.EqualTo(0));

        Assert.That(_core.CanUndo, Is.False);

        // Redo all 4 steps
        _core.Redo(); // Redo Step 1
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(0));

        _core.Redo(); // Redo Step 2
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Redo(); // Redo Step 3
        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Redo(); // Redo Step 4
        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        Assert.That(_core.CanRedo, Is.False);
    }

    #endregion

    #region Scenario 4: After undo, new action clears redo

    [Test]
    public void Scenario4_NewActionAfterUndo_ClearsRedoStack()
    {
        var eff1 = new BrightnessImageEffect { Amount = 10 };
        var eff2 = new GrayscaleImageEffect();
        var eff3 = new BrightnessImageEffect { Amount = 50 };

        _core.AddEffect(eff1);
        _core.AddEffect(eff2);

        // Undo last effect
        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(_core.CanRedo, Is.True);

        // Add a new effect (should clear redo)
        _core.AddEffect(eff3);
        Assert.That(_core.CanRedo, Is.False, "Redo should be cleared after new action");
        Assert.That(_core.Effects.Count, Is.EqualTo(2));
        Assert.That(((BrightnessImageEffect)_core.Effects[1]).Amount, Is.EqualTo(50));
    }

    [Test]
    public void Scenario4_NewAnnotationAfterUndo_ClearsRedoStack()
    {
        var eff1 = new BrightnessImageEffect { Amount = 10 };
        var ann1 = new RectangleAnnotation { StartPoint = new SKPoint(0, 0), EndPoint = new SKPoint(50, 50) };

        _core.AddEffect(eff1);

        // Undo effect
        _core.Undo();
        Assert.That(_core.CanRedo, Is.True);

        // Add annotation (should clear redo)
        _core.AddAnnotation(ann1);
        Assert.That(_core.CanRedo, Is.False, "Redo should be cleared after new annotation");
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.Effects.Count, Is.EqualTo(0));
    }

    #endregion

    #region Scenario 5: Selection and state consistency

    [Test]
    public void Scenario5_EffectsChanged_EventFires_OnUndoRedo()
    {
        int effectsChangedCount = 0;
        _core.EffectsChanged += () => effectsChangedCount++;

        var effect = new BrightnessImageEffect { Amount = 20 };
        _core.AddEffect(effect);
        Assert.That(effectsChangedCount, Is.EqualTo(1));

        _core.Undo();
        Assert.That(effectsChangedCount, Is.EqualTo(2), "EffectsChanged should fire on undo");

        _core.Redo();
        Assert.That(effectsChangedCount, Is.EqualTo(3), "EffectsChanged should fire on redo");
    }

    [Test]
    public void Scenario5_CanUndo_CanRedo_Consistent()
    {
        Assert.That(_core.CanUndo, Is.False);
        Assert.That(_core.CanRedo, Is.False);

        _core.AddEffect(new BrightnessImageEffect());
        Assert.That(_core.CanUndo, Is.True);
        Assert.That(_core.CanRedo, Is.False);

        _core.Undo();
        Assert.That(_core.CanUndo, Is.False);
        Assert.That(_core.CanRedo, Is.True);

        _core.Redo();
        Assert.That(_core.CanUndo, Is.True);
        Assert.That(_core.CanRedo, Is.False);
    }

    #endregion

    #region Scenario 6: IsEnabled toggle and serialization consistency

    [Test]
    public void Scenario6_ToggleEffect_IsUndoable()
    {
        var effect = new BrightnessImageEffect { Amount = 15 };
        _core.AddEffect(effect);
        Assert.That(_core.Effects[0].IsEnabled, Is.True);

        // Toggle off
        _core.ToggleEffect(_core.Effects[0]);
        Assert.That(_core.Effects[0].IsEnabled, Is.False);

        // Undo toggle
        _core.Undo();
        Assert.That(_core.Effects[0].IsEnabled, Is.True, "IsEnabled should be restored on undo");

        // Redo toggle
        _core.Redo();
        Assert.That(_core.Effects[0].IsEnabled, Is.False, "IsEnabled should be toggled on redo");
    }

    [Test]
    public void Scenario6_OnlyEnabledEffects_AreApplied()
    {
        var effect1 = new BrightnessImageEffect { Amount = 10 };
        var effect2 = new GrayscaleImageEffect();
        _core.AddEffect(effect1);
        _core.AddEffect(effect2);

        // Disable effect2
        _core.ToggleEffect(_core.Effects[1]);

        var enabledEffects = _core.Effects.Where(e => e.IsEnabled).ToList();
        Assert.That(enabledEffects.Count, Is.EqualTo(1));
        Assert.That(enabledEffects[0], Is.TypeOf<BrightnessImageEffect>());
    }

    #endregion

    #region Defect regression: Effects during Crop tool are undoable

    [Test]
    public void Regression_EffectsDuringCropTool_AreUndoable()
    {
        _core.ActiveTool = EditorTool.Crop;

        var effect = new BrightnessImageEffect { Amount = 42 };
        _core.AddEffect(effect);

        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(_core.CanUndo, Is.True, "Effect during Crop tool should still be undoable");

        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(0), "Effect should be removed on undo even during Crop tool");
    }

    #endregion

    #region ClearAll resets effects

    [Test]
    public void ClearAll_ClearsEffects()
    {
        _core.AddEffect(new BrightnessImageEffect());
        _core.AddEffect(new GrayscaleImageEffect());
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        _core.ClearAll();
        Assert.That(_core.Effects.Count, Is.EqualTo(0));
        Assert.That(_core.CanUndo, Is.False);
        Assert.That(_core.CanRedo, Is.False);
    }

    #endregion

    #region Clone preserves IsEnabled

    [Test]
    public void Clone_PreservesIsEnabled()
    {
        var effect = new BrightnessImageEffect { Amount = 20, IsEnabled = false };
        var clone = effect.Clone();

        Assert.That(clone.IsEnabled, Is.False);
        Assert.That(((BrightnessImageEffect)clone).Amount, Is.EqualTo(20));
    }

    #endregion

    #region SetEffects (preset import) is undoable

    [Test]
    public void SetEffects_IsUndoable()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        var newEffects = new List<ImageEffect>
        {
            new GrayscaleImageEffect(),
            new BrightnessImageEffect { Amount = 99 }
        };

        _core.SetEffects(newEffects);
        Assert.That(_core.Effects.Count, Is.EqualTo(2));
        Assert.That(_core.Effects[0], Is.TypeOf<GrayscaleImageEffect>());

        // Undo preset import
        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(((BrightnessImageEffect)_core.Effects[0]).Amount, Is.EqualTo(10));
    }

    #endregion

    #region Scenario: InvalidateRequested fires on effect mutations (bug fix verification)

    [Test]
    public void InvalidateRequested_FiresOnAddEffect()
    {
        int invalidateCount = 0;
        _core.InvalidateRequested += () => invalidateCount++;

        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "InvalidateRequested should fire when effect is added");
    }

    [Test]
    public void InvalidateRequested_FiresOnRemoveEffect()
    {
        var effect = new BrightnessImageEffect { Amount = 10 };
        _core.AddEffect(effect);

        int invalidateCount = 0;
        _core.InvalidateRequested += () => invalidateCount++;

        _core.RemoveEffect(effect);
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "InvalidateRequested should fire when effect is removed");
    }

    [Test]
    public void InvalidateRequested_FiresOnToggleEffect()
    {
        var effect = new BrightnessImageEffect { Amount = 10 };
        _core.AddEffect(effect);

        int invalidateCount = 0;
        _core.InvalidateRequested += () => invalidateCount++;

        _core.ToggleEffect(effect);
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "InvalidateRequested should fire when effect is toggled");
    }

    [Test]
    public void InvalidateRequested_FiresOnUndoRedoEffects()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });

        int invalidateCount = 0;
        _core.InvalidateRequested += () => invalidateCount++;

        _core.Undo();
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "InvalidateRequested should fire on undo");

        invalidateCount = 0;
        _core.Redo();
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "InvalidateRequested should fire on redo");
    }

    #endregion

    #region Scenario 7: Crop + Effects interaction

    [Test]
    public void Scenario7_CropPreservesEffects()
    {
        // Load a test image
        var bitmap = new SKBitmap(200, 200);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
        }
        _core.LoadImage(bitmap);

        // Add an effect
        var effect = new BrightnessImageEffect { Amount = 30 };
        _core.AddEffect(effect);
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Add crop annotation
        var crop = new CropAnnotation
        {
            StartPoint = new SKPoint(10, 10),
            EndPoint = new SKPoint(100, 100)
        };
        _core.AddAnnotation(crop);

        // Perform crop
        _core.PerformCrop();

        // Effects should be preserved after crop
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(((BrightnessImageEffect)_core.Effects[0]).Amount, Is.EqualTo(30));
        // Source image should be cropped
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(90));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(90));
    }

    [Test]
    public void Scenario7_UndoCropRestoresEffectsAndImage()
    {
        // Load a test image
        var bitmap = new SKBitmap(200, 200);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
        }
        _core.LoadImage(bitmap);

        // Add effect before crop
        _core.AddEffect(new BrightnessImageEffect { Amount = 25 });

        // Add crop annotation and crop
        var crop = new CropAnnotation
        {
            StartPoint = new SKPoint(20, 20),
            EndPoint = new SKPoint(120, 120)
        };
        _core.AddAnnotation(crop);
        _core.PerformCrop();

        Assert.That(_core.SourceImage!.Width, Is.EqualTo(100));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Undo crop
        _core.Undo();

        // Original image and effects should be restored
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(200));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(200));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(((BrightnessImageEffect)_core.Effects[0]).Amount, Is.EqualTo(25));
    }

    [Test]
    public void Scenario7_AddEffectAfterCrop_UndoEffect_UndoCrop()
    {
        // Load a test image
        var bitmap = new SKBitmap(300, 300);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
        }
        _core.LoadImage(bitmap);

        // Add effect before crop
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });

        // Crop
        var crop = new CropAnnotation
        {
            StartPoint = new SKPoint(0, 0),
            EndPoint = new SKPoint(150, 150)
        };
        _core.AddAnnotation(crop);
        _core.PerformCrop();

        // Add another effect after crop
        _core.AddEffect(new GrayscaleImageEffect());
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        // Undo effect added after crop
        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(150), "Image should still be cropped");

        // Undo crop
        _core.Undo();
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(300), "Image should be restored to original");
        Assert.That(_core.Effects.Count, Is.EqualTo(1), "Effect before crop should still be present");

        // Redo crop
        _core.Redo();
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(150));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        // Redo effect after crop
        _core.Redo();
        Assert.That(_core.Effects.Count, Is.EqualTo(2));
    }

    #endregion

    #region Scenario 8: Preview effect (non-destructive real-time preview)

    [Test]
    public void Scenario8_SetPreviewEffect_DoesNotAffectEffectsList()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.SetPreviewEffect(new GrayscaleImageEffect());

        // Preview effect should NOT appear in the effects list
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        // But InvalidateRequested should have fired
    }

    [Test]
    public void Scenario8_SetPreviewEffect_DoesNotCreateUndoEntry()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });
        Assert.That(_core.CanUndo, Is.True);

        // Undo the add
        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(0));
        Assert.That(_core.CanUndo, Is.False);

        // Set preview — should NOT create undo entry
        _core.SetPreviewEffect(new GrayscaleImageEffect());
        Assert.That(_core.CanUndo, Is.False, "Preview should not create an undo entry");
    }

    [Test]
    public void Scenario8_ClearPreviewEffect_RestoresOriginalState()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });

        _core.SetPreviewEffect(new GrayscaleImageEffect());
        _core.ClearPreviewEffect();

        // Effects list unchanged
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(_core.Effects[0], Is.TypeOf<BrightnessImageEffect>());
    }

    [Test]
    public void Scenario8_CommitPreviewEffect_AddsToEffectsListWithUndo()
    {
        _core.AddEffect(new BrightnessImageEffect { Amount = 10 });

        var preview = new GrayscaleImageEffect();
        _core.SetPreviewEffect(preview);

        // Commit the preview
        _core.CommitPreviewEffect();

        // Effect should now be in the list
        Assert.That(_core.Effects.Count, Is.EqualTo(2));
        Assert.That(_core.Effects[1], Is.TypeOf<GrayscaleImageEffect>());

        // Should be undoable
        _core.Undo();
        Assert.That(_core.Effects.Count, Is.EqualTo(1));
        Assert.That(_core.Effects[0], Is.TypeOf<BrightnessImageEffect>());
    }

    [Test]
    public void Scenario8_PreviewDoesNotAffectAnnotations()
    {
        var ann = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 10),
            EndPoint = new SKPoint(50, 50)
        };
        _core.AddAnnotation(ann);
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));

        // Set and clear preview
        _core.SetPreviewEffect(new BrightnessImageEffect { Amount = 50 });
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotations preserved during preview");

        _core.ClearPreviewEffect();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotations preserved after cancel");

        // Set and commit preview
        _core.SetPreviewEffect(new GrayscaleImageEffect());
        _core.CommitPreviewEffect();
        Assert.That(_core.Annotations.Count, Is.EqualTo(1), "Annotations preserved after commit");
    }

    [Test]
    public void Scenario8_SetPreviewEffectFromFunc_FiresInvalidateRequested()
    {
        int invalidateCount = 0;
        _core.InvalidateRequested += () => invalidateCount++;

        _core.SetPreviewEffect(source => source.Copy());

        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "Func preview should fire InvalidateRequested");

        invalidateCount = 0;
        _core.ClearPreviewEffect();
        Assert.That(invalidateCount, Is.GreaterThanOrEqualTo(1), "ClearPreviewEffect should fire InvalidateRequested");
    }

    #endregion
}
